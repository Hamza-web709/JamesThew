using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var app = Program.CreateApp(args);

if (args.Contains("--seed-admin", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, app.Configuration, app.Environment);
    app.Logger.LogInformation("Local admin seed completed. No credentials are logged.");
    return;
}

app.Run();

// Expose the host to integration tests and browser QA without a second startup implementation.
public partial class Program
{
    public static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args.Where(x => x != "--seed-admin").ToArray());

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) && builder.Environment.IsDevelopment())
        {
            // Windows integrated authentication contains no database password.
            connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=JamesThew_Development;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        }
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection outside source control.");

        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            // Email delivery is not part of this local foundation. Do not pretend to confirm email.
            options.SignIn.RequireConfirmedAccount = false;
        }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.AccessDeniedPath = "/account/access-denied";
            options.Cookie.Name = "JamesThew.Identity";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
        });
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(AppPolicies.MemberAccount, policy =>
                policy.RequireAuthenticatedUser().RequireRole(AppRoles.Member, AppRoles.Admin));
            options.AddPolicy(AppPolicies.AdminOnly, policy =>
                policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        });

        // Add services to the container.
        builder.Services.AddScoped<IContentService, ContentService>();
        builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
        builder.Services.AddScoped<IFeedbackService, FeedbackService>();
        builder.Services.AddScoped<IContributionService, ContributionService>();
        builder.Services.AddScoped<IAdminContentService, AdminContentService>();
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddScoped<IContestService, ContestService>();
        builder.Services.AddScoped<IContestEntryService, ContestEntryService>();
        builder.Services.AddControllersWithViews(options =>
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(Program).Assembly);

        var app = builder.Build();

        // Schema changes are explicit CLI operations. Startup ensures the roles and demo content exist.
        if (!args.Contains("--skip-startup-seed", StringComparer.Ordinal))
        {
            using var scope = app.Services.CreateScope();
            IdentitySeeder.SeedRolesAsync(scope.ServiceProvider).GetAwaiter().GetResult();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ContentSeeder.SeedContentAsync(db).GetAwaiter().GetResult();
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        if (!builder.Configuration.GetValue<bool>("DisableHttpsRedirection"))
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();

        var customUploadPath = builder.Configuration["Media:UploadPath"];
        if (!string.IsNullOrWhiteSpace(customUploadPath))
        {
            if (!Directory.Exists(customUploadPath))
            {
                Directory.CreateDirectory(customUploadPath);
            }
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(customUploadPath),
                RequestPath = "/uploads/editorial"
            });
        }

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        var manifestFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.staticwebassets.endpoints.json");
        if (File.Exists(manifestFile))
        {
            app.MapStaticAssets(manifestFile).AllowAnonymous();
        }
        else
        {
            try
            {
                app.MapStaticAssets().AllowAnonymous();
            }
            catch (InvalidOperationException)
            {
                // Fallback for environments without staticwebassets endpoint manifest
            }
        }

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        return app;
    }
}
