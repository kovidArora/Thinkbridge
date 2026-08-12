using System.Net;
using System.Net.Http.Json;
using OrderSystem.Application.Dtos;
using OrderSystem.Domain.Enums;
using Xunit;

namespace OrderSystem.IntegrationTests;

public class OrdersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_WithThreeOrMoreItems_ReturnsCreated()
    {
        // Regression test for the original bug:
        //   var lastItem = request.Items[request.Items.Count];
        // `Count` is one past the last valid index, so this threw
        // IndexOutOfRangeException for every order with 3+ items. The
        // controller's blanket catch then masked it as a generic 500
        // "An unexpected error occurred." Against the original code this
        // request would come back as 500; against the refactor it must
        // come back as 201 Created.
        var request = new OrderRequest
        {
            CustomerId = 1,
            PaymentMethod = PaymentMethodType.Cod,
            Items = new List<OrderRequestItem>
            {
                new() { ProductId = 1, Quantity = 1 },
                new() { ProductId = 2, Quantity = 1 },
                new() { ProductId = 3, Quantity = 1 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal(275m, order!.Subtotal); // 50 + 25 + 200
        Assert.Equal(3, order.Items.Count);
    }
}
