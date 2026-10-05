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
    public DbSet<EmailOtpChallenge> EmailOtpChallenges => Set<EmailOtpChallenge>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Contest> Contests => Set<Contest>();
    public DbSet<ContestEntry> ContestEntries => Set<ContestEntry>();
    public DbSet<ContestEntryIngredient> ContestEntryIngredients => Set<ContestEntryIngredient>();
    public DbSet<ContestEntryStep> ContestEntrySteps => Set<ContestEntryStep>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Retain Identity's keys/relationships and enforce unique email in SQL as well.
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            user.Property(x => x.IsDemoAdminOtpBypass).HasDefaultValue(false);
            user.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex")
                .IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        });

        builder.Entity<EmailOtpChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CodeSalt).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReturnUrl).HasMaxLength(300);
            entity.HasIndex(x => new { x.UserId, x.Purpose, x.ConsumedAtUtc });
            entity.HasIndex(x => x.ExpiresAtUtc);
            entity.HasOne(x => x.User)
                .WithMany(u => u.EmailOtpChallenges)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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

        builder.Entity<Contest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DescriptionAndRules).HasMaxLength(10000).IsRequired();
            entity.Property(x => x.PrizeDescription).HasMaxLength(500);
            entity.Property(x => x.ImageUrl).HasMaxLength(300);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.OpensAtUtc);
            entity.HasIndex(x => x.ClosesAtUtc);
            entity.HasIndex(x => x.WinnerAnnouncedAtUtc);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WinningEntry)
                .WithMany()
                .HasForeignKey(x => x.WinningEntryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WinnerSelectedByUser)
                .WithMany()
                .HasForeignKey(x => x.WinnerSelectedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WinnerAnnouncedByUser)
                .WithMany()
                .HasForeignKey(x => x.WinnerAnnouncedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.WinnerRevocationReason).HasMaxLength(1000);
            entity.HasOne(x => x.WinnerRevokedByUser)
                .WithMany()
                .HasForeignKey(x => x.WinnerRevokedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContestEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            entity.Property(x => x.TipBody).HasMaxLength(10000);
            entity.Property(x => x.ContributorNotes).HasMaxLength(1000);
            entity.Property(x => x.ImageUrl).HasMaxLength(300);
            entity.Property(x => x.AdminReviewNotes).HasMaxLength(2000);
            entity.Property(x => x.DisqualificationReason).HasMaxLength(1000);
            entity.Property(x => x.RevocationReason).HasMaxLength(1000);

            // One entry per member per contest
            entity.HasIndex(x => new { x.ContestId, x.AuthorUserId }).IsUnique();

            // Relational single winner guard: maximum one entry per contest can have Status == Selected (4)
            entity.HasIndex(x => x.ContestId)
                .IsUnique()
                .HasFilter("[Status] = 4")
                .HasDatabaseName("IX_ContestEntries_ContestId_SingleWinner");

            entity.HasIndex(x => x.ContestId);
            entity.HasIndex(x => x.AuthorUserId);
            entity.HasIndex(x => x.SubmittedAtUtc);
            entity.HasIndex(x => x.Status);

            entity.HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Contest)
                .WithMany(c => c.Entries)
                .HasForeignKey(x => x.ContestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.AuthorUser)
                .WithMany(u => u.ContestEntries)
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Ingredients)
                .WithOne(x => x.ContestEntry)
                .HasForeignKey(x => x.ContestEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Steps)
                .WithOne(x => x.ContestEntry)
                .HasForeignKey(x => x.ContestEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ContestEntryIngredient>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.QuantityText).HasMaxLength(50);
            entity.Property(x => x.Unit).HasMaxLength(30);
            entity.HasIndex(x => new { x.ContestEntryId, x.Position });
        });

        builder.Entity<ContestEntryStep>(entity =>
        {
            entity.Property(x => x.Instruction).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.ContestEntryId, x.Position });
        });
    }
}

