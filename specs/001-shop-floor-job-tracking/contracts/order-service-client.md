# OrderService Client Contract

**Service**: Maliev.OrderService
**Consumer**: Maliev.JobService
**Purpose**: Fetch order item details when OrderPaidEvent is received

## Interface Definition

```csharp
public interface IOrderServiceClient
{
    Task<List<OrderItemDto>> GetOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<DateTime?> GetDeliveryDateAsync(Guid orderId, CancellationToken cancellationToken = default);
}
```

## HTTP Endpoint

### GET /api/orders/{orderId}/items

Returns all line items for a specific order.

**Base URL**: Configured via `OrderService:BaseUrl` in appsettings.json

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| orderId | guid | Yes | Order identifier |

**Headers**:

| Header | Value | Required |
|--------|-------|----------|
| Accept | application/json | Yes |
| Authorization | Bearer {token} | If required |

**Response**: `200 OK`

```json
[
  {
    "orderItemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
    "technology": "FDM",
    "volumeCm3": 150.5,
    "estimatedPrintTimeMinutes": 120,
    "deliveryDate": "2026-02-27T00:00:00Z"
  },
  {
    "orderItemId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
    "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa9",
    "technology": "SLA",
    "volumeCm3": 75.25,
    "estimatedPrintTimeMinutes": 90,
    "deliveryDate": "2026-02-27T00:00:00Z"
  }
]
```

**Status Codes**:

| Code | Handling |
|------|----------|
| 200 | Return deserialized list |
| 404 | Return empty list (order may not have manufacturable items) |
| Other | Throw exception (trigger MassTransit retry) |

## DTO Definition

### OrderItemDto

```csharp
public record OrderItemDto
{
    public Guid OrderItemId { get; init; }
    public Guid MaterialId { get; init; }
    public string Technology { get; init; } = string.Empty;
    public decimal VolumeCm3 { get; init; }
    public int EstimatedPrintTimeMinutes { get; init; }
    public DateTime? DeliveryDate { get; init; }
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| OrderItemId | Guid | No | Unique identifier for the order line item |
| MaterialId | Guid | No | Reference to material in MaterialService |
| Technology | string | No | Manufacturing technology (FDM, SLA, CNC, etc.) |
| VolumeCm3 | decimal | No | Volume in cubic centimeters (may be 0 or negative) |
| EstimatedPrintTimeMinutes | int | No | Estimated production time in minutes |
| DeliveryDate | DateTime? | Yes | Customer's requested delivery date (used for priority) |

## Implementation Requirements

### HTTP Client Configuration

```csharp
services.AddHttpClient<IOrderServiceClient, OrderServiceClient>(client =>
{
    client.BaseAddress = new Uri(configuration["OrderService:BaseUrl"]!);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddTransientHttpErrorPolicy(p => 
    p.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
```

### Error Handling

1. **404 Not Found**: Return empty list
   - Rationale: Some orders may not have manufacturable items (e.g., pure design orders)
   - The consumer should still acknowledge the message

2. **Transient Errors (5xx, timeout)**: Throw exception
   - Rationale: MassTransit will retry based on configured retry policy
   - Message remains unacknowledged

3. **Deserialization Errors**: Throw exception
   - Rationale: Schema mismatch indicates contract version issue
   - Requires investigation

### Retry Policy

Configure Polly retry policy for transient failures:
- 3 retries
- Exponential backoff: 2s, 4s, 8s

This aligns with FR-004: "System MUST retry OrderService API calls on failure without acknowledging the message"

## Usage Example

```csharp
public class OrderPaidEventConsumer : IConsumer<OrderPaidEvent>
{
    private readonly IOrderServiceClient _orderServiceClient;
    private readonly JobDbContext _dbContext;
    
    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        var items = await _orderServiceClient.GetOrderItemsAsync(
            context.Message.OrderId, 
            context.CancellationToken);
            
        if (items.Count == 0)
        {
            _logger.LogWarning("No items found for order {OrderId}", 
                context.Message.OrderId);
            return; // Acknowledge message
        }
        
        // Deduplication check
        var orderExists = await _dbContext.Jobs
            .AnyAsync(j => j.OrderId == context.Message.OrderId);
            
        if (orderExists)
        {
            _logger.LogInformation("Jobs already exist for order {OrderId}", 
                context.Message.OrderId);
            return; // Idempotent - acknowledge
        }
        
        // Create jobs for each item...
    }
}
```

## Contract Versioning

If OrderService introduces breaking changes:
1. Coordinate deployment with OrderService team
2. Update DTO to match new schema
3. Consider versioned endpoint (e.g., `/api/v2/orders/{id}/items`)
