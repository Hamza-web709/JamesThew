using JamesThew.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<Tip> Tips => Set<Tip>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<SubscriptionRequest> SubscriptionRequests => Set<SubscriptionRequest>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Retain Identity's keys/relationships and enforce unique email in SQL as well.
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            user.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex")
                .IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        });

        builder.Entity<ContentItem>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            entity.Property(x => x.AuthorDisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.ContributorNotes).HasMaxLength(1000);
            entity.Property(x => x.ImageUrl).HasMaxLength(300);

            entity.HasOne(x => x.AuthorUser)
                .WithMany(u => u.ContributedContentItems)
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Recipe)
                .WithOne(x => x.ContentItem)
                .HasForeignKey<Recipe>(x => x.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Tip)
                .WithOne(x => x.ContentItem)
                .HasForeignKey<Tip>(x => x.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Recipe>(entity =>
        {
            entity.HasMany(x => x.Ingredients)
                .WithOne(x => x.Recipe)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Steps)
                .WithOne(x => x.Recipe)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RecipeIngredient>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.QuantityText).HasMaxLength(50);
            entity.Property(x => x.Unit).HasMaxLength(30);
            entity.HasIndex(x => new { x.RecipeId, x.Position });
        });

        builder.Entity<RecipeStep>(entity =>
        {
            entity.Property(x => x.Instruction).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.RecipeId, x.Position });
        });

        builder.Entity<Tip>(entity =>
        {
            entity.Property(x => x.Body).HasMaxLength(10000).IsRequired();
        });

        builder.Entity<FaqItem>(entity =>
        {
            entity.Property(x => x.QuestionKey).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.QuestionKey).IsUnique();
            entity.Property(x => x.Question).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Answer).HasMaxLength(2000).IsRequired();
        });

        builder.Entity<SubscriptionRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.AdminNotes).HasMaxLength(500);
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasOne(x => x.User)
                .WithMany(u => u.SubscriptionRequests)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Feedback>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.AdminNotes).HasMaxLength(500);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasOne(x => x.AuthorUser)
                .WithMany(u => u.Feedbacks)
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

