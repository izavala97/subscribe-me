namespace suscribe_me.Services;

/// <summary>
/// Service for handling payments and subscriptions via Stripe
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Create a Stripe Checkout session for subscribing to a creator
    /// </summary>
    Task<string> CreateSubscriptionCheckoutAsync(Guid subscriberId, Guid creatorId, string successUrl, string cancelUrl);
    
    /// <summary>
    /// Create a Stripe Checkout session for purchasing a single post
    /// </summary>
    Task<string> CreatePostPurchaseCheckoutAsync(Guid userId, Guid postId, string successUrl, string cancelUrl);
    
    /// <summary>
    /// Cancel an active subscription
    /// </summary>
    Task CancelSubscriptionAsync(string stripeSubscriptionId);
    
    /// <summary>
    /// Handle incoming Stripe webhook events
    /// </summary>
    Task HandleWebhookAsync(string json, string signature);
    
    /// <summary>
    /// Get or create a Stripe Customer for a user
    /// </summary>
    Task<string> GetOrCreateCustomerAsync(Guid userId, string email);
}
