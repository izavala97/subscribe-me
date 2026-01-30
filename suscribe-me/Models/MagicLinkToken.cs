namespace suscribe_me.Models;

/// <summary>
/// Magic link token for passwordless authentication.
/// Tokens expire after 15 minutes and can only be used once.
/// </summary>
public class MagicLinkToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The secure token sent via email
    /// </summary>
    public required string Token { get; set; }
    
    /// <summary>
    /// Email address this token is valid for
    /// </summary>
    public required string Email { get; set; }
    
    /// <summary>
    /// Username chosen during registration (null for existing users)
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Whether this is a registration (true) or login (false) token
    /// </summary>
    public bool IsRegistration { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Token expires 15 minutes after creation
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);
    
    /// <summary>
    /// Whether this token has been used
    /// </summary>
    public bool IsUsed { get; set; } = false;
    
    public bool IsValid => !IsUsed && DateTime.UtcNow < ExpiresAt;
}
