using Maliev.JobService.Application.Abstractions;
using Maliev.MessagingContracts.Contracts.Payments;
using MassTransit;

namespace Maliev.JobService.Api.Consumers;

/// <summary>
/// Consumes completed payment events to idempotently create production jobs for paid order items.
/// </summary>
public class PaymentCompletedJobCreationConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly IJobService _jobService;
    private readonly ILogger<PaymentCompletedJobCreationConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentCompletedJobCreationConsumer"/> class.
    /// </summary>
    /// <param name="jobService">The application job service.</param>
    /// <param name="logger">The logger.</param>
    public PaymentCompletedJobCreationConsumer(
        IJobService jobService,
        ILogger<PaymentCompletedJobCreationConsumer> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var payload = context.Message.Payload;
        if (payload is null)
        {
            _logger.LogWarning("PaymentCompletedEvent received without payload; skipping job creation");
            return;
        }

        if (string.IsNullOrWhiteSpace(payload.OrderNumber))
        {
            _logger.LogWarning(
                "PaymentCompletedEvent for OrderId: {OrderId} is missing orderNumber; falling back to OrderId lookup",
                payload.OrderId);

            var fallbackCreatedCount = await _jobService.CreateJobsForPaidOrderAsync(payload.OrderId, context.CancellationToken);
            _logger.LogInformation(
                "PaymentCompletedEvent job creation completed for OrderId: {OrderId} using fallback lookup (created jobs: {Count})",
                payload.OrderId,
                fallbackCreatedCount);
            return;
        }

        _logger.LogInformation(
            "Processing PaymentCompletedEvent job creation for OrderId: {OrderId}, OrderNumber: {OrderNumber}",
            payload.OrderId,
            payload.OrderNumber);

        var createdCount = await _jobService.CreateJobsForPaidOrderAsync(
            payload.OrderId,
            payload.OrderNumber,
            context.CancellationToken);
        _logger.LogInformation(
            "PaymentCompletedEvent job creation completed for OrderId: {OrderId}, OrderNumber: {OrderNumber} (created jobs: {Count})",
            payload.OrderId,
            payload.OrderNumber,
            createdCount);
    }

}
