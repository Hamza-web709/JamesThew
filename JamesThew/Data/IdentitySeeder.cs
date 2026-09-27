using System.ComponentModel.DataAnnotations;
using JamesThew.Authorization;
using JamesThew.Models;
using Microsoft.AspNetCore.Identity;

namespace JamesThew.Data;

public static class IdentitySeeder
{
    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        // Role names are safe reference data, not privileged user credentials.
        foreach (var name in new[] { AppRoles.Member, AppRoles.Admin })
        {
            if (!await roles.RoleExistsAsync(name))
            {
                var result = await roles.CreateAsync(new IdentityRole(name));
                if (!result.Succeeded && !await roles.RoleExistsAsync(name))
                    throw new InvalidOperationException("Identity role setup failed.");
            }
        }
    }

    public static async Task SeedLocalAdminAsync(IServiceProvider services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        // Explicit command + Development environment are both required.
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Local admin seeding is available only in Development.");

        var email = configuration["LocalAdmin:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 ||
            !new EmailAddressAttribute().IsValid(email))
            throw new InvalidOperationException("Set a valid LocalAdmin:Email using User Secrets.");

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Never promote a public registration or reset an existing password implicitly.
            if (!await users.IsInRoleAsync(existing, AppRoles.Admin))
                throw new InvalidOperationException("Refusing to promote an existing non-admin account. Use a new local admin email.");
            return;
        }

        var password = configuration["LocalAdmin:Password"];
        if (string.IsNullOrEmpty(password) || password.Length > 128)
            throw new InvalidOperationException("Set LocalAdmin:Password using User Secrets (12-128 characters).");

        var db = services.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await SeedRolesAsync(services);
        var admin = new ApplicationUser { UserName = email, Email = email, DisplayName = "Local administrator" };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
            throw new InvalidOperationException("Local admin creation failed. Check the email and configured password policy.");
        var assigned = await users.AddToRoleAsync(admin, AppRoles.Admin);
        if (!assigned.Succeeded)
            throw new InvalidOperationException("Local admin role assignment failed.");
        await transaction.CommitAsync();
    }
}
