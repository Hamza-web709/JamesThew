using System.Net;
using System.Text.RegularExpressions;
using JamesThew.Models;
using JamesThew.Services;
using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public static class TestAuth
{
    public static void ClearMessages()
    {
        lock (DevelopmentEmailSender.SentMessages)
        {
            DevelopmentEmailSender.SentMessages.Clear();
        }
    }

    public static async Task<HttpResponseMessage> RegisterAndLogin(HttpClient client, string email, string password,
        string displayName = "Test Member")
    {
        var response = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/register"),
            ["DisplayName"] = displayName,
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await CompleteEmailOtp(client, response.Headers.Location?.OriginalString, email, EmailOtpPurpose.Registration);
    }

    public static async Task<HttpResponseMessage> Login(HttpClient client, string email, string password,
        string? returnUrl = "/")
    {
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl ?? string.Empty
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        if (!location.Contains("/account/otp/email", StringComparison.OrdinalIgnoreCase))
            return response;

        return await CompleteEmailOtp(client, location, email, EmailOtpPurpose.Login);
    }

    public static async Task<HttpResponseMessage> CompleteEmailOtp(HttpClient client, string? location, string email,
        EmailOtpPurpose purpose)
    {
        Assert.False(string.IsNullOrWhiteSpace(location));
        var challengeId = ChallengeIdFromLocation(location!);
        var code = LatestOtpFor(email, challengeId);
        return await client.PostAsync("/account/otp/email", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, location!),
            ["ChallengeId"] = challengeId.ToString(),
            ["Purpose"] = purpose.ToString(),
            ["MaskedEmail"] = EmailOtpService.MaskEmail(email),
            ["Code"] = code
        }));
    }

    public static async Task CompleteBrowserEmailOtpAsync(IPage page, string email, DateTime? notBeforeUtc = null)
    {
        var challengeId = ChallengeIdFromLocation(page.Url);
        var code = LatestOtpFor(email, challengeId, notBeforeUtc);
        var boxes = page.Locator(".jt-otp-box");
        await Assertions.Expect(boxes).ToHaveCountAsync(6);
        for (var i = 0; i < code.Length; i++)
            await boxes.Nth(i).FillAsync(code[i].ToString());

        await Task.WhenAll(
            page.WaitForLoadStateAsync(LoadState.DOMContentLoaded),
            page.ClickAsync("form[action*='/account/otp/email'] button[type='submit']")
        );
    }

    public static async Task LoginBrowserMemberAsync(IPage page, string email, string password)
    {
        await page.GotoAsync("/account/login");
        await page.FillAsync("input[name='Email']", email);
        await page.FillAsync("input[name='Password']", password);
        var sentAfter = DateTime.UtcNow;
        await page.ClickAsync("form[action*='/account/login'] button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        if (page.Url.Contains("/account/otp/email", StringComparison.OrdinalIgnoreCase))
            await CompleteBrowserEmailOtpAsync(page, email, sentAfter);
        await page.WaitForURLAsync("**/account/status*");
    }

    public static async Task RegisterBrowserMemberAsync(IPage page, string displayName, string email, string password)
    {
        await page.GotoAsync("/account/register");
        await page.FillAsync("input[name='DisplayName']", displayName);
        await page.FillAsync("input[name='Email']", email);
        await page.FillAsync("input[name='Password']", password);
        await page.FillAsync("input[name='ConfirmPassword']", password);
        var sentAfter = DateTime.UtcNow;
        await page.ClickAsync("form[action*='/account/register'] button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await CompleteBrowserEmailOtpAsync(page, email, sentAfter);
        await page.WaitForURLAsync("**/account/status*");
    }

    public static async Task CompleteBrowserDemoPaymentOtpAsync(IPage page, string code = "1234")
    {
        var boxes = page.Locator(".jt-otp-box");
        await Assertions.Expect(boxes).ToHaveCountAsync(4);
        for (var i = 0; i < code.Length; i++)
            await boxes.Nth(i).FillAsync(code[i].ToString());

        await Task.WhenAll(
            page.WaitForLoadStateAsync(LoadState.DOMContentLoaded),
            page.ClickAsync("form[action*='/membership/payment-otp'] button[type='submit']")
        );
    }

    public static string LatestOtpFor(string email, int? challengeId = null, DateTime? notBeforeUtc = null)
    {
        DevelopmentEmailMessage? message;
        lock (DevelopmentEmailSender.SentMessages)
        {
            var messages = DevelopmentEmailSender.SentMessages
                .Where(x => string.Equals(x.ToEmail, email, StringComparison.OrdinalIgnoreCase))
                .Where(x => challengeId is null || x.HtmlBody.Contains($"challenge:{challengeId}", StringComparison.Ordinal))
                .Where(x => notBeforeUtc is null || x.SentAtUtc >= notBeforeUtc.Value.AddSeconds(-1));
            message = notBeforeUtc is null
                ? messages.OrderByDescending(x => x.SentAtUtc).FirstOrDefault()
                : messages.OrderBy(x => x.SentAtUtc).FirstOrDefault();
        }

        Assert.NotNull(message);
        var match = Regex.Match(message!.HtmlBody, @"\b\d{6}\b");
        Assert.True(match.Success, "Expected a six-digit OTP in the development email test sink.");
        return match.Value;
    }

    public static int ChallengeIdFromLocation(string location)
    {
        var queryIndex = location.IndexOf('?', StringComparison.Ordinal);
        Assert.True(queryIndex >= 0, $"Expected query string in OTP redirect: {location}");
        var query = location[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in query)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(WebUtility.UrlDecode(parts[0]), "challengeId", StringComparison.OrdinalIgnoreCase))
                return int.Parse(WebUtility.UrlDecode(parts[1]));
        }

        throw new InvalidOperationException($"No challengeId query value found in {location}.");
    }

    public static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Expected an antiforgery token in the rendered form at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
