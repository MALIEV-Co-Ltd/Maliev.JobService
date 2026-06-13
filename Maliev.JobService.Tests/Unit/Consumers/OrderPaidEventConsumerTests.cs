using Maliev.JobService.Api.Consumers;
using Maliev.JobService.Application.Abstractions;
using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Contracts.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.JobService.Tests.Unit.Consumers;

/// <summary>
/// Unit tests for the OrderPaidEventConsumer — the MassTransit consumer that bridges
/// OrderService "Paid" events to job creation in JobService.
///
/// This consumer is the critical link in the event chain:
///   PaymentService → PaymentCompletedEvent
///     → OrderService (PaymentCompletedEventConsumer) → Accepted → Paid → OrderPaidEvent
///       → JobService (OrderPaidEventConsumer) → CreateJobsForPaidOrderAsync
/// </summary>
public sealed class OrderPaidEventConsumerTests
{
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<ILogger<OrderPaidEventConsumer>> _loggerMock = new();

    private static OrderPaidEvent BuildEvent(Guid orderId, string? orderNumber = null) =>
        new(
            MessageId: Guid.NewGuid(),
            MessageName: "OrderPaidEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "OrderService",
            ConsumedBy: new[] { "JobService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new OrderPaidEventPayload(
                OrderId: orderId,
                OrderNumber: orderNumber ?? $"ORD-TEST-{orderId:N}"[..16],
                PaymentId: Guid.NewGuid(),
                PaidAmount: 1_500.00,
                Currency: "THB",
                PaidAt: DateTimeOffset.UtcNow));

    /// <summary>
    /// When OrderPaidEvent arrives, the consumer delegates to
    /// IJobService.CreateJobsForPaidOrderAsync with the correct OrderId from the event payload.
    /// </summary>
    [Fact]
    public async Task Consume_OrderPaidEvent_CallsCreateJobsForPaidOrder_WithCorrectOrderId()
    {
        var orderId = Guid.NewGuid();
        var orderNumber = "ORD-2026-00123";
        _jobServiceMock
            .Setup(s => s.CreateJobsForPaidOrderAsync(orderId, orderNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var consumer = new OrderPaidEventConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderPaidEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId, orderNumber));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(orderId, orderNumber, CancellationToken.None),
            Times.Once);
    }

    /// <summary>
    /// When the job service creates jobs successfully, the consumer completes without error.
    /// The returned job count is logged (not surfaced to the caller).
    /// </summary>
    [Fact]
    public async Task Consume_OrderPaidEvent_WhenJobsCreated_CompletesSuccessfully()
    {
        var orderId = Guid.NewGuid();
        _jobServiceMock
            .Setup(s => s.CreateJobsForPaidOrderAsync(orderId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var consumer = new OrderPaidEventConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderPaidEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Should not throw
        await consumer.Consume(contextMock.Object);
    }

    /// <summary>
    /// When the job service throws, the exception propagates out of the consumer so
    /// MassTransit can apply its retry / dead-letter policy.
    /// </summary>
    [Fact]
    public async Task Consume_OrderPaidEvent_WhenServiceThrows_PropagatesException()
    {
        var orderId = Guid.NewGuid();
        _jobServiceMock
            .Setup(s => s.CreateJobsForPaidOrderAsync(orderId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OrderService client unavailable"));

        var consumer = new OrderPaidEventConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderPaidEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => consumer.Consume(contextMock.Object));
    }

    /// <summary>
    /// When jobs already exist for the order (idempotency guard in the service),
    /// the consumer still completes without error — zero is a valid return value.
    /// </summary>
    [Fact]
    public async Task Consume_OrderPaidEvent_WhenZeroJobsCreated_CompletesSuccessfully()
    {
        var orderId = Guid.NewGuid();
        _jobServiceMock
            .Setup(s => s.CreateJobsForPaidOrderAsync(orderId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // idempotency: jobs already existed

        var consumer = new OrderPaidEventConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderPaidEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Should not throw even when no jobs were created
        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(orderId, It.IsAny<string>(), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderPaidEvent_WithoutJobServiceRouting_IgnoresEvent()
    {
        var orderId = Guid.NewGuid();
        var consumer = new OrderPaidEventConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderPaidEvent>>();
        var message = BuildEvent(orderId) with
        {
            ConsumedBy = ["InvoiceService", "NotificationService"]
        };
        contextMock.Setup(c => c.Message).Returns(message);
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_OrderPaidEvent_WithoutPayload_IgnoresEvent()
    {
        var consumer = new OrderPaidEventConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderPaidEvent>>();
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
    }

    [Fact]
    public async Task Consume_OrderPaidEvent_WithoutRoutingList_IgnoresEvent()
    {
        var consumer = new OrderPaidEventConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderPaidEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(Guid.NewGuid()) with
        {
            ConsumedBy = null!
        });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.CreateJobsForPaidOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
