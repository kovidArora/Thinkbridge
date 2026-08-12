using OrderSystem.Application.Interfaces;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Enums;

namespace OrderSystem.Application.Pricing;

/// <summary>
/// Pure pricing calculation, no I/O. The original controller computed
/// discount/tax twice (once before the coupon block, once after) — here
/// it's computed exactly once, and the whole thing is unit-testable
/// without a database.
/// </summary>
public class OrderPricingService : IOrderPricingService
{
    private const decimal HighVolumeThreshold = 10000m;
    private const decimal MidVolumeThreshold = 5000m;
    private const decimal HighVolumeDiscountRate = 0.10m;
    private const decimal MidVolumeDiscountRate = 0.05m;
    private const decimal VipDiscountRate = 0.05m;
    private const decimal TaxRate = 0.18m;
    private const decimal FlatShippingFee = 100m;

    public OrderPricing Calculate(IReadOnlyList<PricedLine> lines, bool isVip, Coupon? coupon)
    {
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);

        var discount = subtotal switch
        {
            > HighVolumeThreshold => subtotal * HighVolumeDiscountRate,
            > MidVolumeThreshold => subtotal * MidVolumeDiscountRate,
            _ => 0m
        };

        if (isVip)
        {
            discount += subtotal * VipDiscountRate;
        }

        if (coupon is not null)
        {
            discount += coupon.Type == CouponType.Percentage
                ? subtotal * coupon.Value / 100m
                : coupon.Value;
        }

        // A stacked discount (volume + VIP + coupon) should never make the
        // order worth less than zero.
        if (discount > subtotal)
        {
            discount = subtotal;
        }

        var discountedSubtotal = subtotal - discount;
        var tax = discountedSubtotal * TaxRate;
        var shipping = subtotal >= MidVolumeThreshold ? 0m : FlatShippingFee;
        var total = discountedSubtotal + tax + shipping;

        return new OrderPricing(subtotal, discount, tax, shipping, total);
    }
}
