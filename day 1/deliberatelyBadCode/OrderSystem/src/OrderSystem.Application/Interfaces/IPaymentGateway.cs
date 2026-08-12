using OrderSystem.Application.Payments;

namespace OrderSystem.Application.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken);
}
