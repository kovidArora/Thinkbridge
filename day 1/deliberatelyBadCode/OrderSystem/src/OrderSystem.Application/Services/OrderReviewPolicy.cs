namespace OrderSystem.Application.Services;

/// <summary>
/// Decides whether an order needs manual review. Pulled out of the
/// controller into its own pure, static, easily-unit-tested policy.
///
/// The original code's "large line quantity" rule only ever inspected
/// request.Items[0] (the first item), so an oversized quantity anywhere
/// else in the order was silently ignored. This version checks every
/// line.
/// </summary>
public static class OrderReviewPolicy
{
    private const int LargeOrderItemCountThreshold = 10;
    private const decimal VipReviewTotalThreshold = 20000m;
    private const int LargeLineQuantityThreshold = 100;

    public static bool RequiresManualReview(
        int itemCount,
        bool isVip,
        decimal total,
        IEnumerable<int> lineQuantities)
    {
        if (itemCount > LargeOrderItemCountThreshold)
        {
            return true;
        }

        if (isVip && total > VipReviewTotalThreshold)
        {
            return true;
        }

        if (lineQuantities.Any(quantity => quantity > LargeLineQuantityThreshold))
        {
            return true;
        }

        return false;
    }
}
