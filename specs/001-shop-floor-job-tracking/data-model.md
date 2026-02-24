# Data Model: Shop Floor Job Tracking

**Feature**: 001-shop-floor-job-tracking
**Date**: 2026-02-22

## Entity Overview

```
┌─────────────────────────────────────────────────────────┐
│                        Job                               │
├─────────────────────────────────────────────────────────┤
│ Id                  : Guid (PK)                          │
│ OrderId             : Guid (indexed)                     │
│ OrderItemId         : Guid                               │
│ MaterialId          : Guid                               │
│ Technology          : string (required)                  │
│ VolumeCm3           : decimal                            │
│ EstimatedPrintTimeMinutes : int                          │
│ AssignedMachineId   : string? (indexed)                  │
│ Priority            : int (indexed)                      │
│ Status              : JobStatus (indexed)                │
│ Notes               : string?                            │
│ StartedAt           : DateTime?                          │
│ CompletedAt         : DateTime?                          │
│ CreatedAt           : DateTime                           │
│ UpdatedAt           : DateTime                           │
└─────────────────────────────────────────────────────────┘
```

## Entities

### JobStatus (Enum)

| Value | Ordinal | Description |
|-------|---------|-------------|
| Pending | 0 | Created from order, awaiting machine assignment |
| Queued | 1 | Assigned to machine, waiting to start |
| InProgress | 2 | Actively being worked on |
| Finishing | 3 | Post-processing underway |
| Completed | 4 | Ready for shipping |
| Cancelled | 5 | Halted for any reason |

### Job (Entity)

| Property | Type | Nullable | Index | Description |
|----------|------|----------|-------|-------------|
| Id | `Guid` | No | PK | Globally unique identifier |
| OrderId | `Guid` | No | IX_Jobs_OrderId | Reference to originating order |
| OrderItemId | `Guid` | No | - | Reference to order line item |
| MaterialId | `Guid` | No | - | Reference to material in MaterialService |
| Technology | `string` | No | - | Manufacturing technology (FDM, SLA, CNC, etc.) |
| VolumeCm3 | `decimal` | No | - | Volume in cubic centimeters |
| EstimatedPrintTimeMinutes | `int` | No | - | Estimated production time |
| AssignedMachineId | `string?` | Yes | IX_Jobs_AssignedMachineId | Machine identifier (set when queued) |
| Priority | `int` | No | IX_Jobs_Priority | Days until delivery (0 = most urgent, 999 = lowest) |
| Status | `JobStatus` | No | IX_Jobs_Status | Current lifecycle state |
| Notes | `string?` | Yes | - | Free-text notes (stores cancellation reason) |
| StartedAt | `DateTime?` | Yes | - | Timestamp when job started (set when InProgress) |
| CompletedAt | `DateTime?` | Yes | - | Timestamp when job completed (set when Completed) |
| CreatedAt | `DateTime` | No | - | Entity creation timestamp |
| UpdatedAt | `DateTime` | No | - | Last modification timestamp |

## Relationships

### External References (No Navigation Properties)

The Job entity holds references to external systems but does not have navigation properties:

- **OrderId** → OrderService (via HTTP API)
- **OrderItemId** → OrderService (via HTTP API)
- **MaterialId** → MaterialService (via events)
- **AssignedMachineId** → External machine registry (string identifier)

### Unique Constraint

A composite unique index ensures one job per order item:

```csharp
modelBuilder.Entity<Job>()
    .HasIndex(j => new { j.OrderId, j.OrderItemId })
    .IsUnique();
```

This supports FR-004.1: deduplication of OrderPaidEvents.

## State Transitions

### Valid Transitions

| Current State | Action | Target State | Side Effects |
|---------------|--------|--------------|--------------|
| Pending | queue | Queued | Set AssignedMachineId |
| Pending | start | InProgress | Set StartedAt, publish JobStartedEvent |
| Queued | start | InProgress | Set StartedAt, publish JobStartedEvent |
| Queued | reassign | Queued | Update AssignedMachineId only |
| InProgress | finish | Finishing | - |
| Finishing | complete | Completed | Set CompletedAt |
| Any | cancel | Cancelled | Store reason in Notes |

### Invalid Transitions

All transitions not listed above return HTTP 409 Conflict.

Specific examples from acceptance criteria:
- Complete a Pending job → 409
- Start a Completed job → 409
- Finish a Queued job → 409

## Validation Rules

### Job Creation (from OrderPaidEvent)

| Field | Rule | Source |
|-------|------|--------|
| Priority | `Math.Max(0, (DeliveryDate - UtcNow).Days)` or 999 if null | FR-003 |
| Status | Default to Pending | - |
| CreatedAt | `DateTime.UtcNow` | - |
| UpdatedAt | `DateTime.UtcNow` | - |
| VolumeCm3 | Accept negative/zero (OrderService validates) | Edge case |

### Job Updates

| Action | Validation |
|--------|------------|
| queue | Job must be in Pending status |
| start | Job must be in Pending or Queued status |
| finish | Job must be in InProgress status |
| complete | Job must be in Finishing status |
| cancel | Valid from any status (no validation) |
| reassign | Job must be in Queued status only |

## Indexes

| Index Name | Columns | Purpose |
|------------|---------|---------|
| PK_Jobs | Id | Primary key |
| IX_Jobs_OrderId | OrderId | Deduplication query |
| IX_Jobs_OrderId_OrderItemId | OrderId, OrderItemId | Unique constraint for deduplication |
| IX_Jobs_Status | Status | Kanban grouping, status filtering |
| IX_Jobs_AssignedMachineId | AssignedMachineId | Machine filtering |
| IX_Jobs_Priority | Priority | Priority sorting |

## EF Core Configuration

```csharp
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(j => j.Id);
        
        builder.Property(j => j.Technology)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(j => j.AssignedMachineId)
            .HasMaxLength(100);
            
        builder.Property(j => j.Notes)
            .HasMaxLength(2000);
            
        builder.Property(j => j.VolumeCm3)
            .HasPrecision(10, 3);
            
        builder.HasIndex(j => j.OrderId);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.AssignedMachineId);
        builder.HasIndex(j => j.Priority);
        
        builder.HasIndex(j => new { j.OrderId, j.OrderItemId })
            .IsUnique();
    }
}
```

## Query Patterns

### Kanban View

```csharp
var jobs = await dbContext.Jobs
    .AsNoTracking()
    .OrderBy(j => j.Priority)
    .ToListAsync(cancellationToken);

var kanban = jobs
    .GroupBy(j => j.Status)
    .ToDictionary(g => g.Key, g => g.ToList());
```

### Paginated Job List

```csharp
var query = dbContext.Jobs.AsNoTracking();

if (status.HasValue)
    query = query.Where(j => j.Status == status);
    
if (!string.IsNullOrEmpty(technology))
    query = query.Where(j => j.Technology == technology);
    
if (!string.IsNullOrEmpty(machineId))
    query = query.Where(j => j.AssignedMachineId == machineId);

var total = await query.CountAsync(cancellationToken);
var items = await query
    .OrderByDescending(j => j.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
```

### Deduplication Check

```csharp
var existingJob = await dbContext.Jobs
    .AnyAsync(j => j.OrderId == orderId, cancellationToken);
```
