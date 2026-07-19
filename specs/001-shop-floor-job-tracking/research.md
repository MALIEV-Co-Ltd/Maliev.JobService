# Research: Shop Floor Job Tracking

**Feature**: 001-shop-floor-job-tracking
**Date**: 2026-02-22

This document consolidates research findings for the JobService implementation.

## Technology Decisions

### Database: PostgreSQL

**Decision**: PostgreSQL as primary data store

**Rationale**:
- Strong consistency guarantees required for job status transitions
- Excellent EF Core provider support (Npgsql)
- Efficient filtering and sorting for Kanban queries
- Well-suited for entity-relationship model
- JSONB support for future extensibility (e.g., metadata field)

**Alternatives Considered**:
- SQL Server: Rejected - adds licensing complexity, PostgreSQL sufficient for requirements
- MongoDB: Rejected - relational model fits job tracking better; ACID transactions needed
- Azure Cosmos DB: Rejected - overkill for single-region microservice

### Message Broker: RabbitMQ

**Decision**: RabbitMQ as MassTransit transport

**Rationale**:
- Most common MassTransit transport
- Built-in retry and dead-letter queue capabilities
- Reliable message delivery with acknowledgments
- Aligns with FR-004 retry requirements

**Alternatives Considered**:
- Azure Service Bus: Rejected - adds cloud dependency
- In-Memory: Rejected - no persistence, not production-ready
- Amazon SQS/SNS: Rejected - adds AWS dependency

### API Style: REST/HTTP

**Decision**: REST over HTTP with ASP.NET Core Controllers

**Rationale**:
- Implied by acceptance criteria (409 Conflict responses)
- Standard for .NET microservices
- Good tooling support with Scalar for API documentation
- Aligns with existing Maliev services architecture

**Alternatives Considered**:
- gRPC: Rejected - adds complexity, REST sufficient for requirements
- GraphQL: Rejected - overkill for CRUD-style operations
- Minimal APIs: Rejected - Controllers preferred for organized endpoint structure

### Logging: Native .NET ILogger

**Decision**: Use built-in Microsoft.Extensions.Logging

**Rationale**:
- Explicit clarification: no external logging libraries
- Structured logging supported out of the box
- Compatible with standard .NET observability patterns

**Alternatives Considered**:
- Serilog: Rejected - per clarification requirement

### Testing Framework: xUnit

**Decision**: xUnit with Moq and standard xUnit assertions

**Rationale**:
- Standard for .NET 8 projects
- Testcontainers for PostgreSQL integration tests
- No FluentAssertions - standard xUnit assertions for simplicity

**Alternatives Considered**:
- FluentAssertions: Rejected - prefer standard xUnit assertions for fewer dependencies

## Integration Patterns

### OrderService Client

**Pattern**: HTTP Client with typed client registration

**Decisions**:
- Use `IHttpClientFactory` for connection management
- Configure Polly retry policy for transient failures
- Return empty list on 404 (non-manufacturable orders)
- Throw on other errors (let MassTransit handle retry)

**API Shape** (assumed until OrderService reviewed):
```
GET /api/orders/{orderId}/items
Response: [
  {
    orderItemId: Guid,
    materialId: Guid,
    technology: string,
    volumeCm3: decimal,
    estimatedPrintTimeMinutes: int,
    deliveryDate: DateTime?
  }
]
```

### MassTransit Configuration

**Pattern**: Consumer with retry and dead-letter queue

**Decisions**:
- Use `UseMessageRetry` for transient failures
- Configure error queue for poison messages
- Implement idempotent consumer (deduplication by OrderId)

## Data Model Decisions

### Job Entity

**Primary Key**: GUID (JobId)
**Natural Key**: (OrderId, OrderItemId) - for deduplication

**Indexes**:
- `IX_Jobs_OrderId` - for deduplication queries
- `IX_Jobs_Status` - for Kanban grouping
- `IX_Jobs_AssignedMachineId` - for machine filtering
- `IX_Jobs_Priority` - for priority sorting

**Status Transitions** (state machine):
```
                    ┌──────────┐
                    │ Pending  │
                    └────┬─────┘
                         │ queue
                    ┌────▼─────┐
          ┌─────────│  Queued  │
          │         └────┬─────┘
          │              │ start
          │         ┌────▼─────┐
          │         │InProgress│
          │         └────┬─────┘
          │              │ finish
          │         ┌────▼─────┐
          │         │Finishing │
          │         └────┬─────┘
          │              │ complete
          │         ┌────▼─────┐
          │         │Completed │
          │         └──────────┘
          │
          │ (from any state)
          └────────►┌──────────┐
                    │Cancelled │
                    └──────────┘
```

### Priority Calculation

**Formula**: `Priority = Math.Max(0, (DeliveryDate - DateTime.UtcNow).Days)`

**Default**: 999 (lowest urgency) when no delivery date

**Clamp**: Minimum 0 (highest urgency for past-due orders)

## Performance Considerations

### Kanban Query Optimization

**Approach**: Single query returning all jobs, group in memory

**Rationale**:
- 500 jobs is small enough for in-memory grouping
- Single database round-trip
- Simpler than 6 separate queries per status

### Pagination

**Approach**: Offset-based pagination with `Skip`/`Take`

**Page Size**: Default 20, configurable via query parameter

**Rationale**:
- Standard EF Core pagination pattern
- Sufficient for current requirements
- Cursor-based pagination can be added later if needed

## Observability Metrics

**Metrics to Expose**:
1. `jobs_total` - Counter by status (Pending, Queued, InProgress, Finishing, Completed, Cancelled)
2. `job_transition_duration_seconds` - Histogram of time between status transitions
3. `jobs_created_total` - Counter of jobs created from OrderPaidEvent

**Logging Points**:
1. Job created (Info level with OrderId, JobId)
2. Status transition (Info level with JobId, FromStatus, ToStatus)
3. OrderService API failure (Warning level with OrderId, error details)
4. Invalid transition attempt (Warning level with JobId, attempted action)

## Open Questions (Resolved)

| Question | Resolution |
|----------|------------|
| Database choice | PostgreSQL - strong consistency for transitions |
| Message broker | RabbitMQ - reliable delivery with DLQ |
| Job retention | Indefinite - analytics/audit value |
| API style | REST/HTTP - standard for microservices |
| Logging library | Native .NET - per clarification |
| Authorization model | Single "Employee" role |
| Concurrent updates | Last write wins |
| Cancelled in Kanban | Yes, separate column |
| Machine reassignment | From Queued status only |
