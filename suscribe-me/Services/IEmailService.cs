namespace suscribe_me.Services;

/// <summary>
/// Service for sending emails (magic links, notifications, etc.)
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a magic link email for authentication
    /// </summary>
    Task SendMagicLinkAsync(string email, string magicLink, bool isRegistration);
    
    /// <summary>
    /// Send a notification email
    /// </summary>
    Task SendNotificationAsync(string email, string subject, string htmlContent);
}
