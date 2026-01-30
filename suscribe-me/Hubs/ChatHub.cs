using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using System.Security.Claims;

namespace suscribe_me.Hubs;

/// <summary>
/// SignalR hub for real-time chat functionality
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ChatHub> _logger;
    
    // Track user connections (userId -> connectionId)
    private static readonly Dictionary<string, string> UserConnections = new();

    public ChatHub(ApplicationDbContext db, ILogger<ChatHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            lock (UserConnections)
            {
                UserConnections[userId] = Context.ConnectionId;
            }
            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            lock (UserConnections)
            {
                UserConnections.Remove(userId);
            }
            _logger.LogInformation("User {UserId} disconnected", userId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Send a message to another user
    /// </summary>
    public async Task SendMessage(string receiverId, string content)
    {
        var senderIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(senderIdStr) || !Guid.TryParse(senderIdStr, out var senderId))
        {
            throw new HubException("Not authenticated");
        }
        
        if (!Guid.TryParse(receiverId, out var receiverIdGuid))
        {
            throw new HubException("Invalid receiver ID");
        }
        
        if (string.IsNullOrWhiteSpace(content) || content.Length > 4000)
        {
            throw new HubException("Message must be between 1 and 4000 characters");
        }
        
        // Save message to database
        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverIdGuid,
            Content = content.Trim(),
            SentAt = DateTime.UtcNow
        };
        
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();
        
        // Get sender info for the notification
        var sender = await _db.Users.FindAsync(senderId);
        
        var messageDto = new
        {
            Id = message.Id,
            SenderId = senderId.ToString(),
            SenderUsername = sender?.Username ?? "Unknown",
            SenderDisplayName = sender?.DisplayName ?? sender?.Username ?? "Unknown",
            Content = message.Content,
            SentAt = message.SentAt.ToString("o")
        };
        
        // Send to receiver if online
        string? receiverConnectionId;
        lock (UserConnections)
        {
            UserConnections.TryGetValue(receiverId, out receiverConnectionId);
        }
        
        if (receiverConnectionId != null)
        {
            await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", messageDto);
        }
        
        // Send confirmation back to sender
        await Clients.Caller.SendAsync("MessageSent", messageDto);
        
        _logger.LogInformation("Message sent from {SenderId} to {ReceiverId}", senderId, receiverId);
    }

    /// <summary>
    /// Mark messages from a user as read
    /// </summary>
    public async Task MarkAsRead(string senderId)
    {
        var receiverIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(receiverIdStr) || !Guid.TryParse(receiverIdStr, out var receiverId))
        {
            throw new HubException("Not authenticated");
        }
        
        if (!Guid.TryParse(senderId, out var senderIdGuid))
        {
            throw new HubException("Invalid sender ID");
        }
        
        var unreadMessages = await _db.ChatMessages
            .Where(m => m.SenderId == senderIdGuid && m.ReceiverId == receiverId && !m.IsRead)
            .ToListAsync();
        
        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
        }
        
        await _db.SaveChangesAsync();
        
        // Notify sender that messages were read
        string? senderConnectionId;
        lock (UserConnections)
        {
            UserConnections.TryGetValue(senderId, out senderConnectionId);
        }
        
        if (senderConnectionId != null)
        {
            await Clients.Client(senderConnectionId).SendAsync("MessagesRead", receiverId.ToString());
        }
        
        _logger.LogInformation("Marked {Count} messages as read from {SenderId} to {ReceiverId}", 
            unreadMessages.Count, senderId, receiverId);
    }

    /// <summary>
    /// Check if a user is online
    /// </summary>
    public Task<bool> IsUserOnline(string userId)
    {
        lock (UserConnections)
        {
            return Task.FromResult(UserConnections.ContainsKey(userId));
        }
    }
}
