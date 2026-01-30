using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using Stripe;
using Stripe.Checkout;

namespace suscribe_me.Services;

/// <summary>
/// Stripe implementation for payment processing.
/// Uses Stripe Test Mode for development.
/// </summary>
public class StripePaymentService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StripeClient _stripeClient;

    public StripePaymentService(
        IConfiguration configuration,
        ILogger<StripePaymentService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
        
        var secretKey = configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey not configured");
        _stripeClient = new StripeClient(secretKey);
    }

    public async Task<string> CreateSubscriptionCheckoutAsync(
        Guid subscriberId, 
        Guid creatorId, 
        string successUrl, 
        string cancelUrl)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var subscriber = await db.Users.FindAsync(subscriberId)
            ?? throw new InvalidOperationException("Subscriber not found");
        var creator = await db.Users.FindAsync(creatorId)
            ?? throw new InvalidOperationException("Creator not found");
        
        if (creator.MonthlyPriceCents <= 0)
            throw new InvalidOperationException("Creator has no subscription price set");
        
        // Get or create Stripe customer
        var customerId = await GetOrCreateCustomerAsync(subscriberId, subscriber.Email);
        
        // Create a price for this creator's subscription
        var priceOptions = new PriceCreateOptions
        {
            UnitAmount = creator.MonthlyPriceCents,
            Currency = "usd",
            Recurring = new PriceRecurringOptions { Interval = "month" },
            ProductData = new PriceProductDataOptions
            {
                Name = $"Subscription to {creator.DisplayName ?? creator.Username}",
                Metadata = new Dictionary<string, string>
                {
                    { "creatorId", creatorId.ToString() }
                }
            }
        };
        
        var price = await _stripeClient.V1.Prices.CreateAsync(priceOptions);
        
        // Create checkout session
        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = customerId,
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = price.Id, Quantity = 1 }
            },
            Metadata = new Dictionary<string, string>
            {
                { "type", "subscription" },
                { "subscriberId", subscriberId.ToString() },
                { "creatorId", creatorId.ToString() }
            }
        };
        
        var session = await _stripeClient.V1.Checkout.Sessions.CreateAsync(sessionOptions);
        
        _logger.LogInformation(
            "Created subscription checkout session {SessionId} for subscriber {SubscriberId} to creator {CreatorId}",
            session.Id, subscriberId, creatorId);
        
        return session.Url!;
    }

    public async Task<string> CreatePostPurchaseCheckoutAsync(
        Guid userId, 
        Guid postId, 
        string successUrl, 
        string cancelUrl)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var user = await db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found");
        var post = await db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == postId)
            ?? throw new InvalidOperationException("Post not found");
        
        if (post.Visibility != PostVisibility.SinglePurchase || post.PriceCents <= 0)
            throw new InvalidOperationException("Post is not available for purchase");
        
        var customerId = await GetOrCreateCustomerAsync(userId, user.Email);
        
        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            Customer = customerId,
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = post.PriceCents,
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = post.Title,
                            Description = $"Access to post by {post.Author.DisplayName ?? post.Author.Username}"
                        }
                    },
                    Quantity = 1
                }
            },
            Metadata = new Dictionary<string, string>
            {
                { "type", "post_purchase" },
                { "userId", userId.ToString() },
                { "postId", postId.ToString() }
            }
        };
        
        var session = await _stripeClient.V1.Checkout.Sessions.CreateAsync(sessionOptions);
        
        _logger.LogInformation(
            "Created post purchase checkout session {SessionId} for user {UserId}, post {PostId}",
            session.Id, userId, postId);
        
        return session.Url!;
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId)
    {
        await _stripeClient.V1.Subscriptions.CancelAsync(stripeSubscriptionId);
        _logger.LogInformation("Cancelled subscription {SubscriptionId}", stripeSubscriptionId);
    }

    public async Task HandleWebhookAsync(string json, string signature)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret not configured");
        
        var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        
        _logger.LogInformation("Received Stripe webhook: {EventType}", stripeEvent.Type);
        
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompletedAsync(stripeEvent.Data.Object as Session);
                break;
                
            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(stripeEvent.Data.Object as Stripe.Subscription);
                break;
                
            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent.Data.Object as Stripe.Subscription);
                break;
                
            case "invoice.payment_failed":
                await HandlePaymentFailedAsync(stripeEvent.Data.Object as Invoice);
                break;
        }
    }

    public async Task<string> GetOrCreateCustomerAsync(Guid userId, string email)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var user = await db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found");
        
        if (!string.IsNullOrEmpty(user.StripeCustomerId))
            return user.StripeCustomerId;
        
        var customerOptions = new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() }
            }
        };
        
        var customer = await _stripeClient.V1.Customers.CreateAsync(customerOptions);
        
        user.StripeCustomerId = customer.Id;
        await db.SaveChangesAsync();
        
        _logger.LogInformation("Created Stripe customer {CustomerId} for user {UserId}", customer.Id, userId);
        
        return customer.Id;
    }

    private async Task HandleCheckoutCompletedAsync(Session? session)
    {
        if (session == null) return;
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var type = session.Metadata.GetValueOrDefault("type");
        
        if (type == "subscription")
        {
            var subscriberId = Guid.Parse(session.Metadata["subscriberId"]);
            var creatorId = Guid.Parse(session.Metadata["creatorId"]);
            
            var creator = await db.Users.FindAsync(creatorId);
            
            var subscription = new Models.Subscription
            {
                SubscriberId = subscriberId,
                CreatorId = creatorId,
                StripeSubscriptionId = session.SubscriptionId,
                Status = SubscriptionStatus.Active,
                AmountCents = creator?.MonthlyPriceCents ?? 0,
                StartedAt = DateTime.UtcNow
            };
            
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
            
            _logger.LogInformation(
                "Created subscription for {SubscriberId} to {CreatorId}",
                subscriberId, creatorId);
        }
        else if (type == "post_purchase")
        {
            var userId = Guid.Parse(session.Metadata["userId"]);
            var postId = Guid.Parse(session.Metadata["postId"]);
            
            var post = await db.Posts.FindAsync(postId);
            
            var purchase = new PostPurchase
            {
                UserId = userId,
                PostId = postId,
                AmountCents = post?.PriceCents ?? 0,
                StripePaymentIntentId = session.PaymentIntentId
            };
            
            db.PostPurchases.Add(purchase);
            await db.SaveChangesAsync();
            
            _logger.LogInformation("Created post purchase for user {UserId}, post {PostId}", userId, postId);
        }
    }

    private async Task HandleSubscriptionUpdatedAsync(Stripe.Subscription? subscription)
    {
        if (subscription == null) return;
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dbSubscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id);
        
        if (dbSubscription != null)
        {
            dbSubscription.Status = subscription.Status switch
            {
                "active" => SubscriptionStatus.Active,
                "past_due" => SubscriptionStatus.PastDue,
                "canceled" => SubscriptionStatus.Cancelled,
                _ => dbSubscription.Status
            };
            
            dbSubscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd;
            
            await db.SaveChangesAsync();
            
            _logger.LogInformation(
                "Updated subscription {SubscriptionId} status to {Status}",
                subscription.Id, dbSubscription.Status);
        }
    }

    private async Task HandleSubscriptionDeletedAsync(Stripe.Subscription? subscription)
    {
        if (subscription == null) return;
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dbSubscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id);
        
        if (dbSubscription != null)
        {
            dbSubscription.Status = SubscriptionStatus.Cancelled;
            dbSubscription.CancelledAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            
            _logger.LogInformation("Subscription {SubscriptionId} cancelled", subscription.Id);
        }
    }

    private Task HandlePaymentFailedAsync(Invoice? invoice)
    {
        if (invoice == null) return Task.CompletedTask;
        
        _logger.LogWarning(
            "Payment failed for subscription {SubscriptionId}, customer {CustomerId}",
            invoice.SubscriptionId, invoice.CustomerId);
        
        // TODO: Send notification email to user
        
        return Task.CompletedTask;
    }
}
