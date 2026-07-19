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

All commands run from within this service directory (`B:\maliev\Maliev.JobService`).

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.JobService.slnx

# Run all tests
dotnet test Maliev.JobService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~ClassName"

# Run with code coverage
dotnet test Maliev.JobService.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.JobService.slnx

# Run API
dotnet run --project Maliev.JobService.Api

# EF Core migrations (Infrastructure project only)
dotnet ef migrations add <MigrationName> --project Maliev.JobService.Infrastructure --startup-project Maliev.JobService.Infrastructure

# Database update
dotnet ef database update --project Maliev.JobService.Infrastructure --startup-project Maliev.JobService.Infrastructure
```

## 3. Code Style & Conventions

### Workspace Structure
```
Maliev.JobService/
├── Maliev.JobService.Api/           # Controllers, Consumers, Middleware
├── Maliev.JobService.Application/   # Use cases, DTOs, Interfaces, Handlers
├── Maliev.JobService.Domain/        # Entities, value objects, domain interfaces
├── Maliev.JobService.Infrastructure/ # EF Core DbContext, repositories, HTTP clients
├── Maliev.JobService.Tests/         # Unit + Integration tests (xUnit)
├── Directory.Build.props            # Central package versioning
└── Maliev.JobService.slnx          # Solution file (.slnx preferred over .sln)
```

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.JobService.Domain.Entities;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `CreateJobAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IJobService`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `job.jobs.create`, `job.kanban.read`
  - Invalid: `job.job.create` (singular), `job.create` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("job/v{version:apiVersion}")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing {JobId}", jobId)`
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
- **JSON**: Check existing conventions in this service for naming policy
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned

### Domain Entities
- **IDs**: Use `Guid` for primary keys.
- **Dates**: Use `DateTimeOffset` instead of `DateTime`.
- **Collections**: Initialize collection properties (e.g., `public ICollection<JobOperation> Operations { get; set; } = new List<JobOperation>();`).
- **Navigation Properties**: Mark as nullable if optional.

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

## Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/job/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

## 6. Testing Guidelines

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

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

## Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("job.resources.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with service domain (e.g., `/job`)
- **Scalar docs**: Configured at `/job/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

## Git Rules

- Each `Maliev.*` folder is an independent git repo. `cd` into it before git commands
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked

## Database & EF Core — Mandatory Rules

### EF Core Design Package
- `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects. It belongs ONLY in the Infrastructure project where migrations live.
- Migration commands must target Infrastructure as both project and startup-project:
  ```
  dotnet ef migrations add <Name> --project Maliev.JobService.Infrastructure --startup-project Maliev.JobService.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
