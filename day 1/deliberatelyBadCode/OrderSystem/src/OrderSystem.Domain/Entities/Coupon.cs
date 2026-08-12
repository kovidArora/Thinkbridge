using OrderSystem.Domain.Enums;

namespace OrderSystem.Domain.Entities;

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime ExpiresAt { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public CouponType Type { get; set; }
    public decimal Value { get; set; }
    public int UsageCount { get; set; }
}
