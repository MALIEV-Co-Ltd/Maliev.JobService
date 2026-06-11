using MassTransit;
using Maliev.JobService.Application.Abstractions;
using Maliev.MessagingContracts.Contracts.Orders;

namespace Maliev.JobService.Api.Consumers;

/// <summary>
/// Consumes the OrderPaidEvent to create production jobs for the order items.
/// </summary>
public class OrderPaidEventConsumer : IConsumer<OrderPaidEvent>
{
    private readonly IJobService _jobService;
    private readonly ILogger<OrderPaidEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderPaidEventConsumer"/> class.
    /// </summary>
    /// <param name="jobService">The application job service.</param>
    /// <param name="logger">The logger.</param>
    public OrderPaidEventConsumer(
        IJobService jobService,
        ILogger<OrderPaidEventConsumer> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        var orderId = context.Message.Payload.OrderId;
        var orderNumber = context.Message.Payload.OrderNumber;

        _logger.LogInformation("Processing OrderPaidEvent for OrderId: {OrderId}, OrderNumber: {OrderNumber}", orderId, orderNumber);

        var createdCount = await _jobService.CreateJobsForPaidOrderAsync(orderId, orderNumber, context.CancellationToken);
        _logger.LogInformation("OrderPaidEvent processing completed for OrderId: {OrderId}, OrderNumber: {OrderNumber} (created jobs: {Count})", orderId, orderNumber, createdCount);
    }
}

