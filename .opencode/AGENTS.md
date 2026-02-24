# Maliev.JobService Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-22

## Active Technologies

- **Language**: .NET 8 (C# 12)
- **Framework**: ASP.NET Core Controllers
- **ORM**: Entity Framework Core 8
- **Database**: PostgreSQL 16
- **Message Broker**: RabbitMQ with MassTransit
- **API Documentation**: Scalar (no Swagger)
- **Testing**: xUnit, Moq, Testcontainers
- **Containerization**: Docker

## Constraints

- No external logging libraries (native ILogger only)
- No AutoMapper (manual mapping)
- No FluentValidation (DataAnnotations)
- No FluentAssertions (standard xUnit assertions)

## Project Structure

```text
Maliev.JobService/
├── Maliev.JobService.slnx
├── Maliev.JobService.Api/          # HTTP API + Message Consumers
│   ├── Controllers/
│   ├── Clients/
│   ├── Consumers/
│   ├── DTOs/
│   └── Program.cs
├── Maliev.JobService.Data/         # EF Core Entities + DbContext
│   ├── Entities/
│   ├── JobDbContext.cs
│   └── Migrations/
└── Maliev.JobService.Tests/        # Unit + Integration Tests
    ├── Unit/
    └── Integration/
```

## Commands

### Build & Run

```bash
dotnet build                              # Build solution
dotnet run --project Maliev.JobService.Api # Run API
dotnet test                               # Run all tests
```

### EF Core

```bash
dotnet ef migrations add <Name> --project Maliev.JobService.Data --startup-project Maliev.JobService.Api
dotnet ef database update --project Maliev.JobService.Data --startup-project Maliev.JobService.Api
dotnet ef database drop --force --project Maliev.JobService.Data --startup-project Maliev.JobService.Api
```

### Docker Infrastructure

```bash
docker run -d --name jobservice-postgres -e POSTGRES_USER=maliev -e POSTGRES_PASSWORD=maliev123 -e POSTGRES_DB=jobservice -p 5432:5432 postgres:16
docker run -d --name jobservice-rabbitmq -e RABBITMQ_DEFAULT_USER=maliev -e RABBITMQ_DEFAULT_PASS=maliev123 -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

## Code Style

### C# Conventions

- Use file-scoped namespaces
- Use primary constructors where appropriate
- Use records for DTOs
- Use `required` keyword for required properties
- Nullable reference types enabled

### Entity Example

```csharp
namespace Maliev.JobService.Data.Entities;

public class Job
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public required string Technology { get; set; }
    public JobStatus Status { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### DTO Example

```csharp
namespace Maliev.JobService.Api.DTOs;

public record JobDto
{
    public required Guid JobId { get; init; }
    public required Guid OrderId { get; init; }
    public required string Status { get; init; }
    public int Priority { get; init; }
}
```

### Controller Example

```csharp
namespace Maliev.JobService.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<JobDto>> GetById(Guid id)
    {
        // Implementation
    }
}
```

### Consumer Example

```csharp
namespace Maliev.JobService.Api.Consumers;

public class OrderPaidEventConsumer : IConsumer<OrderPaidEvent>
{
    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        // Implementation
    }
}
```

## Logging

- Use native `ILogger<T>` only (no Serilog or external libraries)
- Structured logging with parameter placeholders

```csharp
_logger.LogInformation("Job {JobId} transitioned from {FromStatus} to {ToStatus}", 
    jobId, fromStatus, toStatus);
```

## Recent Changes

### Feature: 001-shop-floor-job-tracking (2026-02-22)

- Initial microservice setup
- Job entity with 6 status states
- REST API for job management
- MassTransit consumer for OrderPaidEvent
- PostgreSQL with EF Core
- RabbitMQ message broker integration

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
