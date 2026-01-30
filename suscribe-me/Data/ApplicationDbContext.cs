using Microsoft.EntityFrameworkCore;
using suscribe_me.Models;

namespace suscribe_me.Data;

/// <summary>
/// Application database context.
/// Configured to use SQLite in development, SQL Server in production.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostPurchase> PostPurchases => Set<PostPurchase>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(100);
            entity.Property(u => u.Bio).HasMaxLength(2000);
        });

        // MagicLinkToken configuration
        modelBuilder.Entity<MagicLinkToken>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.Token).IsUnique();
            entity.Property(m => m.Token).HasMaxLength(128).IsRequired();
            entity.Property(m => m.Email).HasMaxLength(255).IsRequired();
            entity.Property(m => m.Username).HasMaxLength(50);
        });

        // Post configuration
        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).HasMaxLength(200).IsRequired();
            entity.Property(p => p.PreviewText).HasMaxLength(500);
            
            entity.HasOne(p => p.Author)
                  .WithMany(u => u.Posts)
                  .HasForeignKey(p => p.AuthorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PostPurchase configuration
        modelBuilder.Entity<PostPurchase>(entity =>
        {
            entity.HasKey(pp => pp.Id);
            entity.HasIndex(pp => new { pp.UserId, pp.PostId }).IsUnique();
            
            entity.HasOne(pp => pp.User)
                  .WithMany()
                  .HasForeignKey(pp => pp.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(pp => pp.Post)
                  .WithMany(p => p.Purchases)
                  .HasForeignKey(pp => pp.PostId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Follow configuration
        modelBuilder.Entity<Follow>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.FollowerId, f.FollowedId }).IsUnique();
            
            entity.HasOne(f => f.Follower)
                  .WithMany(u => u.Following)
                  .HasForeignKey(f => f.FollowerId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(f => f.Followed)
                  .WithMany(u => u.Followers)
                  .HasForeignKey(f => f.FollowedId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.SubscriberId, s.CreatorId }).IsUnique();
            
            entity.HasOne(s => s.Subscriber)
                  .WithMany(u => u.Subscriptions)
                  .HasForeignKey(s => s.SubscriberId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(s => s.Creator)
                  .WithMany(u => u.Subscribers)
                  .HasForeignKey(s => s.CreatorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Favorite configuration
        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.UserId, f.PostId }).IsUnique();
            
            entity.HasOne(f => f.User)
                  .WithMany(u => u.Favorites)
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(f => f.Post)
                  .WithMany(p => p.Favorites)
                  .HasForeignKey(f => f.PostId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.SenderId, c.ReceiverId, c.SentAt });
            
            entity.Property(c => c.Content).HasMaxLength(4000).IsRequired();
            
            entity.HasOne(c => c.Sender)
                  .WithMany(u => u.SentMessages)
                  .HasForeignKey(c => c.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(c => c.Receiver)
                  .WithMany(u => u.ReceivedMessages)
                  .HasForeignKey(c => c.ReceiverId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
