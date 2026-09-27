using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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

        // 2. Build and start dedicated Kestrel web application on dynamic port
        var projectDir = FindProjectDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ContentRootPath = projectDir,
            WebRootPath = Path.Combine(projectDir, "wwwroot")
        });

        // Set test configuration
        builder.Configuration["ConnectionStrings:DefaultConnection"] = ConnectionString;
        builder.Configuration["Media:UploadPath"] = TestUploadDir;
        builder.Configuration["DisableHttpsRedirection"] = "true";

        // Bind Kestrel to dynamic port on loopback
        builder.WebHost.UseKestrel(k => k.Listen(System.Net.IPAddress.Loopback, 0));

        // Register application services
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ConnectionString));
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedAccount = false;
        }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.AccessDeniedPath = "/account/access-denied";
            options.Cookie.Name = $"JamesThew.QA.{RunId}";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.MemberAccount, policy =>
                policy.RequireAuthenticatedUser().RequireRole(AppRoles.Member, AppRoles.Admin));
            options.AddPolicy(AppPolicies.AdminOnly, policy =>
                policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        });

        builder.Services.AddScoped<IContentService, ContentService>();
        builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
        builder.Services.AddScoped<IFeedbackService, FeedbackService>();
        builder.Services.AddScoped<IContributionService, ContributionService>();
        builder.Services.AddScoped<IAdminContentService, AdminContentService>();
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddControllersWithViews(options =>
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(JamesThew.Controllers.HomeController).Assembly);

        _app = builder.Build();

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

        // Configure pipeline
        _app.UseStaticFiles();

        if (Directory.Exists(TestUploadDir))
        {
            _app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(TestUploadDir),
                RequestPath = "/uploads/editorial"
            });
        }

        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();

        _app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        ServerAddress = addresses!.Addresses.First();
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        // Drop isolated test database
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
        catch { }

        // Remove isolated test upload directory
        try
        {
            if (Directory.Exists(TestUploadDir))
            {
                Directory.Delete(TestUploadDir, recursive: true);
            }
        }
        catch { }
    }
}
