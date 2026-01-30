using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using System.Security.Claims;

namespace suscribe_me.Controllers;

/// <summary>
/// Handles favorite/unfavorite actions for posts
/// </summary>
[Authorize]
[Route("api/favorite")]
public class FavoriteController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<FavoriteController> _logger;

    public FavoriteController(ApplicationDbContext db, ILogger<FavoriteController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // POST: /api/favorite/{postId}/toggle
    [HttpPost("{postId}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid postId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();
        
        var favorite = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == currentUserId && f.PostId == postId);
        
        bool isFavorited;
        
        if (favorite != null)
        {
            _db.Favorites.Remove(favorite);
            await _db.SaveChangesAsync();
            isFavorited = false;
            _logger.LogInformation("User {UserId} unfavorited post {PostId}", currentUserId, postId);
        }
        else
        {
            var postExists = await _db.Posts.AnyAsync(p => p.Id == postId);
            if (!postExists)
                return NotFound("Post not found");
            
            favorite = new Favorite
            {
                UserId = currentUserId.Value,
                PostId = postId
            };
            
            _db.Favorites.Add(favorite);
            await _db.SaveChangesAsync();
            isFavorited = true;
            _logger.LogInformation("User {UserId} favorited post {PostId}", currentUserId, postId);
        }
        
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            var count = await _db.Favorites.CountAsync(f => f.PostId == postId);
            return Json(new { success = true, isFavorited, count });
        }
        
        return Redirect(Request.Headers.Referer.ToString() ?? "/");
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }
}
