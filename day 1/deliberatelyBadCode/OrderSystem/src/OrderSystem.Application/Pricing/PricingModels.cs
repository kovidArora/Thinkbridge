namespace OrderSystem.Application.Pricing;

public record PricedLine(int ProductId, int Quantity, decimal UnitPrice);

public record OrderPricing(decimal Subtotal, decimal Discount, decimal Tax, decimal Shipping, decimal Total);
