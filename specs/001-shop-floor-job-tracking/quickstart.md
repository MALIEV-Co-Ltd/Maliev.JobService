# Quickstart: Shop Floor Job Tracking

This guide provides essential commands and patterns for developing the JobService microservice.

## Prerequisites

- .NET 8 SDK
- Docker (for PostgreSQL and RabbitMQ)
- Entity Framework Core CLI tools

## Setup

### 1. Install EF Core Tools

```bash
dotnet tool install --global dotnet-ef
```

### 2. Start Infrastructure (Docker)

```bash
# PostgreSQL
docker run -d --name jobservice-postgres \
  -e POSTGRES_USER=maliev \
  -e POSTGRES_PASSWORD=maliev123 \
  -e POSTGRES_DB=jobservice \
  -p 5432:5432 \
  postgres:16

# RabbitMQ
docker run -d --name jobservice-rabbitmq \
  -e RABBITMQ_DEFAULT_USER=maliev \
  -e RABBITMQ_DEFAULT_PASS=maliev123 \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management
```

### 3. Create Solution Structure

```bash
# Create solution
dotnet new sln -n Maliev.JobService

# Create projects
dotnet new webapi -n Maliev.JobService.Api -o Maliev.JobService.Api
dotnet new classlib -n Maliev.JobService.Data -o Maliev.JobService.Data
dotnet new xunit -n Maliev.JobService.Tests -o Maliev.JobService.Tests

# Add to solution
dotnet sln add Maliev.JobService.Api
dotnet sln add Maliev.JobService.Data
dotnet sln add Maliev.JobService.Tests

# Add project references
dotnet add Maliev.JobService.Api reference Maliev.JobService.Data
dotnet add Maliev.JobService.Tests reference Maliev.JobService.Api
dotnet add Maliev.JobService.Tests reference Maliev.JobService.Data
```

### 4. Add NuGet Packages

```bash
# API project
dotnet add Maliev.JobService.Api package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Maliev.JobService.Api package MassTransit.RabbitMQ
dotnet add Maliev.JobService.Api package Scalar.AspNetCore

# Data project
dotnet add Maliev.JobService.Data package Microsoft.EntityFrameworkCore
dotnet add Maliev.JobService.Data package Npgsql.EntityFrameworkCore.PostgreSQL

# Tests project
dotnet add Maliev.JobService.Tests package Moq
dotnet add Maliev.JobService.Tests package Testcontainers.PostgreSql
```

## Development Commands

### Build

```bash
dotnet build
```

### Run Migrations

```bash
# Create migration
dotnet ef migrations add InitialJobSchema \
  --project Maliev.JobService.Data \
  --startup-project Maliev.JobService.Api

# Apply migrations
dotnet ef database update \
  --project Maliev.JobService.Data \
  --startup-project Maliev.JobService.Api
```

### Run Application

```bash
cd Maliev.JobService.Api
dotnet run
```

Application runs at:
- HTTP: `http://localhost:5000`
- Scalar API: `http://localhost:5000/scalar/v1`

### Run Tests

```bash
dotnet test
```

### Run Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=jobservice;Username=maliev;Password=maliev123"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "maliev",
    "Password": "maliev123"
  },
  "OrderService": {
    "BaseUrl": "http://localhost:5001"
  }
}
```

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

## Key Code Patterns

### Entity Configuration

```csharp
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(j => j.Id);
        builder.HasIndex(j => new { j.OrderId, j.OrderItemId }).IsUnique();
    }
}
```

### Status Transition Validation

```csharp
private static readonly Dictionary<JobStatus, HashSet<string>> ValidTransitions = new()
{
    [JobStatus.Pending] = new() { "queue", "start", "cancel" },
    [JobStatus.Queued] = new() { "start", "reassign", "cancel" },
    [JobStatus.InProgress] = new() { "finish", "cancel" },
    [JobStatus.Finishing] = new() { "complete", "cancel" },
    [JobStatus.Completed] = new() { "cancel" },
    [JobStatus.Cancelled] = new()
};

public bool IsValidTransition(JobStatus current, string action) =>
    ValidTransitions.GetValueOrDefault(current)?.Contains(action) ?? false;
```

### Priority Calculation

```csharp
public int CalculatePriority(DateTime? deliveryDate)
{
    if (!deliveryDate.HasValue)
        return 999;
    
    var daysRemaining = (deliveryDate.Value - DateTime.UtcNow).Days;
    return Math.Max(0, daysRemaining);
}
```

### Idempotent Consumer

```csharp
public async Task Consume(ConsumeContext<OrderPaidEvent> context)
{
    // Deduplication check
    if (await _dbContext.Jobs.AnyAsync(j => j.OrderId == context.Message.OrderId))
    {
        _logger.LogInformation("Jobs already exist for order {OrderId}", context.Message.OrderId);
        return; // Idempotent - acknowledge without creating duplicates
    }
    
    // Create jobs...
}
```

## API Testing

### Queue a Job

```bash
curl -X POST http://localhost:5000/api/jobs/{jobId}/queue \
  -H "Content-Type: application/json" \
  -d '{"machineId": "PRUSA-01"}'
```

### Start a Job

```bash
curl -X POST http://localhost:5000/api/jobs/{jobId}/start
```

### Get Kanban View

```bash
curl http://localhost:5000/api/jobs/kanban
```

### Get Jobs with Filtering

```bash
curl "http://localhost:5000/api/jobs?status=InProgress&technology=FDM&page=1&pageSize=20"
```

## Monitoring

### Health Check Endpoint

```
GET /health
```

### Metrics Endpoint (if configured)

```
GET /metrics
```

### Logs

Logs are written to console (structured JSON in production):

```json
{
  "timestamp": "2026-02-22T10:30:00Z",
  "level": "Information",
  "message": "Job status changed",
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fromStatus": "Queued",
  "toStatus": "InProgress"
}
```

## Troubleshooting

### Database Connection Issues

```bash
# Check PostgreSQL is running
docker ps | grep jobservice-postgres

# Connect to PostgreSQL
docker exec -it jobservice-postgres psql -U maliev -d jobservice
```

### RabbitMQ Issues

```bash
# Check RabbitMQ is running
docker ps | grep jobservice-rabbitmq

# Access management UI
open http://localhost:15672
# Login: maliev / maliev123
```

### Reset Database

```bash
dotnet ef database drop --force \
  --project Maliev.JobService.Data \
  --startup-project Maliev.JobService.Api

dotnet ef database update \
  --project Maliev.JobService.Data \
  --startup-project Maliev.JobService.Api
```
