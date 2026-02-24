# Maliev Job Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/MALIEV-Co-Ltd/Maliev.JobService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL-blue)](https://www.postgresql.org/)

Manages the physical production lifecycle of manufacturing jobs on the shop floor.

**Role in MALIEV Architecture**: The Job Service orchestrates the manufacturing queue. It transforms paid orders into actionable shop floor jobs, tracks machine assignment, and monitors production status (Pending → Queued → In Progress → Finishing → Completed).

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **Database**: PostgreSQL with Entity Framework Core 10.x
- **Messaging**: RabbitMQ via MassTransit
- **API Documentation**: OpenAPI 3.1 + Scalar UI
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
- ❌ **Swagger / Swashbuckle**: Using **Scalar** for API documentation.
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations or manual logic only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **Aspire Integration**: Fully integrated with Maliev.Aspire for local development.

---

## ✨ Key Features

- **Shop Floor Kanban**: Real-time job status tracking.
- **Machine Orchestration**: Automated assignment of jobs to specific machines (3D printers, CNC).
- **Order-to-Job Pipeline**: Automatically consumes `OrderPaidEvent` to create production jobs.
- **QR-Code Ready**: Status updates designed for mobile/Raspberry Pi scanning.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL & RabbitMQ

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/MALIEV-Co-Ltd/Maliev.JobService.git
cd Maliev.JobService
```

2. **Run via Aspire**
The easiest way to run the service is through the `Maliev.Aspire.AppHost` project.

3. **Manual Run**
```bash
dotnet run --project Maliev.JobService.Api
```

The service will be available at `http://localhost:5200/job`. Access the interactive documentation at `http://localhost:5200/job/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/job/v1/`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/jobs` | List all active jobs |
| GET | `/jobs/{id}` | Get specific job details |
| POST | `/jobs/{id}/status` | Update job production status |
| GET | `/machines` | List available manufacturing machines |

---

## 🧪 Testing

```bash
dotnet test --verbosity normal
```

---

## 📦 Deployment

Deployment is managed via ArgoCD using the `maliev-gitops` repository.

---

## 📄 License

Proprietary - © 2026 MALIEV Co., Ltd. All rights reserved.
