using System.Net;
using Maliev.JobService.Infrastructure.HttpClients;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public sealed class OrderServiceClientTests
{
    [Fact]
    public async Task GetOrderItemsAsync_WhenResponseContainsSnapshots_DeserializesSnapshotFields()
    {
        const string materialSnapshotJson = "{\"materialId\":1,\"materialName\":\"PA12 Nylon\"}";
        const string configurationSnapshotJson = "{\"orderedQuantity\":2,\"serviceCategoryId\":1}";
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                [
                  {
                    "orderItemId": "11111111-1111-1111-1111-111111111111",
                    "materialId": "22222222-2222-2222-2222-222222222222",
                    "materialSnapshotJson": "{{materialSnapshotJson.Replace("\"", "\\\"")}}",
                    "configurationSnapshotJson": "{{configurationSnapshotJson.Replace("\"", "\\\"")}}",
                    "technology": "SLS",
                    "volumeCm3": 12.5,
                    "quantity": 2,
                    "estimatedPrintTimeMinutes": 30
                  }
                ]
                """)
        });
        var client = new OrderServiceClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://orders.test") },
            NullLogger<OrderServiceClient>.Instance);

        var items = await client.GetOrderItemsAsync("ORD-2026-00123");

        var item = Assert.Single(items);
        Assert.Equal(materialSnapshotJson, item.MaterialSnapshotJson);
        Assert.Equal(configurationSnapshotJson, item.ConfigurationSnapshotJson);
        Assert.Equal("/order/v1/orders/ORD-2026-00123/items", handler.RequestUri!.AbsolutePath);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(_response);
        }
    }
}
