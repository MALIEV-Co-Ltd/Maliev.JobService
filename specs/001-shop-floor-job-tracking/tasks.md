# Tasks: Shop Floor Job Tracking (Maliev.JobService)

**Input**: Design documents from `/specs/001-shop-floor-job-tracking/`
**Prerequisites**: plan.md, spec.md, data-model.md, contracts/

**Tests**: Not explicitly requested in feature specification. Tests excluded from task list.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

**Constraints**: 
- Scalar for API documentation (no Swagger)
- Manual mapping (no AutoMapper)
- DataAnnotations for validation (no FluentValidation)
- Standard xUnit assertions (no FluentAssertions)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- **Api project**: `Maliev.JobService.Api/`
- **Data project**: `Maliev.JobService.Data/`
- **Tests project**: `Maliev.JobService.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution file Maliev.JobService.slnx at repository root
- [X] T002 [P] Create web API project Maliev.JobService.Api/ with ASP.NET Core Controllers
- [X] T003 [P] Create class library project Maliev.JobService.Data/ for entities and DbContext
- [X] T004 [P] Create xUnit test project Maliev.JobService.Tests/
- [X] T005 Add project references: Api → Data, Tests → Api, Tests → Data
- [X] T006 [P] Add NuGet packages to Api: Npgsql.EntityFrameworkCore.PostgreSQL, MassTransit.RabbitMQ, Scalar.AspNetCore
- [X] T007 [P] Add NuGet packages to Data: Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL
- [X] T008 [P] Add NuGet packages to Tests: Moq, Testcontainers.PostgreSql
- [X] T009 [P] Create appsettings.json in Maliev.JobService.Api/ with ConnectionStrings, RabbitMQ, OrderService configuration
- [X] T010 [P] Create appsettings.Development.json in Maliev.JobService.Api/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T011 Create JobStatus enum in Maliev.JobService.Data/Entities/JobStatus.cs with values: Pending, Queued, InProgress, Finishing, Completed, Cancelled
- [X] T012 Create Job entity in Maliev.JobService.Data/Entities/Job.cs with all fields from data-model.md
- [X] T013 Create JobConfiguration in Maliev.JobService.Data/Configurations/JobConfiguration.cs with indexes and constraints
- [X] T014 Create JobDbContext in Maliev.JobService.Data/JobDbContext.cs with DbSet<Job> and model configuration
- [X] T015 Run EF Core migration: `dotnet ef migrations add InitialJobSchema` to create Migrations/
- [X] T016 Create Program.cs in Maliev.JobService.Api/ with: builder.Services.AddDbContext<JobDbContext>, AddControllers, AddEndpointsApiExplorer, AddScalarApi, basic middleware pipeline
- [X] T017 Register JobDbContext with PostgreSQL connection string in Program.cs
- [X] T018 Configure Scalar API documentation in Program.cs at /scalar/v1
- [X] T019 Add [Authorize] attribute infrastructure in Program.cs with authentication scheme for Employee role (placeholder for existing auth system)
- [X] T020 Create Metrics module in Maliev.JobService.Api/Metrics/JobMetrics.cs with counters for jobs per status and histogram for transition duration

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Job Auto-Creation from Paid Orders (Priority: P1) 🎯 MVP

**Goal**: Automatically create production jobs when an order is paid

**Independent Test**: Simulate an order payment event and verify jobs are created for each order item with correct data

**Performance Target (SC-001)**: Jobs created within 5 seconds of OrderPaidEvent

### Implementation for User Story 1

- [X] T021 [P] [US1] Create OrderItemDto record in Maliev.JobService.Api/DTOs/OrderItemDto.cs
- [X] T022 [P] [US1] Create IOrderServiceClient interface in Maliev.JobService.Api/Clients/IOrderServiceClient.cs
- [X] T023 [US1] Implement OrderServiceClient in Maliev.JobService.Api/Clients/OrderServiceClient.cs with HttpClient and Polly retry policy (3 retries, exponential backoff)
- [X] T024 [US1] Register IOrderServiceClient with HttpClient factory and Polly retry in Program.cs
- [X] T025 [US1] Create OrderPaidEventConsumer in Maliev.JobService.Api/Consumers/OrderPaidEventConsumer.cs implementing IConsumer<OrderPaidEvent>
- [X] T026 [US1] Implement priority calculation logic in OrderPaidEventConsumer: `Math.Max(0, (DeliveryDate - UtcNow).Days)` or 999 if null
- [X] T027 [US1] Implement deduplication check in OrderPaidEventConsumer using OrderId before creating jobs
- [X] T028 [US1] Configure MassTransit with RabbitMQ in Program.cs and register OrderPaidEventConsumer
- [X] T029 [US1] Add logging for job creation events in OrderPaidEventConsumer using ILogger
- [X] T030 [US1] Increment job count metric in JobMetrics when jobs are created

**Checkpoint**: User Story 1 complete - jobs are automatically created from OrderPaidEvent

---

## Phase 4: User Story 2 - Job Status Transitions (Priority: P1)

**Goal**: Enable machine operators to update job status through the workflow

**Independent Test**: Create a job and perform each status transition in sequence, verifying state changes and 409 on invalid transitions

**Performance Target (SC-002)**: Status transitions complete in under 2 seconds for 95% of requests

### Implementation for User Story 2

- [X] T031 [P] [US2] Create JobDto record in Maliev.JobService.Api/DTOs/JobDto.cs with all fields from FR-018
- [X] T032 [P] [US2] Create QueueJobRequest record in Maliev.JobService.Api/DTOs/QueueJobRequest.cs with DataAnnotations [Required] for MachineId
- [X] T033 [P] [US2] Create CancelJobRequest record in Maliev.JobService.Api/DTOs/CancelJobRequest.cs with DataAnnotations [Required] for Reason
- [X] T034 [P] [US2] Create ReassignJobRequest record in Maliev.JobService.Api/DTOs/ReassignJobRequest.cs with DataAnnotations [Required] for MachineId
- [X] T035 [US2] Create JobController in Maliev.JobService.Api/Controllers/JobController.cs with [ApiController], [Route("api/jobs")], [Authorize] attributes
- [X] T036 [US2] Implement GET /api/jobs/{id} endpoint in JobController to retrieve single job, return 404 if not found
- [X] T037 [US2] Implement POST /api/jobs/{id}/queue endpoint with transition validation (Pending → Queued), set AssignedMachineId
- [X] T038 [US2] Implement POST /api/jobs/{id}/start endpoint with transition validation (Pending|Queued → InProgress), set StartedAt, publish JobStartedEvent
- [X] T039 [US2] Implement POST /api/jobs/{id}/finish endpoint with transition validation (InProgress → Finishing)
- [X] T040 [US2] Implement POST /api/jobs/{id}/complete endpoint with transition validation (Finishing → Completed), set CompletedAt
- [X] T041 [US2] Implement POST /api/jobs/{id}/cancel endpoint (Any → Cancelled), store reason in Notes field
- [X] T042 [X] [US2] Implement PATCH /api/jobs/{id}/reassign endpoint (Queued status only) to change AssignedMachineId
- [X] T043 [US2] Create status transition validation helper method ValidateTransition(currentStatus, action) returning (isValid, errorMessage)
- [X] T044 [US2] Add logging for status transitions in JobController using ILogger with JobId, FromStatus, ToStatus
- [X] T045 [US2] Record transition duration metric in JobMetrics when status changes (time since previous state)

**Checkpoint**: User Story 2 complete - full job lifecycle can be managed via API

---

## Phase 5: User Story 3 - Kanban Board Visualization (Priority: P2)

**Goal**: Provide Kanban view grouping jobs by status for production managers

**Independent Test**: Create jobs in various statuses and verify they appear in correct columns sorted by priority

**Performance Target (SC-003)**: Kanban load under 3 seconds for 500 jobs

**Dependency Note**: US3 can technically start after Foundational, but test data population benefits from US2 completion

### Implementation for User Story 3

- [X] T046 [P] [US3] Create KanbanJobDto record in Maliev.JobService.Api/DTOs/KanbanJobDto.cs with: JobId, OrderId, Technology, MaterialId, AssignedMachineId, Priority, EstimatedPrintTimeMinutes, StartedAt, CompletedAt
- [X] T047 [P] [US3] Create KanbanResponse record in Maliev.JobService.Api/DTOs/KanbanResponse.cs with 6 columns: Pending, Queued, InProgress, Finishing, Completed, Cancelled (each as List<KanbanJobDto>)
- [X] T048 [US3] Implement GET /api/jobs/kanban endpoint in JobController: query all jobs with AsNoTracking, group by Status in memory
- [X] T049 [US3] Sort each Kanban column by Priority ascending (most urgent first)
- [X] T050 [US3] Add logging for Kanban view requests in JobController

**Checkpoint**: User Story 3 complete - Kanban view displays all jobs organized by status

---

## Phase 6: User Story 4 - Job List Search and Filtering (Priority: P3)

**Goal**: Enable search and filtering of jobs by status, technology, and machine

**Independent Test**: Create variety of jobs and verify filtering returns correct subset

### Implementation for User Story 4

- [X] T051 [P] [US4] Create PagedResult<T> record in Maliev.JobService.Api/DTOs/PagedResult.cs with Items, Total, Page, PageSize, TotalPages
- [X] T052 [P] [US4] Create JobListQueryParameters record in Maliev.JobService.Api/DTOs/JobListQueryParameters.cs with optional Status, Technology, AssignedMachineId, Page (default 1), PageSize (default 20)
- [X] T053 [US4] Implement GET /api/jobs endpoint in JobController with query parameters: status, technology, assignedMachineId, page, pageSize
- [X] T054 [US4] Implement filtering logic: Where clause for status (if provided), technology (if provided), assignedMachineId (if provided)
- [X] T055 [US4] Implement pagination: Skip((page-1)*pageSize).Take(pageSize), validate pageSize max 100
- [X] T056 [US4] Return PagedResult<JobDto> with items, total count, page, pageSize, totalPages calculated
- [X] T057 [US4] Add logging for job list queries in JobController

**Checkpoint**: User Story 4 complete - jobs can be searched and filtered

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T058 [P] Add health check endpoint in Program.cs using MapHealthChecks("/health")
- [X] T059 [P] Add input validation error handling middleware in Maliev.JobService.Api/Middleware/ValidationExceptionMiddleware.cs
- [X] T060 Configure structured logging with native ILogger throughout the service (ensure all log statements use parameter placeholders)
- [X] T061 Add error handling middleware for consistent error responses in Maliev.JobService.Api/Middleware/ExceptionMiddleware.cs
- [X] T062 [P] Update README.md with service overview and quickstart instructions
- [X] T063 Verify all acceptance criteria from spec.md are met by manual testing

---

## Summary

| Phase | Total | Completed | Status |
|-------|-------|-----------|--------|
| Phase 1: Setup | 10 | 10 | ✓ PASS |
| Phase 2: Foundational | 10 | 10 | ✓ PASS |
| Phase 3: US1 | 10 | 10 | ✓ PASS |
| Phase 4: US2 | 15 | 15 | ✓ PASS |
| Phase 5: US3 | 5 | 5 | ✓ PASS |
| Phase 6: US4 | 7 | 7 | ✓ PASS |
| Phase 7: Polish | 6 | 6 | ✓ PASS |

**Total**: 63 tasks | **Completed**: 63 | **Remaining**: 0

---

## Notes

- All tasks completed successfully.
- .NET 10 SDK used for development and verified via build/test.
- EF Core migration `InitialJobSchema` created.
- Unit tests verify core domain logic.
- Service is ready for deployment/integration.
