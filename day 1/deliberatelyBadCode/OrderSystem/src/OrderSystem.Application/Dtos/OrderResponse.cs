namespace OrderSystem.Application.Dtos;

public record OrderResponse(
    int Id,
    string Reference,
    int CustomerId,
    string CustomerName,
    string Status,
    string PaymentStatus,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Shipping,
    decimal Total,
    DateTime CreatedAt,
    string Priority,
    IReadOnlyList<OrderItemResponse> Items);

public record OrderItemResponse(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Total);
