namespace suscribe_me.Models;

/// <summary>
/// Represents a user in the subscription platform.
/// Each user has a unique username that becomes their profile URL.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Unique username used in profile URL (e.g., /username)
    /// </summary>
    public required string Username { get; set; }
    
    /// <summary>
    /// Email address used for authentication via magic link
    /// </summary>
    public required string Email { get; set; }
    
    /// <summary>
    /// Display name shown on profile
    /// </summary>
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// User bio/description (supports markdown)
    /// </summary>
    public string? Bio { get; set; }
    
    /// <summary>
    /// Profile picture URL (future: Azure Blob Storage)
    /// </summary>
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// Monthly subscription price in cents (0 = free tier only)
    /// </summary>
    public int MonthlyPriceCents { get; set; } = 0;
    
    /// <summary>
    /// Stripe Connect account ID for receiving payments
    /// </summary>
    public string? StripeAccountId { get; set; }
    
    /// <summary>
    /// Stripe customer ID for making payments
    /// </summary>
    public string? StripeCustomerId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Follow> Followers { get; set; } = new List<Follow>();
    public ICollection<Follow> Following { get; set; } = new List<Follow>();
    public ICollection<Subscription> Subscribers { get; set; } = new List<Subscription>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
    public ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
}
