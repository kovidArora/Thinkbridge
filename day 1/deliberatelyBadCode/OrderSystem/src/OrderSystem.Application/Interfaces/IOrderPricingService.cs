using OrderSystem.Application.Pricing;
using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Interfaces;

public interface IOrderPricingService
{
    OrderPricing Calculate(IReadOnlyList<PricedLine> lines, bool isVip, Coupon? coupon);
}
