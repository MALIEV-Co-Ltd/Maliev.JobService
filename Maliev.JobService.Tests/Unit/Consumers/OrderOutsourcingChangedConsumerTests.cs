using MassTransit;
using Maliev.JobService.Api.Consumers;
using Maliev.JobService.Application.Abstractions;
using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Contracts.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.JobService.Tests.Unit.Consumers;

public sealed class OrderOutsourcingChangedConsumerTests
{
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<ILogger<OrderOutsourcingChangedConsumer>> _loggerMock = new();

    [Fact]
    public async Task Consume_OrderOutsourcingChangedEvent_UpdatesMatchingJobs()
    {
        var orderId = Guid.NewGuid();
        _jobServiceMock
            .Setup(s => s.UpdateOutsourcingStatusAsync(orderId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var consumer = new OrderOutsourcingChangedConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderOutsourcingChangedEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId, true));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.UpdateOutsourcingStatusAsync(orderId, true, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderOutsourcingChangedEvent_WithoutPayload_IgnoresEvent()
    {
        var consumer = new OrderOutsourcingChangedConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderOutsourcingChangedEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(Guid.NewGuid(), true) with
        {
            Payload = null!
        });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _jobServiceMock.Verify(
            s => s.UpdateOutsourcingStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_OrderOutsourcingChangedEvent_WhenServiceThrows_PropagatesException()
    {
        var orderId = Guid.NewGuid();
        _jobServiceMock
            .Setup(s => s.UpdateOutsourcingStatusAsync(orderId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var consumer = new OrderOutsourcingChangedConsumer(_jobServiceMock.Object, _loggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderOutsourcingChangedEvent>>();
        contextMock.Setup(c => c.Message).Returns(BuildEvent(orderId, false));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Consume(contextMock.Object));
    }

    private static OrderOutsourcingChangedEvent BuildEvent(Guid orderId, bool isOutsourced)
    {
        return new OrderOutsourcingChangedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(OrderOutsourcingChangedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "OrderService",
            ConsumedBy: ["JobService", "NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new OrderOutsourcingChangedEventPayload(
                OrderId: orderId,
                IsOutsourced: isOutsourced,
                SupplierName: isOutsourced ? "Trusted Supplier" : null,
                SupplierCostTHB: isOutsourced ? 1200.0 : null,
                ChangedBy: Guid.NewGuid(),
                ChangedAtUtc: DateTimeOffset.UtcNow));
    }
}
