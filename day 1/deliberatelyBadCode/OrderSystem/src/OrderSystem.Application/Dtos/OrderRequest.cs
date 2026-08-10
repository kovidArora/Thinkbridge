using System.ComponentModel.DataAnnotations;
using OrderSystem.Domain.Enums;

namespace OrderSystem.Application.Dtos;

public class OrderRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid customer id is required.")]
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "Order must contain at least one item.")]
    [MinLength(1, ErrorMessage = "Order must contain at least one item.")]
    public List<OrderRequestItem> Items { get; set; } = new();

    public string? CouponCode { get; set; }

    public PaymentMethodType PaymentMethod { get; set; }

    public string? CardNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool Priority { get; set; }

    public int? DeliveryAddressId { get; set; }

    [MaxLength(200)]
    public string? Referrer { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    // Cross-field validation that the original code did deep inside the
    // action method. Putting it here means [ApiController] rejects an
    // invalid request with 400 + ModelState errors before any service or
    // repository code runs at all.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentMethod == PaymentMethodType.Card)
        {
            if (string.IsNullOrWhiteSpace(CardNumber))
            {
                yield return new ValidationResult(
                    "Card number is required for card payments.",
                    new[] { nameof(CardNumber) });
            }
            else if (CardNumber.Length < 12)
            {
                yield return new ValidationResult(
                    "Card number is invalid.",
                    new[] { nameof(CardNumber) });
            }
        }
    }
}
