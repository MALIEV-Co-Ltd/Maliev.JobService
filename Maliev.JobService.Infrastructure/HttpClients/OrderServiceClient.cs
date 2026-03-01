using System.Net;
using System.Text.Json;
using Maliev.JobService.Domain.Clients;
using Maliev.JobService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Maliev.JobService.Infrastructure.HttpClients;

/// <summary>
/// Implementation of the order service client.
/// </summary>
public class OrderServiceClient : IOrderServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderServiceClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public OrderServiceClient(HttpClient httpClient, ILogger<OrderServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<OrderItemDto>> GetOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/orders/{orderId}/items", cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Order {OrderId} not found, returning empty list", orderId);
            return [];
        }
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var items = JsonSerializer.Deserialize<List<OrderItemDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return items ?? [];
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetDeliveryDateAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var items = await GetOrderItemsAsync(orderId, cancellationToken);
        return items.FirstOrDefault()?.DeliveryDate;
    }
}
