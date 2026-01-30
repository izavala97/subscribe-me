using suscribe_me.Models;

namespace suscribe_me.Services;

/// <summary>
/// Mock payment service for local development without Stripe.
/// Simulates successful payments and logs actions.
/// </summary>
public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(ILogger<MockPaymentService> logger)
    {
        _logger = logger;
    }

    public Task<string> CreateSubscriptionCheckoutAsync(
        Guid subscriberId, 
        Guid creatorId, 
        string successUrl, 
        string cancelUrl)
    {
        var mockSessionId = $"mock_sub_{Guid.NewGuid():N}";
        
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("💳 MOCK PAYMENT - SUBSCRIPTION CHECKOUT");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("Subscriber ID: {SubscriberId}", subscriberId);
        _logger.LogInformation("Creator ID: {CreatorId}", creatorId);
        _logger.LogInformation("Session ID: {SessionId}", mockSessionId);
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("Redirecting to success URL (simulating successful payment)");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        
        // Return success URL directly to simulate successful payment
        return Task.FromResult(successUrl + $"?session_id={mockSessionId}");
    }

    public Task<string> CreatePostPurchaseCheckoutAsync(
        Guid userId, 
        Guid postId, 
        string successUrl, 
        string cancelUrl)
    {
        var mockSessionId = $"mock_post_{Guid.NewGuid():N}";
        
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("💳 MOCK PAYMENT - POST PURCHASE CHECKOUT");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("User ID: {UserId}", userId);
        _logger.LogInformation("Post ID: {PostId}", postId);
        _logger.LogInformation("Session ID: {SessionId}", mockSessionId);
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("Redirecting to success URL (simulating successful payment)");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        
        return Task.FromResult(successUrl + $"?session_id={mockSessionId}");
    }

    public Task CancelSubscriptionAsync(string stripeSubscriptionId)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("💳 MOCK PAYMENT - CANCEL SUBSCRIPTION");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("Subscription ID: {SubscriptionId}", stripeSubscriptionId);
        _logger.LogInformation("Status: Cancelled (mock)");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        
        return Task.CompletedTask;
    }

    public Task HandleWebhookAsync(string json, string signature)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("💳 MOCK PAYMENT - WEBHOOK RECEIVED");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("Payload length: {Length} bytes", json.Length);
        _logger.LogInformation("Signature: {Signature}", signature);
        _logger.LogInformation("Status: Ignored (mock mode)");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        
        return Task.CompletedTask;
    }

    public Task<string> GetOrCreateCustomerAsync(Guid userId, string email)
    {
        var mockCustomerId = $"cus_mock_{userId:N}";
        
        _logger.LogInformation("💳 MOCK: Created/retrieved customer {CustomerId} for {Email}", mockCustomerId, email);
        
        return Task.FromResult(mockCustomerId);
    }
}
