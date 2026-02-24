# Maliev.JobService — Shop Floor & Production Agent

## 🏭 Service Identity
**Service Name**: `Maliev.JobService`
**Role**: Manages the physical production lifecycle of manufacturing jobs on the shop floor.
**Domain**: Manufacturing Operations (Kanban, Machine Assignment, Job Tracking).

## 📜 Constitution & Compliance
This service strictly adheres to the **Maliev Technical Constitution** (see root `AGENTS.md`).
- **Framework**: .NET 10.0
- **Documentation**: Scalar UI at `/job/scalar`
- **Auth**: GCP-style Permissions (`job.jobs.read`, `job.jobs.write`)
- **Logging**: Serilog via ServiceDefaults
- **Database**: PostgreSQL (JobDbContext) via ServiceDefaults

## 🏗️ Architecture
- **API**: ASP.NET Core Web API
- **Events**: Consumes `OrderPaidEvent` (from OrderService) to create jobs.
- **State Machine**: Tracks `Pending` → `Queued` → `InProgress` → `Finishing` → `Completed`.

## 🛠️ Development Guidelines
1.  **Build**: `dotnet build Maliev.JobService.slnx`
2.  **Test**: `dotnet test` (Must maintain >80% coverage)
3.  **Run**: `dotnet run --project Maliev.JobService.Api` (Use Aspire for full stack)

## 📦 Dependencies
- `Maliev.Aspire.ServiceDefaults`: Observability, Resiliency.
- `Maliev.MessagingContracts`: Event definitions.
- `MassTransit.RabbitMQ`: Message bus.
- `Npgsql.EntityFrameworkCore.PostgreSQL`: Data persistence.
