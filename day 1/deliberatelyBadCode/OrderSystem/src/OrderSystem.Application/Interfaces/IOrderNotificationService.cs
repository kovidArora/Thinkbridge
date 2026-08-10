using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Interfaces;

public interface IOrderNotificationService
{
    Task SendOrderConfirmationAsync(Order order, Customer customer, CancellationToken cancellationToken);
    Task SendReviewRequiredAsync(Order order, CancellationToken cancellationToken);
}
