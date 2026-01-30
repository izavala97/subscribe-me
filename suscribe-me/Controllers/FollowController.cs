using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using System.Security.Claims;

namespace suscribe_me.Controllers;

/// <summary>
/// Handles follow/unfollow actions
/// </summary>
[Authorize]
[Route("api/follow")]
public class FollowController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<FollowController> _logger;

    public FollowController(ApplicationDbContext db, ILogger<FollowController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // POST: /api/follow/{userId}
    [HttpPost("{userId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();
        
        if (currentUserId == userId)
            return BadRequest("You cannot follow yourself");
        
        // Check if already following
        var existingFollow = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowedId == userId);
        
        if (existingFollow != null)
            return BadRequest("Already following this user");
        
        // Check if user exists
        var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return NotFound("User not found");
        
        var follow = new Follow
        {
            FollowerId = currentUserId.Value,
            FollowedId = userId
        };
        
        _db.Follows.Add(follow);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("User {FollowerId} followed {FollowedId}", currentUserId, userId);
        
        // Return to previous page or profile
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { success = true, isFollowing = true });
        
        return Redirect(Request.Headers.Referer.ToString() ?? "/");
    }

    // DELETE: /api/follow/{userId}
    [HttpDelete("{userId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unfollow(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();
        
        var follow = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowedId == userId);
        
        if (follow == null)
            return NotFound("Not following this user");
        
        _db.Follows.Remove(follow);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("User {FollowerId} unfollowed {FollowedId}", currentUserId, userId);
        
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { success = true, isFollowing = false });
        
        return Redirect(Request.Headers.Referer.ToString() ?? "/");
    }

    // POST: /api/follow/{userId}/toggle (for simpler form handling)
    [HttpPost("{userId}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();
        
        var follow = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowedId == userId);
        
        if (follow != null)
        {
            _db.Follows.Remove(follow);
            await _db.SaveChangesAsync();
            _logger.LogInformation("User {FollowerId} unfollowed {FollowedId}", currentUserId, userId);
        }
        else
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return NotFound("User not found");
            
            if (currentUserId == userId)
                return BadRequest("You cannot follow yourself");
            
            follow = new Follow
            {
                FollowerId = currentUserId.Value,
                FollowedId = userId
            };
            
            _db.Follows.Add(follow);
            await _db.SaveChangesAsync();
            _logger.LogInformation("User {FollowerId} followed {FollowedId}", currentUserId, userId);
        }
        
        return Redirect(Request.Headers.Referer.ToString() ?? "/");
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }
}
