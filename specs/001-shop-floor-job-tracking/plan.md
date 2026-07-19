# Implementation Plan: Shop Floor Job Tracking (Maliev.JobService)

**Branch**: `001-shop-floor-job-tracking` | **Date**: 2026-02-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-shop-floor-job-tracking/spec.md`

## Summary

A .NET microservice that digitalizes shop floor production management by automatically creating production jobs from paid orders, tracking job status through a defined workflow (Pending → Queued → InProgress → Finishing → Completed/Cancelled), and providing a Kanban-style visualization for production managers. The service integrates with OrderService via HTTP API and consumes OrderPaidEvent messages via MassTransit/RabbitMQ.

## Technical Context

**Language/Version**: .NET 8 (LTS)
**Primary Dependencies**: ASP.NET Core Controllers, Entity Framework Core 8, MassTransit, Npgsql (PostgreSQL provider)
**Storage**: PostgreSQL (strong consistency for status transitions, efficient Kanban queries)
**Testing**: xUnit, Moq, Testcontainers for integration tests
**Target Platform**: Linux container (Docker/Kubernetes)
**Project Type**: web-service (microservice)
**Performance Goals**: Jobs created within 5 seconds of OrderPaidEvent; status transitions <2s for 95% of requests; Kanban load <3s for 500 jobs
**Constraints**: 
- Native .NET logging only (no Serilog)
- REST/HTTP API with Controllers (not Minimal APIs)
- RabbitMQ message broker
- Scalar for API documentation (no Swagger)
- DataAnnotations for validation (no FluentValidation)
- Manual mapping (no AutoMapper)
- Standard xUnit assertions (no FluentAssertions)
**Scale/Scope**: Up to 500 concurrent jobs in Kanban view; indefinite job retention for analytics

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

No constitution file exists for this project. Default gates applied:

| Gate | Status | Notes |
|------|--------|-------|
| Single project structure | PASS | Using 3-project solution (Api, Data, Tests) |
| Clear separation of concerns | PASS | Api layer, Data layer, Test layer separated |
| Technology justified | PASS | PostgreSQL, RabbitMQ, MassTransit per clarifications |
| No external logging dependency | PASS | Native .NET logging per clarification |

## Project Structure

### Documentation (this feature)

```text
specs/001-shop-floor-job-tracking/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── order-service-client.md
│   └── job-api.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
Maliev.JobService/
├── Maliev.JobService.slnx
├── Maliev.JobService.Api/
│   ├── Controllers/
│   │   └── JobController.cs
│   ├── Clients/
│   │   ├── IOrderServiceClient.cs
│   │   └── OrderServiceClient.cs
│   ├── Consumers/
│   │   └── OrderPaidEventConsumer.cs
│   ├── DTOs/
│   │   ├── OrderItemDto.cs
│   │   ├── JobDto.cs
│   │   ├── KanbanResponse.cs
│   │   └── PagedResult.cs
│   ├── Program.cs
│   └── appsettings.json
├── Maliev.JobService.Data/
│   ├── Entities/
│   │   ├── Job.cs
│   │   └── JobStatus.cs
│   ├── JobDbContext.cs
│   └── Migrations/
└── Maliev.JobService.Tests/
    ├── Unit/
    │   ├── OrderPaidEventConsumerTests.cs
    │   └── JobControllerTests.cs
    └── Integration/
        └── JobWorkflowTests.cs
```

**Structure Decision**: Multi-project solution following standard .NET microservice patterns. Api project contains HTTP endpoints and message consumers. Data project contains EF Core entities and DbContext. Tests project contains unit and integration tests.

## Complexity Tracking

No violations to justify.

## Prerequisites

- The `Maliev.MessagingContracts` work package must be complete (`JobStartedEvent` contract must exist) before publishing events.
- Read `Maliev.OrderService` structure to understand the `/api/orders/{id}/items` endpoint shape before writing `IOrderServiceClient`.

## Implementation Phases

### Phase 1: Project Scaffold & Data Layer

1. Create solution and project structure
2. Define `JobStatus` enum
3. Define `Job` entity with all fields from spec
4. Create `JobDbContext` with indexes
5. Run initial EF Core migration

### Phase 2: OrderService Client

1. Define `IOrderServiceClient` interface
2. Implement HTTP client with retry logic
3. Define `OrderItemDto` record

### Phase 3: Event Consumer

1. Create `OrderPaidEventConsumer` implementing `IConsumer<OrderPaidEvent>`
2. Implement job creation logic with priority calculation
3. Implement deduplication by OrderId

### Phase 4: Job Controller

1. Create `JobController` with all endpoints
2. Implement status transition validation
3. Implement Kanban view endpoint
4. Implement paginated job list with filtering

### Phase 5: Program.cs & Configuration

1. Register DbContext with PostgreSQL
2. Register HTTP client for OrderService
3. Configure MassTransit with RabbitMQ
4. Configure Scalar API documentation

### Phase 6: Tests

1. Unit tests for consumer and controller
2. Integration tests for workflow scenarios
3. Verify all acceptance criteria pass
