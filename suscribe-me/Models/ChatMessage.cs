namespace suscribe_me.Models;

/// <summary>
/// Represents a chat message between two users
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The user who sent the message
    /// </summary>
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    
    /// <summary>
    /// The user who receives the message
    /// </summary>
    public Guid ReceiverId { get; set; }
    public User Receiver { get; set; } = null!;
    
    /// <summary>
    /// Message content (plain text, markdown support can be added later)
    /// </summary>
    public required string Content { get; set; }
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether the receiver has read this message
    /// </summary>
    public bool IsRead { get; set; } = false;
    
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// Represents a conversation thread between two users (for listing purposes)
/// </summary>
public class Conversation
{
    /// <summary>
    /// The other user in the conversation
    /// </summary>
    public required User OtherUser { get; set; }
    
    /// <summary>
    /// The most recent message in the conversation
    /// </summary>
    public required ChatMessage LastMessage { get; set; }
    
    /// <summary>
    /// Number of unread messages from the other user
    /// </summary>
    public int UnreadCount { get; set; }
}
