using Maliev.JobService.Domain.Models;

namespace Maliev.JobService.Domain.Clients;

/// <summary>
/// Client for interacting with the Order Service.
/// </summary>
public interface IOrderServiceClient
{
    /// <summary>
    /// Gets the items associated with a specific order.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of order items.</returns>
    Task<List<OrderItemDto>> GetOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the delivery date for a specific order.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The delivery date if available, otherwise null.</returns>
    Task<DateTime?> GetDeliveryDateAsync(Guid orderId, CancellationToken cancellationToken = default);
}
