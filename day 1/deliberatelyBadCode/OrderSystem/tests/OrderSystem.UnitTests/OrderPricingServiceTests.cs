using System.ComponentModel.DataAnnotations;
using OrderSystem.Application.Dtos;
using OrderSystem.Application.Pricing;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Enums;
using Xunit;

namespace OrderSystem.UnitTests;

public class OrderPricingServiceTests
{
    private readonly OrderPricingService _sut = new();

    [Fact]
    public void Calculate_AppliesVolumeDiscount_VipDiscount_AndTax()
    {
        var lines = new List<PricedLine> { new(ProductId: 1, Quantity: 1, UnitPrice: 12000m) };

        var pricing = _sut.Calculate(lines, isVip: true, coupon: null);

        // subtotal 12000 > 10000 -> 10% volume discount, +5% VIP = 15% of 12000 = 1800
        Assert.Equal(12000m, pricing.Subtotal);
        Assert.Equal(1800m, pricing.Discount);
        Assert.Equal((12000m - 1800m) * 0.18m, pricing.Tax);
        Assert.Equal(0m, pricing.Shipping); // subtotal >= 5000 -> free shipping
        Assert.Equal(pricing.Subtotal - pricing.Discount + pricing.Tax + pricing.Shipping, pricing.Total);
    }

    [Fact]
    public void Calculate_CapsCombinedDiscount_AtSubtotal()
    {
        var lines = new List<PricedLine> { new(ProductId: 1, Quantity: 1, UnitPrice: 100m) };
        var coupon = new Coupon
        {
            Code = "HUGE",
            IsActive = true,
            Type = CouponType.FixedAmount,
            Value = 500m, // bigger than the subtotal
            MinimumOrderAmount = 0m,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var pricing = _sut.Calculate(lines, isVip: false, coupon: coupon);

        Assert.Equal(100m, pricing.Discount); // capped at subtotal, never negative
        Assert.Equal(0m, pricing.Tax);        // nothing left to tax
        Assert.True(pricing.Total >= 0m);
    }

    [Fact]
    public void Validate_RejectsNegativeQuantity()
    {
        var item = new OrderRequestItem
        {
            ProductId = 1,
            Quantity = -1
        };

        var validationContext = new ValidationContext(item);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(item, validationContext, validationResults, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, result => result.ErrorMessage == "Quantity must be greater than zero.");
    }
}
