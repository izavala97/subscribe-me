using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using suscribe_me.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace suscribe_me.Controllers;

/// <summary>
/// Handles user profile pages (/{username})
/// </summary>
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMarkdownService _markdown;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        ApplicationDbContext db,
        IMarkdownService markdown,
        ILogger<ProfileController> logger)
    {
        _db = db;
        _markdown = markdown;
        _logger = logger;
    }

    // GET: /{username}
    [HttpGet]
    public async Task<IActionResult> Index(string username)
    {
        if (string.IsNullOrEmpty(username))
            return NotFound();
        
        var user = await _db.Users
            .Include(u => u.Followers)
            .Include(u => u.Following)
            .Include(u => u.Subscribers)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        
        if (user == null)
            return NotFound();
        
        var currentUserId = GetCurrentUserId();
        var isOwner = currentUserId == user.Id;
        var isFollowing = false;
        var isSubscribed = false;
        
        if (currentUserId.HasValue && !isOwner)
        {
            isFollowing = await _db.Follows
                .AnyAsync(f => f.FollowerId == currentUserId && f.FollowedId == user.Id);
            isSubscribed = await _db.Subscriptions
                .AnyAsync(s => s.SubscriberId == currentUserId && s.CreatorId == user.Id && s.Status == SubscriptionStatus.Active);
        }
        
        // Get posts the current user can see
        var posts = await GetAccessiblePostsAsync(user.Id, currentUserId, isFollowing, isSubscribed);
        
        var viewModel = new ProfileViewModel
        {
            User = user,
            BioHtml = _markdown.ToHtml(user.Bio ?? ""),
            Posts = posts.Select(p => new PostSummaryViewModel
            {
                Post = p,
                ContentHtml = _markdown.ToHtml(p.Content),
                CanAccess = CanAccessPost(p, currentUserId, isFollowing, isSubscribed)
            }).ToList(),
            IsOwner = isOwner,
            IsFollowing = isFollowing,
            IsSubscribed = isSubscribed,
            FollowerCount = user.Followers.Count,
            FollowingCount = user.Following.Count,
            SubscriberCount = user.Subscribers.Count(s => s.Status == SubscriptionStatus.Active)
        };
        
        return View(viewModel);
    }

    // GET: /profile/edit
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null)
            return NotFound();
        
        var model = new EditProfileViewModel
        {
            DisplayName = user.DisplayName ?? user.Username,
            Bio = user.Bio ?? "",
            MonthlyPriceCents = user.MonthlyPriceCents
        };
        
        return View(model);
    }

    // POST: /profile/edit
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);
        
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null)
            return NotFound();
        
        user.DisplayName = model.DisplayName;
        user.Bio = model.Bio;
        user.MonthlyPriceCents = model.MonthlyPriceCents;
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = "Profile updated successfully!";
        
        _logger.LogInformation("User {UserId} updated profile", userId);
        
        return RedirectToAction("Index", new { username = user.Username });
    }

    // GET: /profile/settings
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Settings()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null)
            return NotFound();
        
        return View(user);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<List<Post>> GetAccessiblePostsAsync(
        Guid authorId, 
        Guid? viewerId, 
        bool isFollowing, 
        bool isSubscribed)
    {
        var query = _db.Posts
            .Where(p => p.AuthorId == authorId && p.IsPublished)
            .OrderByDescending(p => p.CreatedAt);
        
        // If viewer is the author, return all posts
        if (viewerId == authorId)
            return await query.ToListAsync();
        
        // For other viewers, get all posts but we'll filter what they can see in the view
        return await query.ToListAsync();
    }

    private bool CanAccessPost(Post post, Guid? viewerId, bool isFollowing, bool isSubscribed)
    {
        if (viewerId == post.AuthorId)
            return true; // Author can always see their own posts
        
        return post.Visibility switch
        {
            PostVisibility.Public => true,
            PostVisibility.FollowersOnly => isFollowing || isSubscribed,
            PostVisibility.SubscribersOnly => isSubscribed,
            PostVisibility.SinglePurchase => false, // Will check purchases separately
            _ => false
        };
    }
}

// View Models
public class ProfileViewModel
{
    public required User User { get; set; }
    public string BioHtml { get; set; } = "";
    public List<PostSummaryViewModel> Posts { get; set; } = new();
    public bool IsOwner { get; set; }
    public bool IsFollowing { get; set; }
    public bool IsSubscribed { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int SubscriberCount { get; set; }
}

public class PostSummaryViewModel
{
    public required Post Post { get; set; }
    public string ContentHtml { get; set; } = "";
    public bool CanAccess { get; set; }
}

public class EditProfileViewModel
{
    [Required]
    [StringLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
    public string DisplayName { get; set; } = "";
    
    [StringLength(2000, ErrorMessage = "Bio cannot exceed 2000 characters")]
    public string Bio { get; set; } = "";
    
    [Range(0, 100000, ErrorMessage = "Price must be between $0 and $1000")]
    [Display(Name = "Monthly subscription price (in cents, 0 = free)")]
    public int MonthlyPriceCents { get; set; }
}
