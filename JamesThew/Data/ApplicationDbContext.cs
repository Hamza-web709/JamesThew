using JamesThew.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
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
    }
}
