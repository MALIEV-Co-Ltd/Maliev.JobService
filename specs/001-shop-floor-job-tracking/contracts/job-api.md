# Job API Contract

**Service**: Maliev.JobService
**Version**: 1.0.0
**Base URL**: `/api/jobs`

## Authentication

All endpoints require authentication with "Employee" role. Authorization header:
```
Authorization: Bearer {token}
```

## Endpoints

### GET /api/jobs/kanban

Returns all jobs grouped by status for Kanban visualization.

**Response**: `200 OK`

```json
{
  "pending": [
    {
      "jobId": "guid",
      "orderId": "guid",
      "technology": "FDM",
      "materialId": "guid",
      "assignedMachineId": null,
      "priority": 5,
      "estimatedPrintTimeMinutes": 120,
      "startedAt": null,
      "completedAt": null
    }
  ],
  "queued": [...],
  "inProgress": [...],
  "finishing": [...],
  "completed": [...],
  "cancelled": [...]
}
```

**Status Codes**:
- `200 OK` - Jobs retrieved successfully
- `401 Unauthorized` - Missing or invalid token

---

### GET /api/jobs

Returns paginated list of jobs with optional filtering.

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| status | string | No | Filter by status (Pending, Queued, InProgress, Finishing, Completed, Cancelled) |
| technology | string | No | Filter by technology (e.g., FDM, SLA, CNC) |
| assignedMachineId | string | No | Filter by assigned machine |
| page | int | No | Page number (default: 1) |
| pageSize | int | No | Items per page (default: 20, max: 100) |

**Response**: `200 OK`

```json
{
  "items": [
    {
      "jobId": "guid",
      "orderId": "guid",
      "orderItemId": "guid",
      "technology": "FDM",
      "materialId": "guid",
      "volumeCm3": 150.5,
      "estimatedPrintTimeMinutes": 120,
      "assignedMachineId": "PRUSA-01",
      "priority": 5,
      "status": "InProgress",
      "notes": null,
      "startedAt": "2026-02-22T10:30:00Z",
      "completedAt": null,
      "createdAt": "2026-02-21T08:00:00Z",
      "updatedAt": "2026-02-22T10:30:00Z"
    }
  ],
  "total": 50,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

**Status Codes**:
- `200 OK` - Jobs retrieved successfully
- `400 Bad Request` - Invalid query parameters
- `401 Unauthorized` - Missing or invalid token

---

### GET /api/jobs/{id}

Returns a single job by ID.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | guid | Yes | Job ID |

**Response**: `200 OK`

```json
{
  "jobId": "guid",
  "orderId": "guid",
  "orderItemId": "guid",
  "technology": "FDM",
  "materialId": "guid",
  "volumeCm3": 150.5,
  "estimatedPrintTimeMinutes": 120,
  "assignedMachineId": "PRUSA-01",
  "priority": 5,
  "status": "InProgress",
  "notes": null,
  "startedAt": "2026-02-22T10:30:00Z",
  "completedAt": null,
  "createdAt": "2026-02-21T08:00:00Z",
  "updatedAt": "2026-02-22T10:30:00Z"
}
```

**Status Codes**:
- `200 OK` - Job retrieved successfully
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Job not found

---

### POST /api/jobs/{id}/queue

Queue a job with machine assignment.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | guid | Yes | Job ID |

**Request Body**:

```json
{
  "machineId": "PRUSA-01"
}
```

**Response**: `200 OK`

```json
{
  "jobId": "guid",
  "status": "Queued",
  "assignedMachineId": "PRUSA-01"
}
```

**Status Codes**:
- `200 OK` - Job queued successfully
- `400 Bad Request` - Missing machineId
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Job not found
- `409 Conflict` - Invalid transition (job not in Pending status)

**Valid Transitions**: `Pending → Queued`

---

### POST /api/jobs/{id}/start

Start a job.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | guid | Yes | Job ID |

**Request Body**: None

**Response**: `200 OK`

```json
{
  "jobId": "guid",
  "status": "InProgress",
  "startedAt": "2026-02-22T10:30:00Z"
}
```

**Side Effects**:
- Sets `StartedAt` timestamp
- Publishes `JobStartedEvent` to message broker

**Status Codes**:
- `200 OK` - Job started successfully
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Job not found
- `409 Conflict` - Invalid transition (job not in Pending or Queued status)

**Valid Transitions**: `Pending → InProgress`, `Queued → InProgress`

---

### POST /api/jobs/{id}/finish

Mark a job as finishing (post-processing).

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | guid | Yes | Job ID |

**Request Body**: None

**Response**: `200 OK`

```json
{
  "jobId": "guid",
  "status": "Finishing"
}
```

**Status Codes**:
- `200 OK` - Job marked as finishing
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Job not found
- `409 Conflict` - Invalid transition (job not in InProgress status)

**Valid Transitions**: `InProgress → Finishing`

---

### POST /api/jobs/{id}/complete

Mark a job as completed.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | guid | Yes | Job ID |

**Request Body**: None

**Response**: `200 OK`

```json
{
  "jobId": "guid",
  "status": "Completed",
  "completedAt": "2026-02-22T14:00:00Z"
}
```

**Side Effects**:
- Sets `CompletedAt` timestamp

**Status Codes**:
- `200 OK` - Job completed successfully
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Job not found
- `409 Conflict` - Invalid transition (job not in Finishing status)

**Valid Transitions**: `Finishing → Completed`

---

### POST /api/jobs/{id}/cancel

Cancel a job.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | guid | Yes | Job ID |

**Request Body**:

```json
{
  "reason": "Customer requested cancellation"
}
```

**Response**: `200 OK`

```json
{
  "jobId": "guid",
  "status": "Cancelled",
  "notes": "Customer requested cancellation"
}
```

**Side Effects**:
- Stores cancellation reason in `Notes` field

**Status Codes**:
- `200 OK` - Job cancelled successfully
- `400 Bad Request` - Missing reason
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Job not found

**Valid Transitions**: `Any → Cancelled`

---

### PATCH /api/jobs/{id}/reassign

Reassign a queued job to a different machine.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | guid | Yes | Job ID |

**Request Body**:

```json
{
  "machineId": "PRUSA-02"
}
```

**Response**: `200 OK`

```json
{
  "jobId": "guid",
  "status": "Queued",
  "assignedMachineId": "PRUSA-02"
}
```

**Status Codes**:
- `200 OK` - Job reassigned successfully
- `400 Bad Request` - Missing machineId
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Job not found
- `409 Conflict` - Invalid operation (job not in Queued status)

**Valid Transitions**: `Queued → Queued` (machine change only)

## Error Response Format

All error responses follow this format:

```json
{
  "error": {
    "code": "INVALID_TRANSITION",
    "message": "Cannot start a job that is already completed",
    "details": {
      "currentStatus": "Completed",
      "attemptedAction": "start"
    }
  }
}
```

## Status Codes Summary

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request (validation error) |
| 401 | Unauthorized (missing/invalid token) |
| 404 | Not Found |
| 409 | Conflict (invalid state transition) |
| 500 | Internal Server Error |
