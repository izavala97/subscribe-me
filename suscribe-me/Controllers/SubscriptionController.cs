using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using suscribe_me.Services;
using System.Security.Claims;

namespace suscribe_me.Controllers;

/// <summary>
/// Handles subscription management and Stripe checkout
/// </summary>
[Authorize]
public class SubscriptionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ApplicationDbContext db,
        IPaymentService paymentService,
        IConfiguration configuration,
        ILogger<SubscriptionController> logger)
    {
        _db = db;
        _paymentService = paymentService;
        _configuration = configuration;
        _logger = logger;
    }

    // GET: /subscription/my
    [HttpGet]
    public async Task<IActionResult> My()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var subscriptions = await _db.Subscriptions
            .Include(s => s.Creator)
            .Where(s => s.SubscriberId == userId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
        
        return View(subscriptions);
    }

    // GET: /subscription/subscribers
    [HttpGet]
    public async Task<IActionResult> Subscribers()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var subscribers = await _db.Subscriptions
            .Include(s => s.Subscriber)
            .Where(s => s.CreatorId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
        
        return View(subscribers);
    }

    // POST: /subscription/subscribe/{creatorId}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(Guid creatorId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        if (userId == creatorId)
        {
            TempData["Error"] = "You cannot subscribe to yourself.";
            return Redirect(Request.Headers.Referer.ToString() ?? "/");
        }
        
        // Check if already subscribed
        var existingSubscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriberId == userId && s.CreatorId == creatorId && s.Status == SubscriptionStatus.Active);
        
        if (existingSubscription != null)
        {
            TempData["Error"] = "You are already subscribed to this creator.";
            return Redirect(Request.Headers.Referer.ToString() ?? "/");
        }
        
        var creator = await _db.Users.FindAsync(creatorId);
        if (creator == null)
        {
            TempData["Error"] = "Creator not found.";
            return Redirect(Request.Headers.Referer.ToString() ?? "/");
        }
        
        if (creator.MonthlyPriceCents <= 0)
        {
            TempData["Error"] = "This creator does not have a subscription tier set up.";
            return Redirect(Request.Headers.Referer.ToString() ?? "/");
        }
        
        var baseUrl = _configuration["App:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/subscription/success";
        var cancelUrl = $"{baseUrl}/{creator.Username}";
        
        try
        {
            var checkoutUrl = await _paymentService.CreateSubscriptionCheckoutAsync(
                userId.Value, creatorId, successUrl, cancelUrl);
            
            _logger.LogInformation("User {UserId} starting subscription checkout for creator {CreatorId}", 
                userId, creatorId);
            
            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subscription checkout for user {UserId} to creator {CreatorId}", 
                userId, creatorId);
            TempData["Error"] = "Failed to start checkout. Please try again.";
            return Redirect(Request.Headers.Referer.ToString() ?? "/");
        }
    }

    // GET: /subscription/success
    [HttpGet]
    public IActionResult Success(string? session_id)
    {
        _logger.LogInformation("Subscription checkout completed, session: {SessionId}", session_id);
        TempData["Success"] = "Subscription activated! You now have access to exclusive content.";
        return RedirectToAction("My");
    }

    // POST: /subscription/cancel/{subscriptionId}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid subscriptionId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var subscription = await _db.Subscriptions.FindAsync(subscriptionId);
        if (subscription == null || subscription.SubscriberId != userId)
            return NotFound();
        
        if (subscription.Status != SubscriptionStatus.Active)
        {
            TempData["Error"] = "This subscription is not active.";
            return RedirectToAction("My");
        }
        
        try
        {
            if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            {
                await _paymentService.CancelSubscriptionAsync(subscription.StripeSubscriptionId);
            }
            
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("User {UserId} cancelled subscription {SubscriptionId}", userId, subscriptionId);
            TempData["Success"] = "Subscription cancelled. You will retain access until the end of your billing period.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel subscription {SubscriptionId}", subscriptionId);
            TempData["Error"] = "Failed to cancel subscription. Please try again.";
        }
        
        return RedirectToAction("My");
    }

    // POST: /subscription/purchase/{postId}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchasePost(Guid postId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var post = await _db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null)
            return NotFound();
        
        if (post.Visibility != PostVisibility.SinglePurchase || post.PriceCents <= 0)
        {
            TempData["Error"] = "This post is not available for purchase.";
            return RedirectToAction("View", "Post", new { id = postId });
        }
        
        // Check if already purchased
        var existingPurchase = await _db.PostPurchases
            .AnyAsync(pp => pp.UserId == userId && pp.PostId == postId);
        
        if (existingPurchase)
        {
            TempData["Error"] = "You have already purchased this post.";
            return RedirectToAction("View", "Post", new { id = postId });
        }
        
        var baseUrl = _configuration["App:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/post/{postId}";
        var cancelUrl = successUrl;
        
        try
        {
            var checkoutUrl = await _paymentService.CreatePostPurchaseCheckoutAsync(
                userId.Value, postId, successUrl, cancelUrl);
            
            _logger.LogInformation("User {UserId} starting post purchase checkout for post {PostId}", 
                userId, postId);
            
            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create post purchase checkout for user {UserId}, post {PostId}", 
                userId, postId);
            TempData["Error"] = "Failed to start checkout. Please try again.";
            return RedirectToAction("View", "Post", new { id = postId });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }
}
