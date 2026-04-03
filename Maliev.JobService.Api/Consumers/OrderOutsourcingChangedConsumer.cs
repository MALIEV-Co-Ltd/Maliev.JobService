using MassTransit;
using Maliev.JobService.Application.Abstractions;
using Maliev.MessagingContracts.Contracts.Orders;

namespace Maliev.JobService.Api.Consumers;

/// <summary>
/// Consumes the OrderOutsourcingChangedEvent to update the IsOutsourced flag on all jobs for the order.
/// </summary>
public class OrderOutsourcingChangedConsumer : IConsumer<OrderOutsourcingChangedEvent>
{
    private readonly IJobService _jobService;
    private readonly ILogger<OrderOutsourcingChangedConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderOutsourcingChangedConsumer"/> class.
    /// </summary>
    /// <param name="jobService">The application job service.</param>
    /// <param name="logger">The logger.</param>
    public OrderOutsourcingChangedConsumer(
        IJobService jobService,
        ILogger<OrderOutsourcingChangedConsumer> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderOutsourcingChangedEvent> context)
    {
        var orderId = context.Message.Payload.OrderId;
        var isOutsourced = context.Message.Payload.IsOutsourced;

        _logger.LogInformation(
            "Processing OrderOutsourcingChangedEvent for OrderId: {OrderId}, IsOutsourced: {IsOutsourced}",
            orderId,
            isOutsourced);

        var updatedCount = await _jobService.UpdateOutsourcingStatusAsync(orderId, isOutsourced, context.CancellationToken);

        _logger.LogInformation(
            "OrderOutsourcingChangedEvent processing completed for OrderId: {OrderId} (updated jobs: {Count})",
            orderId,
            updatedCount);
    }
}
