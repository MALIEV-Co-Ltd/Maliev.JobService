# Feature Specification: Shop Floor Job Tracking

**Feature Branch**: `001-shop-floor-job-tracking`  
**Created**: 2026-02-21  
**Status**: Draft  
**Input**: User description: "Shop Floor Job Tracking (Maliev.JobService) - A microservice that digitalizes shop floor production management"

## Clarifications

### Session 2026-02-22

- Q: What database/storage should JobService use? → A: PostgreSQL - relational database with strong consistency for status transitions
- Q: What message broker should MassTransit use as transport? → A: RabbitMQ - reliable message delivery with built-in retry and dead-letter capabilities
- Q: What is the retention policy for Completed/Cancelled jobs? → A: Retain indefinitely - historical data valuable for analytics and audit; Kanban queries filter for active jobs
- Q: What API style should JobService expose? → A: REST/HTTP - standard for microservices, implied by 409 Conflict responses in acceptance criteria
- Q: What observability approach should JobService use? → A: Native .NET structured logging + metrics (job counts, transition times) - no external logging libraries

### Session 2026-02-21

- Q: What authorization model should be used for job operations? → A: Single "Employee" role - any authenticated employee can perform all job operations
- Q: How should concurrent updates to the same job be handled? → A: Last write wins - the second update overwrites the first
- Q: Should Cancelled jobs appear in the Kanban view? → A: Include Cancelled as a separate column - visible alongside other states
- Q: Can a job be reassigned to a different machine after being queued? → A: Reassign allowed from Queued only - can change machine before work starts

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Job Auto-Creation from Paid Orders (Priority: P1)

As a production manager, when an order is paid, I need the system to automatically create production jobs for each line item so that work can begin without manual data entry.

**Why this priority**: This is the foundation of the entire system - without automatic job creation from paid orders, the rest of the workflow cannot function. Manual job creation would defeat the purpose of automation.

**Independent Test**: Can be fully tested by simulating an order payment event and verifying that jobs are created for each order item with correct data (material, technology, volume, estimated time).

**Acceptance Scenarios**:

1. **Given** an order has been paid with 3 line items (FDM, SLA, CNC), **When** the payment event is received, **Then** 3 jobs are created in Pending status, one for each line item
2. **Given** a paid order with items having a delivery date 5 days away, **When** jobs are created, **Then** each job has Priority = 5
3. **Given** a paid order with no delivery date specified, **When** jobs are created, **Then** each job has Priority = 999 (lowest urgency)
4. **Given** the order service is temporarily unavailable, **When** a payment event is received, **Then** the event is not acknowledged and will be retried

---

### User Story 2 - Job Status Transitions (Priority: P1)

As a machine operator, I need to update job status as work progresses so that the production team has visibility into current shop floor activity.

**Why this priority**: Status transitions are the core workflow mechanism. Without them, there's no way to track progress or communicate job state to the team.

**Independent Test**: Can be fully tested by creating a job and performing each status transition in sequence, verifying the job state updates correctly.

**Acceptance Scenarios**:

1. **Given** a job in Pending status, **When** I queue it with machine "PRUSA-01", **Then** the job moves to Queued status and AssignedMachineId is set
2. **Given** a job in Queued status, **When** I start it, **Then** the job moves to InProgress, StartedAt timestamp is set, and a JobStartedEvent is published
3. **Given** a job in InProgress status, **When** I mark it as finishing, **Then** the job moves to Finishing status
4. **Given** a job in Finishing status, **When** I complete it, **Then** the job moves to Completed status and CompletedAt timestamp is set
5. **Given** a job in any status, **When** I cancel it with a reason, **Then** the job moves to Cancelled status and the reason is stored in Notes
6. **Given** a job in Completed status, **When** I try to start it, **Then** the request returns 409 Conflict

---

### User Story 3 - Kanban Board Visualization (Priority: P2)

As a production manager, I need to see all jobs organized by status in a Kanban view so that I can quickly understand the current state of production.

**Why this priority**: While status tracking is essential, the Kanban view provides the visual management layer that replaces the physical whiteboard. It can be developed after the core status functionality.

**Independent Test**: Can be fully tested by creating jobs in various statuses and verifying they appear in the correct columns, sorted by priority.

**Acceptance Scenarios**:

1. **Given** jobs exist across all statuses, **When** I request the Kanban view, **Then** jobs are grouped by status (Pending, Queued, InProgress, Finishing, Completed, Cancelled)
2. **Given** the Kanban view is displayed, **When** multiple jobs exist in the same status, **Then** they are sorted by Priority ascending (most urgent first)
3. **Given** the Kanban view is displayed, **When** I view each job card, **Then** I can see jobId, orderId, technology, materialId, assignedMachineId, priority, estimatedPrintTimeMinutes, startedAt, and completedAt

---

### User Story 4 - Job List Search and Filtering (Priority: P3)

As a production coordinator, I need to search and filter jobs by status, technology, and machine so that I can find specific jobs or analyze production patterns.

**Why this priority**: Search functionality enhances usability but is not required for basic job tracking operations. The Kanban view provides sufficient visibility for MVP.

