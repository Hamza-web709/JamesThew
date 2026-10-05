using System.Net;
using System.Security.Cryptography;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class Phase8AOtpAndPaymentTests(FoundationFixture fixture)
{
    private static string NewEmail() => Guid.NewGuid().ToString("N") + "@example.test";
    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20)) + "a!9";

    [Theory]
    [InlineData("4242 4242 4242 4242", "Visa", true)]
    [InlineData("5555 5555 5555 4444", "Mastercard", true)]
    [InlineData("4111 1111 1111 1112", "Visa", false)]
    [InlineData("6011 1111 1111 1117", "Unknown Card", false)]
    public void Demo_card_validation_detects_network_and_luhn(string cardNumber, string network, bool valid)
    {
        var digits = DemoCardValidator.DigitsOnly(cardNumber);
        Assert.Equal(network, DemoCardValidator.DetectNetwork(digits));
        Assert.Equal(valid, DemoCardValidator.IsSupportedNetwork(network) && DemoCardValidator.IsValidLuhn(digits));
    }

    [Fact]
    public async Task Registration_otp_hashes_code_confirms_email_and_signs_in_once()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();

        var register = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/register"),
            ["DisplayName"] = "OTP Member",
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        }));

        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);
        Assert.Contains("/account/otp/email", register.Headers.Location!.OriginalString);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.EmailOtpChallenges.SingleAsync(x => x.Email == email);
            Assert.Equal(EmailOtpPurpose.Registration, challenge.Purpose);
            Assert.Equal(64, challenge.CodeHash.Length);
            Assert.DoesNotContain(TestAuth.LatestOtpFor(email, challenge.Id), challenge.CodeHash);
            Assert.False(await db.Users.Where(x => x.Email == email).Select(x => x.EmailConfirmed).SingleAsync());
        }

        var verified = await TestAuth.CompleteEmailOtp(client, register.Headers.Location!.OriginalString, email, EmailOtpPurpose.Registration);
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        Assert.Equal("/account/status", verified.Headers.Location!.OriginalString);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.EmailOtpChallenges.SingleAsync(x => x.Email == email);
            Assert.NotNull(challenge.ConsumedAtUtc);
            Assert.True(await db.Users.Where(x => x.Email == email).Select(x => x.EmailConfirmed).SingleAsync());
        }
    }

    [Fact]
    public async Task Email_otp_service_enforces_attempt_limit_expiry_single_use_and_resend_cooldown()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser { UserName = NewEmail(), Email = NewEmail(), DisplayName = "OTP Service", EmailConfirmed = true };
        var created = await users.CreateAsync(user, NewPassword());
        Assert.True(created.Succeeded);

        var started = await service.SendLoginOtpAsync(user, "/account/status", false);
        Assert.True(started.Success);
        Assert.NotNull(started.ChallengeId);

        var cooldown = await service.ResendAsync(started.ChallengeId.Value, EmailOtpPurpose.Login);
        Assert.False(cooldown.Success);
        Assert.Contains("wait", cooldown.Message, StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < EmailOtpService.MaxFailedAttempts; i++)
            Assert.False((await service.VerifyAsync(started.ChallengeId.Value, EmailOtpPurpose.Login, "000000")).Success);
        Assert.Contains("Too many", (await service.VerifyAsync(started.ChallengeId.Value, EmailOtpPurpose.Login, "111111")).Message);

        var older = await service.SendLoginOtpAsync(user, "/account/status", false);
        var olderCode = TestAuth.LatestOtpFor(user.Email!, older.ChallengeId);
        var newer = await service.SendLoginOtpAsync(user, "/account/status", false);
        Assert.Contains("not found", (await service.VerifyAsync(older.ChallengeId!.Value, EmailOtpPurpose.Login, olderCode)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.True((await service.VerifyAsync(newer.ChallengeId!.Value, EmailOtpPurpose.Login, TestAuth.LatestOtpFor(user.Email!, newer.ChallengeId))).Success);

        var fresh = await service.SendLoginOtpAsync(user, "/account/status", true);
        var challenge = await db.EmailOtpChallenges.SingleAsync(x => x.Id == fresh.ChallengeId);
        challenge.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        Assert.Contains("expired", (await service.VerifyAsync(fresh.ChallengeId!.Value, EmailOtpPurpose.Login, "123456")).Message, StringComparison.OrdinalIgnoreCase);

        var usable = await service.SendLoginOtpAsync(user, "/account/status", true);
        var code = TestAuth.LatestOtpFor(user.Email!, usable.ChallengeId);
        Assert.True((await service.VerifyAsync(usable.ChallengeId!.Value, EmailOtpPurpose.Login, code)).Success);
        Assert.Contains("not found", (await service.VerifyAsync(usable.ChallengeId.Value, EmailOtpPurpose.Login, code)).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Demo_checkout_never_stores_full_card_or_cvv_and_activates_membership_after_payment_otp()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        await TestAuth.RegisterAndLogin(client, email, NewPassword(), "Checkout Member");

        var invalid = await client.PostAsync("/membership/checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/membership/checkout?plan=Monthly"),
            ["Plan"] = "Monthly",
            ["CardholderName"] = "Checkout Member",
            ["CardNumber"] = "6011 1111 1111 1117",
            ["Expiry"] = "01/20",
            ["Cvv"] = "12"
        }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Visa or Mastercard", invalidHtml);
        Assert.Contains("future expiry", invalidHtml);
        Assert.Contains("exactly three digits", invalidHtml);

        var checkout = await client.PostAsync("/membership/checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/membership/checkout?plan=Yearly"),
            ["Plan"] = "Yearly",
            ["CardholderName"] = "Checkout Member",
            ["CardNumber"] = "5555 5555 5555 4444",
            ["Expiry"] = "12/30",
            ["Cvv"] = "987"
        }));
        Assert.Equal(HttpStatusCode.Redirect, checkout.StatusCode);
        Assert.Equal("/membership/payment-otp", checkout.Headers.Location!.OriginalString);

        var shortOtp = await client.PostAsync("/membership/payment-otp", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/membership/payment-otp"),
            ["DemoPaymentOtp"] = "123"
        }));
        Assert.Equal(HttpStatusCode.OK, shortOtp.StatusCode);

        var activated = await client.PostAsync("/membership/payment-otp", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/membership/payment-otp"),
            ["DemoPaymentOtp"] = "1234"
        }));
        Assert.Equal(HttpStatusCode.Redirect, activated.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var sub = await db.SubscriptionRequests.SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(SubscriptionStatus.Approved, sub.Status);
        Assert.Equal(SubscriptionPlan.Yearly, sub.Plan);
        Assert.Equal("DemoPaymentCheckout", sub.ReviewedByAdminId);
        Assert.Contains("ending 4444", sub.AdminNotes);
        Assert.DoesNotContain("5555 5555 5555 4444", sub.AdminNotes);
        Assert.DoesNotContain("987", sub.AdminNotes);
    }
}
