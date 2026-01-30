namespace suscribe_me.Models;

/// <summary>
/// Represents a follow relationship between users.
/// Following is free and allows access to FollowersOnly content.
/// </summary>
public class Follow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The user who is following
    /// </summary>
    public Guid FollowerId { get; set; }
    public User Follower { get; set; } = null!;
    
    /// <summary>
    /// The user being followed
    /// </summary>
    public Guid FollowedId { get; set; }
    public User Followed { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a paid subscription relationship.
/// Subscribers get access to SubscribersOnly content.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The user who is subscribing (paying)
    /// </summary>
    public Guid SubscriberId { get; set; }
    public User Subscriber { get; set; } = null!;
    
    /// <summary>
    /// The creator being subscribed to
    /// </summary>
    public Guid CreatorId { get; set; }
    public User Creator { get; set; } = null!;
    
    /// <summary>
    /// Stripe subscription ID
    /// </summary>
    public string? StripeSubscriptionId { get; set; }
    
    /// <summary>
    /// Current status of the subscription
    /// </summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    
    /// <summary>
    /// Monthly amount in cents
    /// </summary>
    public int AmountCents { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the current period ends (for renewal)
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; set; }
    
    /// <summary>
    /// When the subscription was cancelled (if applicable)
    /// </summary>
    public DateTime? CancelledAt { get; set; }
}

public enum SubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Cancelled = 2,
    Expired = 3
}

/// <summary>
/// Represents a user's favorite/saved post
/// </summary>
public class Favorite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
