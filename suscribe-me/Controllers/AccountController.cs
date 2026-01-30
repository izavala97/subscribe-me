using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Models;
using suscribe_me.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;

namespace suscribe_me.Controllers;

/// <summary>
/// Handles user authentication with magic link (passwordless) login
/// </summary>
public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ApplicationDbContext db,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AccountController> logger)
    {
        _db = db;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    // GET: /account/login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // POST: /account/login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);
        
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email.ToLower());
        
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "No account found with this email. Please register first.");
            return View(model);
        }
        
        // Generate magic link token
        var token = await CreateMagicLinkTokenAsync(model.Email.ToLower(), isRegistration: false);
        
        // Build magic link URL
        var baseUrl = _configuration["App:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var magicLink = $"{baseUrl}/account/verify?token={token}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
        
        // Send email
        await _emailService.SendMagicLinkAsync(model.Email, magicLink, isRegistration: false);
        
        _logger.LogInformation("Magic link sent to {Email} for login", model.Email);
        
        return View("CheckEmail", new CheckEmailViewModel { Email = model.Email, IsRegistration = false });
    }

    // GET: /account/register
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        
        return View();
    }

    // POST: /account/register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);
        
        var emailLower = model.Email.ToLower();
        var usernameLower = model.Username.ToLower();
        
        // Check if email already exists
        if (await _db.Users.AnyAsync(u => u.Email == emailLower))
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already registered. Please login instead.");
            return View(model);
        }
        
        // Check if username already exists
        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already taken. Please choose another.");
            return View(model);
        }
        
        // Validate username format
        if (!IsValidUsername(model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Username can only contain letters, numbers, and underscores.");
            return View(model);
        }
        
        // Generate magic link token with username
        var token = await CreateMagicLinkTokenAsync(emailLower, isRegistration: true, username: model.Username);
        
        // Build magic link URL
        var baseUrl = _configuration["App:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var magicLink = $"{baseUrl}/account/verify?token={token}";
        
        // Send email
        await _emailService.SendMagicLinkAsync(model.Email, magicLink, isRegistration: true);
        
        _logger.LogInformation("Magic link sent to {Email} for registration with username {Username}", model.Email, model.Username);
        
        return View("CheckEmail", new CheckEmailViewModel { Email = model.Email, IsRegistration = true });
    }

    // GET: /account/verify?token=xxx
    [HttpGet]
    public async Task<IActionResult> Verify(string token, string? returnUrl = null)
    {
        if (string.IsNullOrEmpty(token))
            return RedirectToAction("Login");
        
        var magicLink = await _db.MagicLinkTokens
            .FirstOrDefaultAsync(m => m.Token == token);
        
        if (magicLink == null || !magicLink.IsValid)
        {
            TempData["Error"] = "This link has expired or is invalid. Please request a new one.";
            return RedirectToAction("Login");
        }
        
        // Mark token as used
        magicLink.IsUsed = true;
        
        User user;
        
        if (magicLink.IsRegistration)
        {
            // Create new user
            user = new User
            {
                Email = magicLink.Email,
                Username = magicLink.Username!,
                DisplayName = magicLink.Username,
                LastLoginAt = DateTime.UtcNow
            };
            
            _db.Users.Add(user);
            _logger.LogInformation("New user registered: {Username} ({Email})", user.Username, user.Email);
        }
        else
        {
            // Find existing user
            user = await _db.Users.FirstAsync(u => u.Email == magicLink.Email);
            user.LastLoginAt = DateTime.UtcNow;
            _logger.LogInformation("User logged in: {Username} ({Email})", user.Username, user.Email);
        }
        
        await _db.SaveChangesAsync();
        
        // Sign in the user
        await SignInUserAsync(user);
        
        TempData["Success"] = magicLink.IsRegistration 
            ? "Welcome to Subscribe-Me! Your account has been created." 
            : "Welcome back!";
        
        return LocalRedirect(returnUrl ?? "/");
    }

    // POST: /account/logout
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index", "Home");
    }

    // GET: /account/access-denied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task<string> CreateMagicLinkTokenAsync(string email, bool isRegistration, string? username = null)
    {
        // Generate secure random token
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
        
        var magicLink = new MagicLinkToken
        {
            Token = token,
            Email = email,
            Username = username,
            IsRegistration = isRegistration,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        
        _db.MagicLinkTokens.Add(magicLink);
        await _db.SaveChangesAsync();
        
        return token;
    }

    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("DisplayName", user.DisplayName ?? user.Username)
        };
        
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        
        await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    private static bool IsValidUsername(string username)
    {
        if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 30)
            return false;
        
        // Reserved words that can't be usernames
        var reserved = new[] { "admin", "api", "account", "home", "login", "logout", "register", 
            "profile", "settings", "feed", "explore", "search", "chat", "messages", "notifications",
            "post", "posts", "follow", "following", "followers", "subscribe", "subscription" };
        
        if (reserved.Contains(username.ToLower()))
            return false;
        
        // Only allow letters, numbers, and underscores
        return username.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}

// View Models
public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Username is required")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 30 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public string Username { get; set; } = string.Empty;
}

public class CheckEmailViewModel
{
    public string Email { get; set; } = string.Empty;
    public bool IsRegistration { get; set; }
}