**Independent Test**: Can be fully tested by creating a variety of jobs and verifying that filtering by status, technology, and machine returns the correct subset.

**Acceptance Scenarios**:

1. **Given** jobs exist with various statuses, **When** I filter by status=InProgress, **Then** only jobs with InProgress status are returned
2. **Given** jobs exist with various technologies, **When** I filter by technology=Fdm, **Then** only FDM jobs are returned
3. **Given** jobs exist with various machines, **When** I filter by assignedMachineId=PRUSA-01, **Then** only jobs assigned to PRUSA-01 are returned
4. **Given** 50 jobs exist, **When** I request page 2 with pageSize=20, **Then** jobs 21-40 are returned

---

### Edge Cases

- What happens when an order item has negative or zero volume? The job is created with the provided value; validation is the responsibility of the originating OrderService.
- What happens when a job transition is attempted in the wrong order (e.g., complete a Pending job)? The system returns 409 Conflict and the job status remains unchanged.
- What happens when the same payment event is processed twice (duplicate message)? The system deduplicates by OrderId - if jobs already exist for the order, no new jobs are created.
- What happens when priority calculation results in a negative number (past due date)? Priority is clamped to minimum 0 (highest urgency).
- What happens when a job is cancelled after starting? The job moves to Cancelled status; any downstream systems that received JobStartedEvent are not notified of cancellation.
- What happens when two employees simultaneously update the same job? Last write wins - the second update overwrites the first.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST automatically create a Job for each order line item when an OrderPaidEvent is received
- **FR-002**: System MUST fetch order item details from OrderService HTTP API after receiving OrderPaidEvent
- **FR-003**: System MUST calculate Job priority as days remaining until delivery date (clamped to minimum 0, defaulting to 999 if no date)
- **FR-004**: System MUST retry OrderService API calls on failure without acknowledging the message
- **FR-004.1**: System MUST deduplicate OrderPaidEvents by OrderId to prevent creating duplicate jobs for the same order
- **FR-005**: System MUST allow jobs to transition from Pending or Queued to InProgress via the start endpoint
- **FR-006**: System MUST set StartedAt timestamp and publish JobStartedEvent when a job starts
- **FR-007**: System MUST allow jobs to transition from Pending to Queued via the queue endpoint
- **FR-008**: System MUST allow jobs to transition from InProgress to Finishing via the finish endpoint
- **FR-009**: System MUST allow jobs to transition from Finishing to Completed via the complete endpoint
- **FR-010**: System MUST set CompletedAt timestamp when a job is marked complete
- **FR-011**: System MUST allow jobs to transition from any status to Cancelled via the cancel endpoint
- **FR-012**: System MUST store cancellation reason in the Notes field
- **FR-013**: System MUST reject invalid status transitions with 409 Conflict response
- **FR-014**: System MUST require Employee authorization for all job management endpoints
- **FR-015**: System MUST provide a Kanban view grouping jobs by status
- **FR-016**: System MUST sort Kanban groups by Priority ascending (most urgent first)
- **FR-017**: System MUST provide a paginated job list with filtering by status, technology, and assignedMachineId
- **FR-018**: System MUST capture job details including: orderId, orderItemId, materialId, technology, volumeCm3, estimatedPrintTimeMinutes, assignedMachineId, priority, status, timestamps, and notes

### Key Entities

- **Job**: Represents a single production task derived from an order line item. Tracks the complete lifecycle from creation through completion or cancellation. Contains references to order and material systems, production parameters (technology, volume, estimated time), machine assignment, priority for scheduling, current status, and timestamps for progress tracking.

- **JobStatus**: The lifecycle state of a job. Valid states: Pending (created, awaiting assignment), Queued (assigned to machine, waiting), InProgress (actively being worked), Finishing (post-processing underway), Completed (ready for shipping), Cancelled (halted for any reason).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Jobs are created within 5 seconds of receiving an OrderPaidEvent under normal operating conditions
- **SC-002**: Status transitions complete in under 2 seconds for 95% of requests
- **SC-003**: Kanban board displays all current jobs with load time under 3 seconds for up to 500 jobs
- **SC-004**: 100% of valid status transitions result in correct state changes; 100% of invalid transitions return 409 Conflict
- **SC-005**: JobStartedEvent is published for 100% of jobs transitioning to InProgress status
- **SC-006**: Production team can determine current shop floor status within 30 seconds of viewing the Kanban board

### Assumptions

- Employee authorization is handled by an existing authentication/authorization system
- OrderService provides the documented HTTP API with order item details
- MaterialService and InventoryService exist and can receive events
- MassTransit messaging infrastructure with RabbitMQ transport is already configured and available
- The system will run as a microservice alongside other Maliev services
- Job IDs are globally unique (GUIDs)
- Machine IDs are string identifiers managed externally
- PostgreSQL is the primary data store, providing strong consistency for job status transitions and efficient filtering/sorting for Kanban queries
- Jobs are retained indefinitely; Completed and Cancelled jobs remain in the database for analytics and audit purposes
- The service exposes a REST/HTTP API for all job management operations
- Observability uses native .NET logging only (no Serilog or external libraries); metrics include job counts per status and average transition times
