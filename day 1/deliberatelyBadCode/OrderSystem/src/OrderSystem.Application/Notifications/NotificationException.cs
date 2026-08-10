namespace OrderSystem.Application.Notifications;

/// <summary>
/// Thrown by IOrderNotificationService implementations when a best-effort
/// notification (confirmation email, review alert) fails. Deliberately a
/// distinct type so OrderService can catch exactly this and nothing else.
/// </summary>
public class NotificationException : Exception
{
    public NotificationException(string message) : base(message) { }
    public NotificationException(string message, Exception inner) : base(message, inner) { }
}
