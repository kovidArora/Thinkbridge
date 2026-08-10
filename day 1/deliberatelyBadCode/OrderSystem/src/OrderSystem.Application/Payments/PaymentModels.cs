namespace OrderSystem.Application.Payments;

public record PaymentRequest(string CardNumber, decimal Amount);

public record PaymentResult(bool Approved, string? TransactionId, string? FailureReason);

/// <summary>
/// Thrown for genuine payment-gateway faults (timeouts, unreachable
/// service, malformed request) — NOT for an ordinary decline. A decline is
/// an expected business outcome and is represented by
/// PaymentResult.Approved == false, not an exception.
/// </summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message) : base(message) { }
    public PaymentGatewayException(string message, Exception inner) : base(message, inner) { }
}
