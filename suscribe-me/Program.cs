using Microsoft.EntityFrameworkCore;
using suscribe_me.Data;
using suscribe_me.Hubs;
using suscribe_me.Services;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════
// DATABASE CONFIGURATION
// ═══════════════════════════════════════════════════════════════
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

if (builder.Environment.IsDevelopment())
{
    // SQLite for local development
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    // Azure SQL Server for production
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));
    
    // ALTERNATIVE: PostgreSQL for production
    // builder.Services.AddDbContext<ApplicationDbContext>(options =>
    //     options.UseNpgsql(connectionString, npgsqlOptions =>
    //     {
    //         npgsqlOptions.EnableRetryOnFailure(5);
    //     }));
}

// ═══════════════════════════════════════════════════════════════
// AUTHENTICATION (Cookie-based session)
// ═══════════════════════════════════════════════════════════════
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════════
// EMAIL SERVICE
// ═══════════════════════════════════════════════════════════════
if (builder.Environment.IsDevelopment())
{
    // Mock email service logs to console in development
    builder.Services.AddSingleton<IEmailService, MockEmailService>();
}
else
{
    // Azure Communication Services in production
    builder.Services.AddSingleton<IEmailService, AzureEmailService>();
}

// ═══════════════════════════════════════════════════════════════
// PAYMENT SERVICE (Stripe)
// ═══════════════════════════════════════════════════════════════
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(stripeSecretKey))
{
    // Mock payment service when Stripe is not configured
    builder.Services.AddSingleton<IPaymentService, MockPaymentService>();
}
else
{
    // Stripe payment service (works with test keys in development)
    builder.Services.AddSingleton<IPaymentService, StripePaymentService>();
}

// ═══════════════════════════════════════════════════════════════
// BLOB STORAGE
// ═══════════════════════════════════════════════════════════════
var blobConnectionString = builder.Configuration["Azure:BlobStorage:ConnectionString"];
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(blobConnectionString))
{
    // Local file storage in development
    builder.Services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();
}
else
{
    // Azure Blob Storage in production
    builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
}

// ═══════════════════════════════════════════════════════════════
// SIGNALR (Real-time chat)
// ═══════════════════════════════════════════════════════════════
var signalRConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
if (builder.Environment.IsDevelopment() || string.IsNullOrEmpty(signalRConnectionString))
{
    // Local SignalR for development
    builder.Services.AddSignalR();
}
else
{
    // Azure SignalR Service for production (auto-scaling)
    builder.Services.AddSignalR()
        .AddAzureSignalR(signalRConnectionString);
}

// ═══════════════════════════════════════════════════════════════
// OTHER SERVICES
// ═══════════════════════════════════════════════════════════════
builder.Services.AddSingleton<IMarkdownService, MarkdigService>();

// Add MVC with views
builder.Services.AddControllersWithViews();

// Add HttpContextAccessor for accessing current user
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════
// DATABASE INITIALIZATION
// ═══════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    if (app.Environment.IsDevelopment())
    {
        // Auto-create database in development
        db.Database.EnsureCreated();
    }
    else
    {
        // Apply migrations in production
        db.Database.Migrate();
    }
}

// ═══════════════════════════════════════════════════════════════
// MIDDLEWARE PIPELINE
// ═══════════════════════════════════════════════════════════════
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// ═══════════════════════════════════════════════════════════════
// ROUTE CONFIGURATION
// ═══════════════════════════════════════════════════════════════

// Map SignalR hub for real-time chat
app.MapHub<ChatHub>("/hubs/chat");

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Profile route (must be last to avoid catching other routes)
app.MapControllerRoute(
    name: "profile",
    pattern: "{username}",
    defaults: new { controller = "Profile", action = "Index" });

app.Run();
