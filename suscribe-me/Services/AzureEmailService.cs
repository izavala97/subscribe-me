using Azure;
using Azure.Communication.Email;

namespace suscribe_me.Services;

/// <summary>
/// Azure Communication Services implementation for sending emails
/// </summary>
public class AzureEmailService : IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;
    private readonly ILogger<AzureEmailService> _logger;

    public AzureEmailService(IConfiguration configuration, ILogger<AzureEmailService> logger)
    {
        _logger = logger;
        var connectionString = configuration["Azure:CommunicationServices:ConnectionString"]
            ?? throw new InvalidOperationException("Azure Communication Services connection string not configured");
        _senderAddress = configuration["Azure:CommunicationServices:SenderAddress"]
            ?? throw new InvalidOperationException("Azure Communication Services sender address not configured");
        _emailClient = new EmailClient(connectionString);
    }

    public async Task SendMagicLinkAsync(string email, string magicLink, bool isRegistration)
    {
        var action = isRegistration ? "complete your registration" : "sign in";
        var subject = isRegistration ? "Complete your Subscribe-Me registration" : "Sign in to Subscribe-Me";
        
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #6366f1; color: white !important; text-decoration: none; border-radius: 6px; font-weight: 600; }}
        .footer {{ margin-top: 30px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>Welcome to Subscribe-Me!</h2>
        <p>Click the button below to {action}:</p>
        <p style='margin: 30px 0;'>
            <a href='{magicLink}' class='button'>
                {(isRegistration ? "Complete Registration" : "Sign In")}
            </a>
        </p>
        <p>Or copy and paste this link into your browser:</p>
        <p style='word-break: break-all; color: #6366f1;'>{magicLink}</p>
        <p class='footer'>
            This link expires in 15 minutes.<br>
            If you didn't request this email, you can safely ignore it.
        </p>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlContent);
    }

    public async Task SendNotificationAsync(string email, string subject, string htmlContent)
    {
        await SendEmailAsync(email, subject, htmlContent);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlContent)
    {
        try
        {
            var emailContent = new EmailContent(subject) { Html = htmlContent };
            var emailMessage = new EmailMessage(_senderAddress, to, emailContent);
            
            var operation = await _emailClient.SendAsync(WaitUntil.Started, emailMessage);
            _logger.LogInformation("Email sent to {Email}, operation ID: {OperationId}", to, operation.Id);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
        }
    }
}
