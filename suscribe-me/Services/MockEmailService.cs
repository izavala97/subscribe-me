namespace suscribe_me.Services;

/// <summary>
/// Mock email service for local development.
/// Logs emails to console instead of sending them.
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendMagicLinkAsync(string email, string magicLink, bool isRegistration)
    {
        var action = isRegistration ? "REGISTRATION" : "LOGIN";
        
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("📧 MOCK EMAIL - {Action}", action);
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("To: {Email}", email);
        _logger.LogInformation("Subject: {Subject}", isRegistration ? "Complete your registration" : "Sign in to Subscribe-Me");
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("🔗 MAGIC LINK (click or copy to browser):");
        _logger.LogInformation("{MagicLink}", magicLink);
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        
        return Task.CompletedTask;
    }

    public Task SendNotificationAsync(string email, string subject, string htmlContent)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("📧 MOCK EMAIL - NOTIFICATION");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("To: {Email}", email);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        
        return Task.CompletedTask;
    }
}
