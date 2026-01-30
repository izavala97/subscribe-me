namespace suscribe_me.Models;

/// <summary>
/// Visibility levels for posts.
/// Determines who can view the content.
/// </summary>
public enum PostVisibility
{
    /// <summary>
    /// Anyone can view, including non-logged-in users
    /// </summary>
    Public = 0,
    
    /// <summary>
    /// Only logged-in users who follow the author can view
    /// </summary>
    FollowersOnly = 1,
    
    /// <summary>
    /// Only users with an active subscription to the author can view
    /// </summary>
    SubscribersOnly = 2,
    
    /// <summary>
    /// Requires a one-time payment to view
    /// </summary>
    SinglePurchase = 3
}

/// <summary>
/// Represents a post/content created by a user.
/// Content is text with markdown support.
/// </summary>
public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The author of this post
    /// </summary>
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    
    /// <summary>
    /// Post title
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Post content in markdown format
    /// </summary>
    public required string Content { get; set; }
    
    /// <summary>
    /// Who can view this post
    /// </summary>
    public PostVisibility Visibility { get; set; } = PostVisibility.Public;
    
    /// <summary>
    /// Price in cents for SinglePurchase posts (0 for other visibility types)
    /// </summary>
    public int PriceCents { get; set; } = 0;
    
    /// <summary>
    /// Preview/teaser text shown to users who can't access the full content
    /// </summary>
    public string? PreviewText { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Whether the post is published or draft
    /// </summary>
    public bool IsPublished { get; set; } = true;
    
    // Navigation properties
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<PostPurchase> Purchases { get; set; } = new List<PostPurchase>();
}

/// <summary>
/// Tracks one-time purchases of SinglePurchase posts
/// </summary>
public class PostPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    
    /// <summary>
    /// Amount paid in cents
    /// </summary>
    public int AmountCents { get; set; }
    
    /// <summary>
    /// Stripe payment intent ID
    /// </summary>
    public string? StripePaymentIntentId { get; set; }
    
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
}
