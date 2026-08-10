namespace OrderSystem.Domain.Enums;

public enum OrderStatus
{
    Pending,
    RequiresReview,
    Cancelled,
    Completed
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Declined
}

public enum OrderPriority
{
    Normal,
    High
}

public enum PaymentMethodType
{
    Card,
    Cod
}

public enum CouponType
{
    Percentage,
    FixedAmount
}
