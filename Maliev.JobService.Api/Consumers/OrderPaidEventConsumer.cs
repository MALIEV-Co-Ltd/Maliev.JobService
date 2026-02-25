using MassTransit;
using Maliev.JobService.Api.Clients;
using Maliev.JobService.Api.Metrics;
using Maliev.JobService.Data;
using Maliev.JobService.Data.Entities;
using Maliev.MessagingContracts.Contracts.Orders;
using Microsoft.EntityFrameworkCore;

namespace Maliev.JobService.Api.Consumers;

/// <summary>
/// Consumes the OrderPaidEvent to create production jobs for the order items.
/// </summary>
public class OrderPaidEventConsumer : IConsumer<OrderPaidEvent>
{
    private readonly JobDbContext _dbContext;
    private readonly IOrderServiceClient _orderServiceClient;
    private readonly JobMetrics _metrics;
    private readonly ILogger<OrderPaidEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderPaidEventConsumer"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="orderServiceClient">The order service client.</param>
    /// <param name="metrics">The job metrics.</param>
    /// <param name="logger">The logger.</param>
    public OrderPaidEventConsumer(
        JobDbContext dbContext,
        IOrderServiceClient orderServiceClient,
        JobMetrics metrics,
        ILogger<OrderPaidEventConsumer> logger)
    {
        _dbContext = dbContext;
        _orderServiceClient = orderServiceClient;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        var orderId = context.Message.Payload.OrderId;
        
        _logger.LogInformation("Processing OrderPaidEvent for OrderId: {OrderId}", orderId);
        
        var existingJobs = await _dbContext.Jobs
            .AnyAsync(j => j.OrderId == orderId, context.CancellationToken);
            
        if (existingJobs)
        {
            _logger.LogInformation("Jobs already exist for OrderId: {OrderId}, skipping creation", orderId);
            return;
        }
        
        var orderItems = await _orderServiceClient.GetOrderItemsAsync(orderId, context.CancellationToken);
        
        if (orderItems.Count == 0)
        {
            _logger.LogWarning("No items found for OrderId: {OrderId}", orderId);
            return;
        }
        
        var now = DateTime.UtcNow;
        var deliveryDate = orderItems.FirstOrDefault()?.DeliveryDate;
        
        foreach (var item in orderItems)
        {
            var priority = CalculatePriority(item.DeliveryDate);
            
            var job = new Job
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                OrderItemId = item.OrderItemId,
                MaterialId = item.MaterialId,
                Technology = item.Technology,
                VolumeCm3 = item.VolumeCm3,
                EstimatedPrintTimeMinutes = item.EstimatedPrintTimeMinutes,
                Priority = priority,
                Status = JobStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            
            _dbContext.Jobs.Add(job);
            
            _logger.LogInformation(
                "Created job {JobId} for OrderId: {OrderId}, OrderItemId: {OrderItemId}, Priority: {Priority}",
                job.Id, orderId, item.OrderItemId, priority);
        }
        
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        
        _metrics.RecordJobCreated(orderItems.Count);
        
        _logger.LogInformation(
            "Successfully created {Count} jobs for OrderId: {OrderId}",
            orderItems.Count, orderId);
    }
    
    private static int CalculatePriority(DateTime? deliveryDate)
    {
        if (!deliveryDate.HasValue)
            return 999;
            
        var daysRemaining = (deliveryDate.Value - DateTime.UtcNow).Days;
        return Math.Max(0, daysRemaining);
    }
}

