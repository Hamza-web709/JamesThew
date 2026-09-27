using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class FoundationTests(FoundationFixture fixture)
{
    private static string NewEmail() => Guid.NewGuid().ToString("N") + "@example.test";
    // Ephemeral credentials are generated per test and are never persisted in source/logs.
    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20)) + "a!9";

    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Expected an antiforgery token in the rendered form.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> Register(HttpClient client, string email, string password,
        string displayName = "Test member", string? requestedRole = null)
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/register"),
            ["DisplayName"] = displayName, ["Email"] = email,
            ["Password"] = password, ["ConfirmPassword"] = password
        };
        if (requestedRole is not null)
        {
            values["Role"] = requestedRole;
            values["EmailConfirmed"] = "true";
            values["IsActive"] = "true";
        }
        return await client.PostAsync("/account/register", new FormUrlEncodedContent(values));
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string email, string password,
        string returnUrl = "/account/status") => await client.PostAsync("/account/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await Token(client, "/account/login"),
                ["Email"] = email, ["Password"] = password, ["ReturnUrl"] = returnUrl
            }));

    private static async Task Logout(HttpClient client)
    {
        var response = await client.PostAsync("/account/logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token(client, "/account/status") }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Migration_applies_to_sql_server_and_contains_only_foundation_tables()
    {
        await using var db = fixture.CreateDb();
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync(); // Repeating the documented migration is safe.
        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'").ToListAsync();
        Assert.All(tables, name => Assert.True(name.StartsWith("AspNet") || name == "__EFMigrationsHistory"));
    }

    [Fact]
    public async Task Role_setup_is_idempotent()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
        await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.Equal(2, await roles.Roles.CountAsync());
        Assert.True(await roles.RoleExistsAsync(AppRoles.Member));
        Assert.True(await roles.RoleExistsAsync(AppRoles.Admin));
    }

    [Fact]
    public async Task Anonymous_visitors_can_browse_home_but_protected_routes_challenge_login()
    {
        using var client = fixture.NewClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        foreach (var path in new[] { "/account/status", "/admin" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/account/login", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task Registration_hashes_password_and_ignores_privileged_fields()
    {
        using var client = fixture.NewClient();
        var email = NewEmail(); var password = NewPassword();
        var response = await Register(client, email, password, "  Test member  ", AppRoles.Admin);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/status", response.Headers.Location!.ToString());
        var cookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.Equal("Test member", user.DisplayName);
        Assert.False(user.EmailConfirmed);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(user, password));
        Assert.Equal(new[] { AppRoles.Member }, await users.GetRolesAsync(user));
        Assert.Contains("does not grant paid membership", await client.GetStringAsync("/account/status"));
    }

    [Fact]
    public async Task Case_insensitive_duplicate_email_does_not_create_another_account()
    {
        var email = NewEmail();
        using var first = fixture.NewClient(); using var second = fixture.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await Register(first, email, NewPassword())).StatusCode);
        var duplicate = await Register(second, email.ToUpperInvariant(), NewPassword());
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Unable to register", await duplicate.Content.ReadAsStringAsync());
        await using var db = fixture.CreateDb();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Invalid_registration_is_rejected_server_side_without_a_user()
    {
        using var client = fixture.NewClient(); var email = NewEmail();
        var response = await Register(client, email, "x", " ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateDb();
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
    }

    [Theory]
    [InlineData("/account/register")]
    [InlineData("/account/login")]
    public async Task Authentication_posts_require_antiforgery(string path)
    {
        using var client = fixture.NewClient();
        var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_requires_post_and_antiforgery_and_removes_access()
    {
        using var client = fixture.NewClient();
        await Register(client, NewEmail(), NewPassword());
        // Conventional-route fallback may report 404 rather than 405; GET must never log out.
        Assert.Contains((await client.GetAsync("/account/logout")).StatusCode,
            new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/account/logout",
            new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/account/status")).StatusCode);
        await Logout(client);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/account/status")).StatusCode);
    }

    [Theory]
    [InlineData("https://example.test/steal")]
    [InlineData("//example.test/steal")]
    public async Task Login_rejects_external_return_urls(string returnUrl)
    {
        using var client = fixture.NewClient(); var email = NewEmail(); var password = NewPassword();
        await Register(client, email, password); await Logout(client);
        var result = await Login(client, email, password, returnUrl);
        Assert.Equal("/account/status", result.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Member_cannot_access_admin_and_login_preserves_safe_local_return_url()
    {
        using var client = fixture.NewClient(); var email = NewEmail(); var password = NewPassword();
        await Register(client, email, password); await Logout(client);
        var login = await Login(client, email, password, "/admin");
        Assert.Equal("/admin", login.Headers.Location!.ToString());
        var denied = await client.GetAsync("/admin");
        Assert.Contains("/account/access-denied", denied.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(denied.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task Invalid_login_is_generic_and_lockout_prevents_correct_password_after_five_failures()
    {
        using var client = fixture.NewClient(); var email = NewEmail(); var password = NewPassword();
        await Register(client, email, password); await Logout(client);
        for (var i = 0; i < 5; i++)
        {
            var failed = await Login(client, email, NewPassword());
            Assert.Contains("Unable to sign in", await failed.Content.ReadAsStringAsync());
        }
        var locked = await Login(client, email, password);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Contains("Unable to sign in", await locked.Content.ReadAsStringAsync());
        var unknown = await Login(client, NewEmail(), NewPassword());
        Assert.Contains("Unable to sign in", await unknown.Content.ReadAsStringAsync());
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByEmailAsync(email))!));
    }

    private static IConfiguration SeedConfig(string email, string password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        { ["LocalAdmin:Email"] = email, ["LocalAdmin:Password"] = password }).Build();

    [Fact]
    public async Task Admin_seed_is_idempotent_does_not_reset_password_and_allows_admin_login()
    {
        var email = NewEmail(); var password = NewPassword();
        await using var scope = fixture.Services.CreateAsyncScope();
        await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, SeedConfig(email, password), new TestEnvironment("Development"));
        await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, SeedConfig(email, NewPassword()), new TestEnvironment("Development"));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.True(await users.CheckPasswordAsync(user, password));
        Assert.True(await users.IsInRoleAsync(user, AppRoles.Admin));
        using var client = fixture.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await Login(client, email, password, "/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Admin_seed_refuses_to_promote_a_public_member()
    {
        var email = NewEmail(); using var client = fixture.NewClient();
        await Register(client, email, NewPassword());
        await using var scope = fixture.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => IdentitySeeder.SeedLocalAdminAsync(
            scope.ServiceProvider, SeedConfig(email, NewPassword()), new TestEnvironment("Development")));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.False(await users.IsInRoleAsync((await users.FindByEmailAsync(email))!, AppRoles.Admin));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Admin_seed_is_disabled_outside_development(string environment)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => IdentitySeeder.SeedLocalAdminAsync(
            scope.ServiceProvider, SeedConfig(NewEmail(), NewPassword()), new TestEnvironment(environment)));
    }

    [Fact]
    public async Task Admin_seed_with_missing_or_weak_password_creates_no_user()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var email = NewEmail();
        foreach (var password in new[] { string.Empty, "x" })
            await Assert.ThrowsAsync<InvalidOperationException>(() => IdentitySeeder.SeedLocalAdminAsync(
                scope.ServiceProvider, SeedConfig(email, password), new TestEnvironment("Development")));
        await using var db = fixture.CreateDb();
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
    }

    [Fact]
    public async Task SQL_backup_restores_identity_data_into_an_isolated_database()
    {
        // Include an actual password hash and role mapping even if this test runs first.
        var email = NewEmail(); var password = NewPassword();
        using var client = fixture.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await Register(client, email, password)).StatusCode);
        // Only names generated by this fixture are used in administrative SQL statements.
        var restoredName = fixture.DatabaseName + "_Restore";
        var folder = Path.Combine(Path.GetTempPath(), fixture.DatabaseName);
        Directory.CreateDirectory(folder);
        var backup = Path.Combine(folder, "identity.bak");
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = "master" };
        await using var master = new SqlConnection(connection.ConnectionString);
        await master.OpenAsync();
        async Task Execute(string sql)
        {
            await using var command = new SqlCommand(sql, master) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync();
        }
        string Quote(string value) => value.Replace("'", "''");
        try
        {
            await Execute($"BACKUP DATABASE [{fixture.DatabaseName}] TO DISK=N'{Quote(backup)}' WITH INIT, CHECKSUM");
            await Execute($"RESTORE VERIFYONLY FROM DISK=N'{Quote(backup)}' WITH CHECKSUM");
            var logicalFiles = new List<(string Name, string Type)>();
            await using (var command = new SqlCommand($"RESTORE FILELISTONLY FROM DISK=N'{Quote(backup)}'", master))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    logicalFiles.Add((reader.GetString(0), reader.GetString(2)));
            }
            var moves = logicalFiles.Select((file, index) =>
                $"MOVE N'{Quote(file.Name)}' TO N'{Quote(Path.Combine(folder, $"restore-{index}." + (file.Type == "L" ? "ldf" : "mdf")))}'");
            await Execute($"RESTORE DATABASE [{restoredName}] FROM DISK=N'{Quote(backup)}' WITH {string.Join(", ", moves)}, CHECKSUM");
            await using var restored = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = restoredName }.ConnectionString).Options);
            await using var original = fixture.CreateDb();
            Assert.Equal(await original.Users.CountAsync(), await restored.Users.CountAsync());
            Assert.Equal(await original.UserRoles.CountAsync(), await restored.UserRoles.CountAsync());
            Assert.Equal(2, await restored.Roles.CountAsync());
            var originalUser = await original.Users.SingleAsync(x => x.Email == email);
            var restoredUser = await restored.Users.SingleAsync(x => x.Email == email);
            Assert.Equal(originalUser.PasswordHash, restoredUser.PasswordHash);
            Assert.True(await restored.UserRoles.AnyAsync(x => x.UserId == restoredUser.Id));
            Assert.Empty(await restored.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await Execute($"IF DB_ID(N'{restoredName}') IS NOT NULL BEGIN ALTER DATABASE [{restoredName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{restoredName}]; END");
            // Delete only the exact scratch backup this test created, never recurse over user paths.
            if (File.Exists(backup)) File.Delete(backup);
        }
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "JamesThew.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
