using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JamesThew.Tests;

public class BrowserTestServer : IAsyncLifetime
{
    private WebApplication? _app;
    public string RunId { get; } = Guid.NewGuid().ToString("N")[..8];
    public string DatabaseName => $"JamesThew_BrowserQA_{RunId}";
    public string ConnectionString => $@"Server=(localdb)\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
    public string TestUploadDir { get; }
    public string ServerAddress { get; private set; } = string.Empty;

    public BrowserTestServer()
    {
        TestUploadDir = Path.Combine(Path.GetTempPath(), $"JamesThew_QA_Uploads_{RunId}");
    }

    private static string FindProjectDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "JamesThew");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "JamesThew.csproj")))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return Path.GetFullPath("JamesThew");
    }

    public async Task InitializeAsync()
    {
        if (!Directory.Exists(TestUploadDir))
        {
            Directory.CreateDirectory(TestUploadDir);
        }

        // 1. Create and migrate dedicated test database
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(dbOptions))
        {
            await db.Database.MigrateAsync();
        }

        // 2. Build dedicated Kestrel web application using Program.CreateApp
        // Browser tests exercise the actual application routes/middleware pipeline, not a duplicate app.
        var projectDir = FindProjectDirectory();
        var args = new[]
        {
            "--environment", "Development",
            "--urls", "http://127.0.0.1:0",
            "--contentRoot", projectDir,
            "--ConnectionStrings:DefaultConnection", ConnectionString,
            "--Media:UploadPath", TestUploadDir,
            "--DisableHttpsRedirection", "true",
            "--skip-startup-seed"
        };

        _app = Program.CreateApp(args);

        // Seed roles, demo content, test admin and test member
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ContentSeeder.SeedContentAsync(db);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminUser = await userManager.FindByEmailAsync("admin@jamesthew.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@jamesthew.com",
                    Email = "admin@jamesthew.com",
                    DisplayName = "QA Admin",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(adminUser, "Admin@Pass1234!");
                await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
            }

            var memberUser = await userManager.FindByEmailAsync("member@jamesthew.com");
            if (memberUser == null)
            {
                memberUser = new ApplicationUser
                {
                    UserName = "member@jamesthew.com",
                    Email = "member@jamesthew.com",
                    DisplayName = "QA Member",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(memberUser, "Member@Pass1234!");
                await userManager.AddToRoleAsync(memberUser, AppRoles.Member);
            }
        }

        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        ServerAddress = addresses!.Addresses.First();
    }

    public static void AssertSafeTestDatabase(string databaseName, string runId)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Test database name cannot be null or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new InvalidOperationException("Test runId cannot be null or whitespace.");
        }

        var expectedPrefix = "JamesThew_BrowserQA_";
        if (!databaseName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !databaseName.Equals($"{expectedPrefix}{runId}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to delete database '{databaseName}'. It does not match the expected ephemeral test database name '{expectedPrefix}{runId}'.");
        }

        if (databaseName.Equals("JamesThew_Development", StringComparison.OrdinalIgnoreCase) ||
            databaseName.Equals("JamesThew", StringComparison.OrdinalIgnoreCase) ||
            databaseName.Equals("master", StringComparison.OrdinalIgnoreCase) ||
            databaseName.Equals("model", StringComparison.OrdinalIgnoreCase) ||
            databaseName.Equals("msdb", StringComparison.OrdinalIgnoreCase) ||
            databaseName.Equals("tempdb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"CRITICAL: Database '{databaseName}' is protected and must never be deleted.");
        }
    }

    public static void AssertSafeTestUploadDirectory(string uploadDir, string runId)
    {
        if (string.IsNullOrWhiteSpace(uploadDir))
        {
            throw new InvalidOperationException("Test upload directory path cannot be null or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new InvalidOperationException("Test runId cannot be null or whitespace.");
        }

        var fullPath = Path.GetFullPath(uploadDir);
        var tempPath = Path.GetFullPath(Path.GetTempPath());

        // Must reside strictly inside the system temporary folder
        if (!fullPath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to delete directory '{fullPath}'. It is not within the system temporary folder '{tempPath}'.");
        }

        // Folder name must strictly match the unique test run folder
        var folderName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var expectedFolderName = $"JamesThew_QA_Uploads_{runId}";
        if (!string.Equals(folderName, expectedFolderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to delete directory '{fullPath}'. Folder name '{folderName}' does not match expected test pattern '{expectedFolderName}'.");
        }

        // Explicit safeguard against repository or project asset directories
        if (fullPath.Contains("source\\repos", StringComparison.OrdinalIgnoreCase) ||
            fullPath.Contains("source/repos", StringComparison.OrdinalIgnoreCase) ||
            fullPath.Contains("wwwroot", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CRITICAL: Directory '{fullPath}' appears to be within project workspace and must never be deleted.");
        }
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            try
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
            catch { }
        }

        // Drop isolated test database with strict safety guard
        try
        {
            AssertSafeTestDatabase(DatabaseName, RunId);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Transient database cleanup failures swallowed, but safety guard violations throw
        }

        // Remove isolated test upload directory with strict safety guard
        try
        {
            if (Directory.Exists(TestUploadDir))
            {
                AssertSafeTestUploadDirectory(TestUploadDir, RunId);
                Directory.Delete(TestUploadDir, recursive: true);
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Transient file lock cleanup failures swallowed, but safety guard violations throw
        }
    }
}
