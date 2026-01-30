using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using System.Security.Claims;

namespace suscribe_me.Controllers;

/// <summary>
/// Handles chat/messaging functionality
/// </summary>
[Authorize]
public class ChatController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ApplicationDbContext db, ILogger<ChatController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET: /chat
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");
        
        // Get all conversations (grouped by the other user)
        var conversations = await GetConversationsAsync(userId.Value);
        
        return View(new ChatIndexViewModel { Conversations = conversations });
    }

    // GET: /chat/{userId}
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Conversation(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");
        
        var otherUser = await _db.Users.FindAsync(userId);
        if (otherUser == null)
            return NotFound();
        
        // Get messages between the two users
        var messages = await _db.ChatMessages
            .Where(m => 
                (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                (m.SenderId == userId && m.ReceiverId == currentUserId))
            .OrderBy(m => m.SentAt)
            .Take(100) // Limit to last 100 messages
            .ToListAsync();
        
        // Mark received messages as read
        var unreadMessages = messages
            .Where(m => m.ReceiverId == currentUserId && !m.IsRead)
            .ToList();
        
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
            msg.ReadAt = DateTime.UtcNow;
        }
        
        if (unreadMessages.Any())
            await _db.SaveChangesAsync();
        
        var conversations = await GetConversationsAsync(currentUserId.Value);
        
        return View(new ChatConversationViewModel
        {
            Conversations = conversations,
            CurrentConversation = otherUser,
            Messages = messages,
            CurrentUserId = currentUserId.Value
        });
    }

    // GET: /chat/new/{userId}
    [HttpGet("new/{userId:guid}")]
    public async Task<IActionResult> New(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");
        
        if (currentUserId == userId)
        {
            TempData["Error"] = "You cannot message yourself.";
            return RedirectToAction("Index");
        }
        
        var otherUser = await _db.Users.FindAsync(userId);
        if (otherUser == null)
            return NotFound();
        
        // Redirect to conversation view (will be empty if no messages yet)
        return RedirectToAction("Conversation", new { userId });
    }

    // POST: /chat/send
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(Guid receiverId, string content)
    {
        var senderId = GetCurrentUserId();
        if (!senderId.HasValue)
            return RedirectToAction("Login", "Account");
        
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Message cannot be empty.";
            return RedirectToAction("Conversation", new { userId = receiverId });
        }
        
        if (content.Length > 4000)
        {
            TempData["Error"] = "Message is too long (max 4000 characters).";
            return RedirectToAction("Conversation", new { userId = receiverId });
        }
        
        var receiverExists = await _db.Users.AnyAsync(u => u.Id == receiverId);
        if (!receiverExists)
            return NotFound();
        
        var message = new ChatMessage
        {
            SenderId = senderId.Value,
            ReceiverId = receiverId,
            Content = content.Trim()
        };
        
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("User {SenderId} sent message to {ReceiverId}", senderId, receiverId);
        
        return RedirectToAction("Conversation", new { userId = receiverId });
    }

    private async Task<List<Conversation>> GetConversationsAsync(Guid userId)
    {
        // Get all users we've messaged or received messages from
        var messageUsers = await _db.ChatMessages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .Select(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
            .Distinct()
            .ToListAsync();
        
        var conversations = new List<Conversation>();
        
        foreach (var otherUserId in messageUsers)
        {
            var otherUser = await _db.Users.FindAsync(otherUserId);
            if (otherUser == null) continue;
            
            var lastMessage = await _db.ChatMessages
                .Where(m => 
                    (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                    (m.SenderId == otherUserId && m.ReceiverId == userId))
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();
            
            if (lastMessage == null) continue;
            
            var unreadCount = await _db.ChatMessages
                .CountAsync(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead);
            
            conversations.Add(new Conversation
            {
                OtherUser = otherUser,
                LastMessage = lastMessage,
                UnreadCount = unreadCount
            });
        }
        
        return conversations.OrderByDescending(c => c.LastMessage.SentAt).ToList();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }
}

// View Models
public class ChatIndexViewModel
{
    public List<Conversation> Conversations { get; set; } = new();
}

public class ChatConversationViewModel
{
    public List<Conversation> Conversations { get; set; } = new();
    public required User CurrentConversation { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
    public Guid CurrentUserId { get; set; }
}
