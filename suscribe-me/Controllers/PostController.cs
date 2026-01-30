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
/// Handles post creation, editing, and viewing
/// </summary>
[Authorize]
public class PostController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMarkdownService _markdown;
    private readonly ILogger<PostController> _logger;

    public PostController(
        ApplicationDbContext db,
        IMarkdownService markdown,
        ILogger<PostController> logger)
    {
        _db = db;
        _markdown = markdown;
        _logger = logger;
    }

    // GET: /post/{id}
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> View(Guid id)
    {
        var post = await _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Favorites)
            .FirstOrDefaultAsync(p => p.Id == id);
        
        if (post == null)
            return NotFound();
        
        var currentUserId = GetCurrentUserId();
        var canAccess = await CanAccessPostAsync(post, currentUserId);
        var isFavorited = currentUserId.HasValue && 
            await _db.Favorites.AnyAsync(f => f.UserId == currentUserId && f.PostId == id);
        
        var viewModel = new PostViewViewModel
        {
            Post = post,
            ContentHtml = canAccess ? _markdown.ToHtml(post.Content) : "",
            CanAccess = canAccess,
            IsFavorited = isFavorited,
            IsOwner = currentUserId == post.AuthorId,
            FavoriteCount = post.Favorites.Count
        };
        
        return View(viewModel);
    }

    // GET: /post/create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreatePostViewModel());
    }

    // POST: /post/create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePostViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);
        
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        // Validate price for SinglePurchase posts
        if (model.Visibility == PostVisibility.SinglePurchase && model.PriceCents <= 0)
        {
            ModelState.AddModelError(nameof(model.PriceCents), "Single purchase posts must have a price greater than 0.");
            return View(model);
        }
        
        var post = new Post
        {
            AuthorId = userId.Value,
            Title = model.Title,
            Content = model.Content,
            Visibility = model.Visibility,
            PriceCents = model.Visibility == PostVisibility.SinglePurchase ? model.PriceCents : 0,
            PreviewText = model.PreviewText,
            IsPublished = true
        };
        
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} created post {PostId} with visibility {Visibility}", 
            userId, post.Id, post.Visibility);
        
        TempData["Success"] = "Post created successfully!";
        
        return RedirectToAction("View", new { id = post.Id });
    }

    // GET: /post/edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();
        
        if (post.AuthorId != userId.Value)
            return Forbid();
        
        var model = new EditPostViewModel
        {
            Id = post.Id,
            Title = post.Title,
            Content = post.Content,
            Visibility = post.Visibility,
            PriceCents = post.PriceCents,
            PreviewText = post.PreviewText ?? ""
        };
        
        return View(model);
    }

    // POST: /post/edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditPostViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);
        
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();
        
        if (post.AuthorId != userId.Value)
            return Forbid();
        
        post.Title = model.Title;
        post.Content = model.Content;
        post.Visibility = model.Visibility;
        post.PriceCents = model.Visibility == PostVisibility.SinglePurchase ? model.PriceCents : 0;
        post.PreviewText = model.PreviewText;
        post.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} edited post {PostId}", userId, post.Id);
        
        TempData["Success"] = "Post updated successfully!";
        
        return RedirectToAction("View", new { id = post.Id });
    }

    // POST: /post/delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var post = await _db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
            return NotFound();
        
        if (post.AuthorId != userId.Value)
            return Forbid();
        
        var username = post.Author.Username;
        
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} deleted post {PostId}", userId, id);
        
        TempData["Success"] = "Post deleted successfully!";
        
        return RedirectToAction("Index", "Profile", new { username });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<bool> CanAccessPostAsync(Post post, Guid? viewerId)
    {
        if (viewerId == post.AuthorId)
            return true;
        
        if (post.Visibility == PostVisibility.Public)
            return true;
        
        if (!viewerId.HasValue)
            return false;
        
        var isFollowing = await _db.Follows
            .AnyAsync(f => f.FollowerId == viewerId && f.FollowedId == post.AuthorId);
        
        var isSubscribed = await _db.Subscriptions
            .AnyAsync(s => s.SubscriberId == viewerId && s.CreatorId == post.AuthorId && s.Status == SubscriptionStatus.Active);
        
        return post.Visibility switch
        {
            PostVisibility.FollowersOnly => isFollowing || isSubscribed,
            PostVisibility.SubscribersOnly => isSubscribed,
            PostVisibility.SinglePurchase => await _db.PostPurchases.AnyAsync(pp => pp.UserId == viewerId && pp.PostId == post.Id),
            _ => false
        };
    }
}

// View Models
public class PostViewViewModel
{
    public required Post Post { get; set; }
    public string ContentHtml { get; set; } = "";
    public bool CanAccess { get; set; }
    public bool IsFavorited { get; set; }
    public bool IsOwner { get; set; }
    public int FavoriteCount { get; set; }
}

public class CreatePostViewModel
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = "";
    
    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = "";
    
    [Display(Name = "Who can see this post?")]
    public PostVisibility Visibility { get; set; } = PostVisibility.Public;
    
    [Range(0, 100000, ErrorMessage = "Price must be between $0 and $1000")]
    [Display(Name = "Price (in cents, for single purchase posts)")]
    public int PriceCents { get; set; }
    
    [StringLength(500, ErrorMessage = "Preview text cannot exceed 500 characters")]
    [Display(Name = "Preview text (shown to users who can't access the full post)")]
    public string PreviewText { get; set; } = "";
}

public class EditPostViewModel : CreatePostViewModel
{
    public Guid Id { get; set; }
}
