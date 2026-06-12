namespace Maliev.JobService.Domain.Models;

/// <summary>
/// Data transfer object for an order item received from the Order Service.
/// </summary>
public record OrderItemDto
{
    /// <summary>Gets the unique identifier of the order item.</summary>
    public required Guid OrderItemId { get; init; }
    /// <summary>Gets the originating project ID when the order item came from a project quotation.</summary>
    public Guid? SourceProjectId { get; init; }
    /// <summary>Gets the originating project part ID when the order item came from a project quotation.</summary>
    public Guid? SourceProjectPartId { get; init; }
    /// <summary>Gets the customer identifier associated with the order.</summary>
    public string? CustomerId { get; init; }
    /// <summary>Gets the customer display name snapshot when known.</summary>
    public string? CustomerName { get; init; }
    /// <summary>Gets the unique identifier of the material.</summary>
    public required Guid MaterialId { get; init; }
    /// <summary>Gets the locked material snapshot JSON from the order boundary.</summary>
    public string? MaterialSnapshotJson { get; init; }
    /// <summary>Gets the locked configuration snapshot JSON from the order boundary.</summary>
    public string? ConfigurationSnapshotJson { get; init; }
    /// <summary>Gets the manufacturing technology.</summary>
    public required string Technology { get; init; }
    /// <summary>Gets the part volume in cubic centimeters (per unit).</summary>
    public decimal VolumeCm3 { get; init; }
    /// <summary>Gets the ordered quantity. Multiplied against VolumeCm3 when estimating total print time.</summary>
    public int Quantity { get; init; } = 1;
    /// <summary>Gets the estimated print time in minutes (per unit, 0 means auto-estimate).</summary>
    public int EstimatedPrintTimeMinutes { get; init; }
    /// <summary>Gets the scheduled delivery date.</summary>
    public DateTime? DeliveryDate { get; init; }
}
