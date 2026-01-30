using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using suscribe_me.Services;
using System.Security.Claims;

namespace suscribe_me.Controllers;

/// <summary>
/// Handles feed views (home feed, following, favorites)
/// </summary>
public class FeedController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMarkdownService _markdown;
    private readonly ILogger<FeedController> _logger;

    public FeedController(
        ApplicationDbContext db,
        IMarkdownService markdown,
        ILogger<FeedController> logger)
    {
        _db = db;
        _markdown = markdown;
        _logger = logger;
    }

    // GET: /feed/following
    [Authorize]
    public async Task<IActionResult> Following(int page = 1)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        const int pageSize = 20;
        
        // Get IDs of users we follow
        var followingIds = await _db.Follows
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FollowedId)
            .ToListAsync();
        
        // Get IDs of users we're subscribed to
        var subscribedIds = await _db.Subscriptions
            .Where(s => s.SubscriberId == userId && s.Status == SubscriptionStatus.Active)
            .Select(s => s.CreatorId)
            .ToListAsync();
        
        // Get posts from followed users that we can access
        var posts = await _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Favorites)
            .Where(p => followingIds.Contains(p.AuthorId) && p.IsPublished)
            .Where(p => 
                p.Visibility == PostVisibility.Public ||
                p.Visibility == PostVisibility.FollowersOnly ||
                (p.Visibility == PostVisibility.SubscribersOnly && subscribedIds.Contains(p.AuthorId)))
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        var favoriteIds = await _db.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.PostId)
            .ToListAsync();
        
        var viewModel = new FeedViewModel
        {
            Title = "Following",
            Posts = posts.Select(p => new FeedPostViewModel
            {
                Post = p,
                ContentHtml = _markdown.ToHtml(p.Content.Length > 500 ? p.Content[..500] + "..." : p.Content),
                IsFavorited = favoriteIds.Contains(p.Id),
                FavoriteCount = p.Favorites.Count
            }).ToList(),
            CurrentPage = page,
            HasMore = posts.Count == pageSize
        };
        
        return View("Feed", viewModel);
    }

    // GET: /feed/favorites
    [Authorize]
    public async Task<IActionResult> Favorites(int page = 1)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        const int pageSize = 20;
        
        var posts = await _db.Favorites
            .Include(f => f.Post)
            .ThenInclude(p => p.Author)
            .Include(f => f.Post)
            .ThenInclude(p => p.Favorites)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => f.Post)
            .ToListAsync();
        
        var viewModel = new FeedViewModel
        {
            Title = "Favorites",
            Posts = posts.Select(p => new FeedPostViewModel
            {
                Post = p,
                ContentHtml = _markdown.ToHtml(p.Content.Length > 500 ? p.Content[..500] + "..." : p.Content),
                IsFavorited = true,
                FavoriteCount = p.Favorites.Count
            }).ToList(),
            CurrentPage = page,
            HasMore = posts.Count == pageSize
        };
        
        return View("Feed", viewModel);
    }

    // GET: /feed/explore
    [AllowAnonymous]
    public async Task<IActionResult> Explore(int page = 1)
    {
        var userId = GetCurrentUserId();
        const int pageSize = 20;
        
        // Get public posts from all users
        var posts = await _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Favorites)
            .Where(p => p.IsPublished && p.Visibility == PostVisibility.Public)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        var favoriteIds = userId.HasValue
            ? await _db.Favorites.Where(f => f.UserId == userId).Select(f => f.PostId).ToListAsync()
            : new List<Guid>();
        
        var viewModel = new FeedViewModel
        {
            Title = "Explore",
            Posts = posts.Select(p => new FeedPostViewModel
            {
                Post = p,
                ContentHtml = _markdown.ToHtml(p.Content.Length > 500 ? p.Content[..500] + "..." : p.Content),
                IsFavorited = favoriteIds.Contains(p.Id),
                FavoriteCount = p.Favorites.Count
            }).ToList(),
            CurrentPage = page,
            HasMore = posts.Count == pageSize
        };
        
        return View("Feed", viewModel);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }
}

// View Models
public class FeedViewModel
{
    public string Title { get; set; } = "";
    public List<FeedPostViewModel> Posts { get; set; } = new();
    public int CurrentPage { get; set; }
    public bool HasMore { get; set; }
}

public class FeedPostViewModel
{
    public required Post Post { get; set; }
    public string ContentHtml { get; set; } = "";
    public bool IsFavorited { get; set; }
    public int FavoriteCount { get; set; }
}
