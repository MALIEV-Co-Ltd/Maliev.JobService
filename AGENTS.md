# Maliev.JobService — Shop Floor & Production Agent

This document contains instructions for AI agents operating in this repository.

## 1. Service Scope

**Service Name**: `Maliev.JobService`
**Role**: Manages the physical production lifecycle of manufacturing jobs on the shop floor.
**Domain**: Manufacturing Operations (Kanban, Machine Assignment, Job Tracking).

### Key Responsibilities
- **Job Lifecycle**: Track jobs through states: `Pending` → `Queued` → `InProgress` → `Finishing` → `Completed`
- **Shop Floor Kanban**: Provide real-time Kanban board for production queue
- **Machine Assignment**: Assign jobs to machines (FDM, SLA, CNC)
- **QR Code Workflow**: Single QR per job + confirmation tap pattern (see root AGENTS.md)
- **Event Consumption**: Consumes `OrderPaidEvent` from OrderService to create jobs
- **Capacity Integration**: Works with FacilityService for real-time capacity visibility

## 2. Environment & Build

- **Framework**: .NET 10.0 (C# 13)
- **Database**: PostgreSQL 18 (using Entity Framework Core 10)
- **Architecture**: Clean Architecture (Api, Application, Domain, Infrastructure, Tests)
- **TreatWarningsAsErrors**: ENABLED. Zero compilation warnings allowed.
- **Documentation**: Scalar UI at `/job/scalar`

### Commands

- **Build**: `dotnet build Maliev.JobService.slnx`
- **Test (All)**: `dotnet test`
- **Test (Single)**: `dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"`
- **Run API**: `dotnet run --project Maliev.JobService.Api`
- **Database Migrations**: `dotnet ef migrations add <MigrationName> --project Maliev.JobService.Infrastructure --startup-project Maliev.JobService.Api`
- **Database Update**: `dotnet ef database update --project Maliev.JobService.Infrastructure --startup-project Maliev.JobService.Api`

## 3. Code Style & Conventions

### General
- **Namespaces**: Use file-scoped namespaces (e.g., `namespace Maliev.JobService.Domain.Entities;`).
- **Formatting**: Standard C# conventions (PascalCase for classes/methods, camelCase for local variables).
- **Nullability**: `Nullable` context is ENABLED. Handle nulls explicitly. Use `?` for optional references.
- **Documentation**: XML documentation `///` is **REQUIRED** for all public methods and properties.

### Domain Entities
- **IDs**: Use `Guid` for primary keys.
- **Dates**: Use `DateTimeOffset` instead of `DateTime`.
- **Collections**: Initialize collection properties (e.g., `public ICollection<JobOperation> Operations { get; set; } = new List<JobOperation>();`).
- **Navigation Properties**: Mark as nullable if optional.

### Architecture Rules (Strict)
- **No AutoMapper**: Perform manual mapping.
- **No FluentValidation**: Use Data Annotations (`[Required]`, `[EmailAddress]`).
- **No FluentAssertions**: Use standard xUnit `Assert`.
- **No In-Memory DB**: Use **Testcontainers** for integration tests.
- **No Secrets**: Configuration via environment variables only.

## 4. Permissions

Use GCP-style permissions with plural resource format:

| Permission | Resource | Action |
|------------|----------|--------|
| `job.jobs.read` | jobs | List, Get |
| `job.jobs.create` | jobs | Create, Start, Assign |
| `job.jobs.update` | jobs | Update, AdvanceStatus |
| `job.jobs.delete` | jobs | Delete |
| `job.kanban.read` | kanban | ViewBoard |

## 5. Events

### Consumed
- `OrderPaidEvent` — Triggers job creation when order is paid

### Published
- `JobCreatedEvent` — When new job is created
- `JobStatusChangedEvent` — When job status advances
- `JobStartedEvent` — When job starts (triggers InventoryService material deduction)

## 6. Testing Guidelines

- **Integration over Unit**: Prioritize integration tests using Testcontainers/PostgreSQL.
- **Naming**: `MethodName_StateUnderWhichTestIsRunning_ExpectedBehavior` (e.g., `AdvanceStatus_FromInProgressToFinishing_UpdatesState`).
- **Structure**: Arrange, Act, Assert comments are optional but encouraged for complex tests.

## 7. Specific Workflows

### QR Code Workflow
The "single QR per job + confirmation tap" pattern:
1. Each job has **one QR code** on the job traveler document
2. Scanning opens a mobile-optimized status card
3. Status card shows current status + 1-2 large action buttons
4. Employee taps to confirm (not auto-advance)
5. Satisfies "3-second rule" (~2 seconds: scan + tap)

### Adding a New Job Status
1. Add to `JobStatus` enum in Domain
2. Update state machine logic in Application
3. Add controller endpoint if needed
4. Add integration test

## 8. Agent Behavior
- **Proactive Fixes**: If you see a warning, fix it.
- **Verification**: ALWAYS run `dotnet build` after changes.
- **Safety**: Do not commit secrets.


## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure, not Api:
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project ../Maliev.<Domain>Service.Api
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
