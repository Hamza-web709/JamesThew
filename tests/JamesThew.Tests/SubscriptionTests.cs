using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class SubscriptionTests(FoundationFixture fixture)
{
    private static string NewEmail() => Guid.NewGuid().ToString("N") + "@example.test";
    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20)) + "a!9";

    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Expected an antiforgery token in the rendered form at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> RegisterAndLogin(HttpClient client, string email, string password, string displayName = "Test Member") =>
        await TestAuth.RegisterAndLogin(client, email, password, displayName);

    private static async Task Login(HttpClient client, string email, string password) =>
        await TestAuth.Login(client, email, password);

    private static IConfiguration SeedConfig(string email, string password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalAdmin:Email"] = email,
            ["LocalAdmin:Password"] = password
        }).Build();

    private sealed class DevEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "JamesThew";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task Guest_cannot_submit_subscription_request_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();

        // Get antiforgery token from login page
        var token = await Token(client, "/account/login");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Plan"] = "Monthly",
            ["Notes"] = "Guest attempt"
        };

        var response = await client.PostAsync("/membership/subscribe", new FormUrlEncodedContent(form));

        // Fallback or policy requires authentication; should redirect to login
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logged_in_member_can_complete_demo_checkout_and_is_marked_active()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();

        await RegisterAndLogin(client, email, password, "Culinary Subscriber");

        var checkoutToken = await Token(client, "/membership/checkout?plan=Monthly");
        var checkoutForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = checkoutToken,
            ["Plan"] = "Monthly",
            ["CardholderName"] = "Culinary Subscriber",
            ["CardNumber"] = "4242 4242 4242 4242",
            ["Expiry"] = "12/30",
            ["Cvv"] = "123"
        };

        var response = await client.PostAsync("/membership/checkout", new FormUrlEncodedContent(checkoutForm));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/membership/payment-otp", response.Headers.Location?.OriginalString);

        var otpToken = await Token(client, "/membership/payment-otp");
        var otpForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = otpToken,
            ["DemoPaymentOtp"] = "1234"
        };
        response = await client.PostAsync("/membership/payment-otp", new FormUrlEncodedContent(otpForm));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/membership", response.Headers.Location?.OriginalString);

        var membershipHtml = await client.GetStringAsync("/membership");
        Assert.Contains("Active Premium Access", membershipHtml);
        Assert.Contains("Monthly", membershipHtml);
        Assert.Contains("Demo Payment / Academic Simulation", membershipHtml);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        var sub = await db.SubscriptionRequests.FirstOrDefaultAsync(s => s.UserId == user.Id);
        Assert.NotNull(sub);
        Assert.Equal(SubscriptionStatus.Approved, sub.Status);
        Assert.Equal(SubscriptionPlan.Monthly, sub.Plan);
        Assert.Equal(10.00m, sub.Amount);
        Assert.Equal("DemoPaymentCheckout", sub.ReviewedByAdminId);
        Assert.Contains("ending 4242", sub.AdminNotes ?? string.Empty);
        Assert.DoesNotContain("4242 4242 4242 4242", sub.AdminNotes ?? string.Empty);
        Assert.DoesNotContain("CVV", sub.AdminNotes ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pending_member_cannot_access_paid_recipe_or_tip_body()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();

        await RegisterAndLogin(client, email, password, "Pending Reader");

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            db.SubscriptionRequests.Add(new SubscriptionRequest
            {
                UserId = user.Id,
                Plan = SubscriptionPlan.Yearly,
                Amount = 100.00m,
                Status = SubscriptionStatus.Pending,
                Notes = "Legacy annual demo check",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Attempt to view paid Beef Wellington recipe
        var recipeResponse = await client.GetAsync("/recipes/jamess-masterclass-beef-wellington");
        Assert.Equal(HttpStatusCode.OK, recipeResponse.StatusCode);
        var recipeCache = recipeResponse.Headers.CacheControl?.ToString() ?? "";
        Assert.Contains("no-cache", recipeCache);
        Assert.Contains("no-store", recipeCache);
        Assert.Contains("must-revalidate", recipeCache);

        var recipeHtml = await recipeResponse.Content.ReadAsStringAsync();
        Assert.Contains("jt-locked-box", recipeHtml);
        Assert.DoesNotContain("Center-cut prime beef fillet", recipeHtml);
        Assert.DoesNotContain("English mustard", recipeHtml);

        // Attempt to view paid French sauce tip
        var tipResponse = await client.GetAsync("/tips/masterclass-french-sauce-emulsions-and-pan-deglazing");
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);
        var tipCache = tipResponse.Headers.CacheControl?.ToString() ?? "";
        Assert.Contains("no-cache", tipCache);
        Assert.Contains("no-store", tipCache);
        Assert.Contains("must-revalidate", tipCache);

        var tipHtml = await tipResponse.Content.ReadAsStringAsync();
        Assert.Contains("jt-locked-box", tipHtml);
        Assert.DoesNotContain("chilled butter cubes", tipHtml);
        Assert.DoesNotContain("monter au beurre", tipHtml);
    }

    [Fact]
    public async Task Admin_can_approve_member_subscription_and_approved_member_can_view_paid_content()
    {
        // 1. Create and submit member request
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Masterclass Aspirant");

        int requestId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberUser = await db.Users.FirstAsync(u => u.Email == memberEmail);
            var sub = new SubscriptionRequest
            {
                UserId = memberUser.Id,
                Plan = SubscriptionPlan.Monthly,
                Amount = 10.00m,
                Status = SubscriptionStatus.Pending,
                Notes = "Legacy pending weekend cooking request",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.SubscriptionRequests.Add(sub);
            await db.SaveChangesAsync();
            requestId = sub.Id;
        }

        // 2. Admin logs in and approves request
        using var adminClient = fixture.NewClient();
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            var env = new DevEnv();
            var config = SeedConfig(adminEmail, adminPassword);
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, config, env);
        }
        await Login(adminClient, adminEmail, adminPassword);

        // Admin verifies the pending request in list
        var adminListHtml = await adminClient.GetStringAsync("/admin/subscriptions?status=Pending");
        Assert.Contains(memberEmail, adminListHtml);
        Assert.Contains($"#{requestId}", adminListHtml);

        // Admin posts approval
        var approveToken = await Token(adminClient, "/admin/subscriptions");
        var approveForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = approveToken,
            ["adminNotes"] = "Approved for academic testing."
        };
        var approveResponse = await adminClient.PostAsync($"/admin/subscriptions/{requestId}/approve", new FormUrlEncodedContent(approveForm));
        Assert.Equal(HttpStatusCode.Redirect, approveResponse.StatusCode);

        // 3. Member now accesses paid recipe and tip
        var recipeResponse = await memberClient.GetAsync("/recipes/jamess-masterclass-beef-wellington");
        Assert.Equal(HttpStatusCode.OK, recipeResponse.StatusCode);
        var recipeHtml = await recipeResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("jt-locked-box", recipeHtml);
        Assert.Contains("Center-cut prime beef fillet", recipeHtml);
        Assert.Contains("English mustard", recipeHtml);

        var tipResponse = await memberClient.GetAsync("/tips/masterclass-french-sauce-emulsions-and-pan-deglazing");
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);
        var tipHtml = await tipResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("jt-locked-box", tipHtml);
        Assert.Contains("Monter au Beurre", tipHtml);
        Assert.Contains("nappe consistency", tipHtml);
    }

    [Fact]
    public async Task Admin_can_reject_member_subscription_and_rejected_member_sees_locked_preview()
    {
        // 1. Create and submit member request
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Reject Test Member");

        int requestId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberUser = await db.Users.FirstAsync(u => u.Email == memberEmail);
            var sub = new SubscriptionRequest
            {
                UserId = memberUser.Id,
                Plan = SubscriptionPlan.Monthly,
                Amount = 10.00m,
                Status = SubscriptionStatus.Pending,
                Notes = "Legacy pending rejection path",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.SubscriptionRequests.Add(sub);
            await db.SaveChangesAsync();
            requestId = sub.Id;
        }

        // 2. Admin logs in and rejects request
        using var adminClient = fixture.NewClient();
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            var env = new DevEnv();
            var config = SeedConfig(adminEmail, adminPassword);
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, config, env);
        }
        await Login(adminClient, adminEmail, adminPassword);

        var rejectToken = await Token(adminClient, "/admin/subscriptions");
        var rejectForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = rejectToken,
            ["adminNotes"] = "Demo request rejected due to invalid demo voucher reference."
        };
        var rejectResponse = await adminClient.PostAsync($"/admin/subscriptions/{requestId}/reject", new FormUrlEncodedContent(rejectForm));
        Assert.Equal(HttpStatusCode.Redirect, rejectResponse.StatusCode);

        // 3. Member checks membership page: shows rejected with reason
        var membershipHtml = await memberClient.GetStringAsync("/membership");
        Assert.Contains("Request Not Approved", membershipHtml);
        Assert.Contains("invalid demo voucher reference", membershipHtml);

        // 4. Member still sees locked box on paid content
        var recipeHtml = await memberClient.GetStringAsync("/recipes/jamess-masterclass-beef-wellington");
        Assert.Contains("jt-locked-box", recipeHtml);
        Assert.DoesNotContain("Center-cut prime beef fillet", recipeHtml);
    }

    [Fact]
    public async Task Admin_always_bypasses_locked_content_without_active_personal_subscription()
    {
        using var adminClient = fixture.NewClient();
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            var env = new DevEnv();
            var config = SeedConfig(adminEmail, adminPassword);
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, config, env);
        }
        await Login(adminClient, adminEmail, adminPassword);

        // Admin has no SubscriptionRequest in db, but Admin role bypasses locked state
        var recipeHtml = await adminClient.GetStringAsync("/recipes/jamess-masterclass-beef-wellington");
        Assert.DoesNotContain("jt-locked-box", recipeHtml);
        Assert.Contains("Center-cut prime beef fillet", recipeHtml);

        var tipHtml = await adminClient.GetStringAsync("/tips/masterclass-french-sauce-emulsions-and-pan-deglazing");
        Assert.DoesNotContain("jt-locked-box", tipHtml);
        Assert.Contains("Monter au Beurre", tipHtml);
    }
}
