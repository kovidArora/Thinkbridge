using Microsoft.Extensions.Logging;
using OrderSystem.Application.Interfaces;
using OrderSystem.Domain.Entities;

namespace OrderSystem.Infrastructure.Notifications;

/// <summary>
/// Stand-in for a real email/alerting integration. Uses ILogger instead of
/// Console.WriteLine so output goes through the app's real logging
/// pipeline (structured, filterable, shippable to a log sink).
/// </summary>
public class LoggingNotificationService : IOrderNotificationService
{
    private readonly ILogger<LoggingNotificationService> _logger;

    public LoggingNotificationService(ILogger<LoggingNotificationService> logger) => _logger = logger;

    public Task SendOrderConfirmationAsync(Order order, Customer customer, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Sending order confirmation for {Reference} to {Email}", order.Reference, customer.Email);
        return Task.CompletedTask;
    }

    public Task SendReviewRequiredAsync(Order order, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Order {OrderId} ({Reference}) requires manual review", order.Id, order.Reference);
        return Task.CompletedTask;
    }
}
