using OrderSystem.Domain.Enums;

namespace OrderSystem.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
    public OrderPriority Priority { get; set; }
    public int? DeliveryAddressId { get; set; }
    public string? Referrer { get; set; }
    public string? Metadata { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}
