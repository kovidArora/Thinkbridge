using OrderSystem.Application.Interfaces;
using OrderSystem.Application.Payments;

namespace OrderSystem.Infrastructure.Payments;

/// <summary>
/// Stand-in for a real payment processor integration (Stripe, Braintree,
/// Adyen, ...). A production implementation must never accept or forward a
/// raw card number like this one does for demo purposes — it should accept
/// a client-side tokenized payment method instead (PCI SAQ A scope).
/// </summary>
public class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardNumber))
        {
            throw new PaymentGatewayException("No card token supplied to payment gateway.");
        }

        return Task.FromResult(new PaymentResult(
            Approved: true,
            TransactionId: Guid.NewGuid().ToString("N"),
            FailureReason: null));
    }
}
