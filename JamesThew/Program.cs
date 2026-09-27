using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Keep one-shot seed commands out of the web host's configuration arguments.
var seedAdmin = args.Contains("--seed-admin", StringComparer.Ordinal);
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

if (seedAdmin)
{
    await using var scope = app.Services.CreateAsyncScope();
    await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, app.Configuration, app.Environment);
    app.Logger.LogInformation("Local admin seed completed. No credentials are logged.");
    return;
}

// Schema changes are explicit CLI operations. Startup only ensures the two role names exist.
await using (var scope = app.Services.CreateAsyncScope())
{
    await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Expose the host to integration tests without a second startup implementation.
public partial class Program { }
