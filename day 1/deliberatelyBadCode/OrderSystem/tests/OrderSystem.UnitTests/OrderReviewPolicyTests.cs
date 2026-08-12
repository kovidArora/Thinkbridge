using OrderSystem.Application.Services;
using Xunit;

namespace OrderSystem.UnitTests;

public class OrderReviewPolicyTests
{
    [Fact]
    public void RequiresManualReview_FlagsAnyOversizedLine_NotJustTheFirst()
    {
        // Regression test for the original bug: it only ever inspected
        // request.Items[0], so an oversized quantity anywhere else in the
        // order (as here, on the second line) was silently ignored.
        // Against the original logic this scenario would NOT be flagged;
        // against this policy it must be.
        var lineQuantities = new[] { 1, 250, 1 };

        var requiresReview = OrderReviewPolicy.RequiresManualReview(
            itemCount: lineQuantities.Length,
            isVip: false,
            total: 100m,
            lineQuantities: lineQuantities);

        Assert.True(requiresReview);
    }
}
