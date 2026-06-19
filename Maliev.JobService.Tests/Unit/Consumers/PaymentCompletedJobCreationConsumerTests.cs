using Maliev.JobService.Api.Consumers;
using Maliev.JobService.Application.Abstractions;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.JobService.Tests.Unit.Consumers;

/// <summary>
/// Unit tests for the PaymentCompletedJobCreationConsumer production-job trigger.
/// </summary>
public sealed class PaymentCompletedJobCreationConsumerTests
{
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<ILogger<PaymentCompletedJobCreationConsumer>> _loggerMock = new();

    private static PaymentCompletedEvent BuildEvent(Guid orderId, string? orderNumber = null) =>
        new(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentCompletedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "PaymentService",
            ConsumedBy: ["OrderService", "ProjectService", "JobService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCompletedEventPayload(
                OrderId: orderId,
                OrderNumber: orderNumber ?? $"ORD-TEST-{orderId:N}"[..16],
                CustomerId: Guid.NewGuid().ToString("D"),
                PaymentId: Guid.NewGuid(),
                Amount: 1_500.00,
                Currency: "THB"));

    /// <summary>
    /// Routed payment completion events create missing production jobs using the order number lookup.
    /// </summary>
    [Fact]
    public async Task Consume_PaymentCompletedEvent_CallsCreateJobsForPaidOrder_WithOrderNumber()
    {
        var orderId = Guid.NewGuid();
        var orderNumber = "ORD-2026-00123";
        _jobServiceMock
            .Setup(s => s.CreateJobsForPaidOrderAsync(orderId, orderNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var consumer = new PaymentCompletedJobCreationConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId, orderNumber));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(orderId, orderNumber, CancellationToken.None),
            Times.Once);
    }

    /// <summary>
    /// Missing order numbers fall back to the deterministic order identifier lookup.
    /// </summary>
    [Fact]
    public async Task Consume_PaymentCompletedEvent_WithoutOrderNumber_FallsBackToOrderIdLookup()
    {
        var orderId = Guid.NewGuid();
        _jobServiceMock
            .Setup(s => s.CreateJobsForPaidOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var consumer = new PaymentCompletedJobCreationConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId, string.Empty));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(orderId, CancellationToken.None),
            Times.Once);
        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Payment completion events delivered to the JobService queue are processed even when routing metadata is stale.
    /// </summary>
    [Fact]
    public async Task Consume_PaymentCompletedEvent_WithoutJobServiceRouting_StillCreatesJobs()
    {
        var orderId = Guid.NewGuid();
        var consumer = new PaymentCompletedJobCreationConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId) with
        {
            ConsumedBy = ["OrderService", "ProjectService"]
        });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(orderId, It.IsAny<string>(), CancellationToken.None),
            Times.Once);
    }

    /// <summary>
    /// Null payloads are ignored and do not create jobs.
    /// </summary>
    [Fact]
    public async Task Consume_PaymentCompletedEvent_WithoutPayload_IgnoresEvent()
    {
        var consumer = new PaymentCompletedJobCreationConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(Guid.NewGuid()) with
        {
            Payload = null!
        });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
