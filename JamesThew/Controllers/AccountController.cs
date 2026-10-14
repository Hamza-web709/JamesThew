using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Controllers;

[Route("account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController(UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, ApplicationDbContext db,
    ISubscriptionService subscriptionService, IEmailOtpService emailOtpService,
    IDataProtectionProvider dataProtectionProvider) : Controller
{
    [AllowAnonymous, HttpGet("register")]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Status)) : View(new RegisterViewModel());

    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Status));
        if (!ModelState.IsValid)
            return View(model);

        // User creation and the fixed Member role are one transaction.
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            EmailConfirmed = false
        };
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var created = await users.CreateAsync(user, model.Password);
            if (!created.Succeeded)
            {
                AddRegistrationErrors(created);
                return View(model);
            }
            var assigned = await users.AddToRoleAsync(user, AppRoles.Member);
            if (!assigned.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Unable to create the account. Please try again.");
                return View(model);
            }
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Concurrent duplicate registration must not surface a database exception.
            ModelState.AddModelError(string.Empty, "Unable to register with these details. Try signing in instead.");
            return View(model);
        }

        var otp = await emailOtpService.SendRegistrationOtpAsync(user);
        if (!otp.Success || otp.ChallengeId is null)
        {
            await users.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, otp.Message);
            return View(model);
        }

        TempData["OtpInfo"] = "We sent a verification code to your email address.";
        return RedirectToAction(nameof(EmailOtp), new { challengeId = otp.ChallengeId, purpose = EmailOtpPurpose.Registration });
    }

    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => User.Identity?.IsAuthenticated == true
        ? RedirectAfterLogin(returnUrl) : View(new LoginViewModel { ReturnUrl = SafeLocalReturnUrl(returnUrl) });

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await users.FindByEmailAsync(model.Email);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Unable to sign in. Check your details or try again later.");
            return View(model);
        }

        if (!await users.CheckPasswordAsync(user, model.Password))
        {
            await users.AccessFailedAsync(user);
            ModelState.AddModelError(string.Empty, "Unable to sign in. Check your details or try again later.");
            return View(model);
        }

        await users.ResetAccessFailedCountAsync(user);

        if (user.IsDemoAdminOtpBypass && await users.IsInRoleAsync(user, AppRoles.Admin))
        {
            await signIn.SignInAsync(user, model.RememberMe);
            return await RedirectAfterLoginAsync(model.ReturnUrl, user);
        }

        if (!user.EmailConfirmed)
        {
            var registrationOtp = await emailOtpService.SendRegistrationOtpAsync(user);
            if (!registrationOtp.Success || registrationOtp.ChallengeId is null)
            {
                ModelState.AddModelError(string.Empty, registrationOtp.Message);
                return View(model);
            }

            TempData["OtpInfo"] = "Verify your email before signing in.";
            return RedirectToAction(nameof(EmailOtp), new { challengeId = registrationOtp.ChallengeId, purpose = EmailOtpPurpose.Registration });
        }

        var otp = await emailOtpService.SendLoginOtpAsync(user, SafeLocalReturnUrl(model.ReturnUrl), model.RememberMe);
        if (!otp.Success || otp.ChallengeId is null)
        {
            ModelState.AddModelError(string.Empty, otp.Message);
            return View(model);
        }

        TempData["OtpInfo"] = "We sent a sign-in code to your email address.";
        return RedirectToAction(nameof(EmailOtp), new { challengeId = otp.ChallengeId, purpose = EmailOtpPurpose.Login });
    }

    [AllowAnonymous, HttpGet("otp/email")]
    public async Task<IActionResult> EmailOtp(int challengeId, EmailOtpPurpose purpose)
    {
        var challenge = await db.EmailOtpChallenges.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == challengeId && x.Purpose == purpose);
        if (challenge is null || challenge.ConsumedAtUtc.HasValue)
        {
            TempData["ErrorMessage"] = "Verification session was not found. Please start again.";
            return RedirectToAction(nameof(Login));
        }

        return View(new EmailOtpViewModel
        {
            ChallengeId = challenge.Id,
            Purpose = purpose,
            MaskedEmail = EmailOtpService.MaskEmail(challenge.Email),
            ReturnUrl = challenge.ReturnUrl
        });
    }

    [AllowAnonymous, HttpPost("otp/email")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailOtp(EmailOtpViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await emailOtpService.VerifyAsync(model.ChallengeId, model.Purpose, model.Code);
        if (!result.Success || string.IsNullOrWhiteSpace(result.UserId))
        {
            ModelState.AddModelError(nameof(model.Code), result.Message);
            return View(model);
        }

        var user = await users.FindByIdAsync(result.UserId);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Account was not found. Please start again.");
            return View(model);
        }

        if (model.Purpose == EmailOtpPurpose.Registration && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var update = await users.UpdateAsync(user);
            if (!update.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Unable to confirm your email. Please try again.");
                return View(model);
            }
        }

        await signIn.SignInAsync(user, result.RememberMe);
        TempData["SuccessMessage"] = model.Purpose == EmailOtpPurpose.Registration
            ? "Your email is verified and your account is ready."
            : "You are signed in securely.";
        return await RedirectAfterLoginAsync(result.ReturnUrl, user);
    }

    [AllowAnonymous, HttpPost("otp/email/resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmailOtp(int challengeId, EmailOtpPurpose purpose)
    {
        var result = await emailOtpService.ResendAsync(challengeId, purpose);
        TempData[result.Success ? "OtpInfo" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(EmailOtp), new { challengeId, purpose });
    }

    [AllowAnonymous, HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Status)) : View(new ForgotPasswordViewModel());

    [AllowAnonymous, HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Status));

        if (!ModelState.IsValid)
            return View(model);

        var user = await users.FindByEmailAsync(model.Email);
        if (user is not null && !await users.IsLockedOutAsync(user) && !string.IsNullOrWhiteSpace(user.Email))
        {
            var otp = await emailOtpService.SendPasswordResetOtpAsync(user);
            if (!otp.Success)
            {
                // Account enumeration protection: even if delivery fails, do not reveal account state.
            }
        }

        TempData["OtpInfo"] = "If an account exists for this email, a verification code has been sent.";
        return RedirectToAction(nameof(VerifyResetOtp), new { email = model.Email });
    }

    [AllowAnonymous, HttpGet("forgot-password/verify")]
    public IActionResult VerifyResetOtp(string? email)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Status));

        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(ForgotPassword));

        return View(new VerifyResetOtpViewModel
        {
            Email = email,
            MaskedEmail = EmailOtpService.MaskEmail(email)
        });
    }

    [AllowAnonymous, HttpPost("forgot-password/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyResetOtp(VerifyResetOtpViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Status));

        if (!ModelState.IsValid)
        {
            model.MaskedEmail = EmailOtpService.MaskEmail(model.Email);
            return View(model);
        }

        var result = await emailOtpService.VerifyPasswordResetOtpAsync(model.Email, model.Code);
        if (!result.Success || string.IsNullOrWhiteSpace(result.UserId))
        {
            ModelState.AddModelError(nameof(model.Code), result.Message);
            model.MaskedEmail = EmailOtpService.MaskEmail(model.Email);
            return View(model);
        }

        var user = await users.FindByIdAsync(result.UserId);
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Code), "The verification code is incorrect.");
            model.MaskedEmail = EmailOtpService.MaskEmail(model.Email);
            return View(model);
        }

        var identityToken = await users.GeneratePasswordResetTokenAsync(user);
        var ticket = new PasswordResetAuthTicket(user.Id, user.Email ?? model.Email, identityToken, DateTime.UtcNow);
        var protector = dataProtectionProvider.CreateProtector("JamesThew.PasswordResetAuthorization");
        var protectedToken = protector.Protect(System.Text.Json.JsonSerializer.Serialize(ticket));

        return RedirectToAction(nameof(ResetPassword), new { token = protectedToken });
    }

    [AllowAnonymous, HttpPost("forgot-password/resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendResetOtp(string email)
    {
        var result = await emailOtpService.ResendPasswordResetOtpAsync(email);
        TempData[result.Success ? "OtpInfo" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(VerifyResetOtp), new { email });
    }

    [AllowAnonymous, HttpGet("reset-password")]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Status));

        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Password reset session has expired or is invalid. Please request a new code.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var ticket = UnprotectResetToken(token);
        if (ticket is null)
        {
            TempData["ErrorMessage"] = "Password reset session has expired or is invalid. Please request a new code.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var user = await users.FindByIdAsync(ticket.UserId);
        if (user is null || !string.Equals(user.Email, ticket.Email, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Password reset session has expired or is invalid. Please request a new code.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [AllowAnonymous, HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Status));

        if (!ModelState.IsValid)
            return View(model);

        var ticket = UnprotectResetToken(model.Token);
        if (ticket is null)
        {
            ModelState.AddModelError(string.Empty, "Password reset session has expired or is invalid. Please request a new code.");
            return View(model);
        }

        var user = await users.FindByIdAsync(ticket.UserId);
        if (user is null || !string.Equals(user.Email, ticket.Email, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Password reset session has expired or is invalid. Please request a new code.");
            return View(model);
        }

        var resetResult = await users.ResetPasswordAsync(user, ticket.IdentityToken, model.NewPassword);
        if (!resetResult.Succeeded)
        {
            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [AllowAnonymous, HttpGet("reset-password/confirmation")]
    public IActionResult ResetPasswordConfirmation() => View();

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize(Policy = AppPolicies.MemberAccount), HttpGet("status", Order = 1)]
    [Authorize(Policy = AppPolicies.MemberAccount), HttpGet("profile", Order = 2)]
    public async Task<IActionResult> Status()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            await signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var roles = await users.GetRolesAsync(user);
        var subStatus = await subscriptionService.GetCurrentSubscriptionStatusAsync(user.Id);

        var model = new UserProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            Roles = string.Join(", ", roles),
            SubscriptionStatus = subStatus?.HasActiveSubscription == true
                ? $"Active Subscriber ({subStatus.Plan})"
                : subStatus?.Status == SubscriptionStatus.Pending
                    ? "Subscription Pending Review"
                    : "Standard Registered Member"
        };
        return View(nameof(Status), model);
    }

    [Authorize(Policy = AppPolicies.MemberAccount), HttpPost("status", Order = 1)]
    [Authorize(Policy = AppPolicies.MemberAccount), HttpPost("profile", Order = 2)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            await signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            var roles = await users.GetRolesAsync(user);
            var subStatus = await subscriptionService.GetCurrentSubscriptionStatusAsync(user.Id);
            model.Email = user.Email ?? string.Empty;
            model.Roles = string.Join(", ", roles);
            model.SubscriptionStatus = subStatus?.HasActiveSubscription == true
                ? $"Active Subscriber ({subStatus.Plan})"
                : subStatus?.Status == SubscriptionStatus.Pending
                    ? "Subscription Pending Review"
                    : "Standard Registered Member";
            return View(nameof(Status), model);
        }

        user.DisplayName = model.DisplayName.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            var roles = await users.GetRolesAsync(user);
            var subStatus = await subscriptionService.GetCurrentSubscriptionStatusAsync(user.Id);
            model.Email = user.Email ?? string.Empty;
            model.Roles = string.Join(", ", roles);
            model.SubscriptionStatus = subStatus?.HasActiveSubscription == true
                ? $"Active Subscriber ({subStatus.Plan})"
                : subStatus?.Status == SubscriptionStatus.Pending
                    ? "Subscription Pending Review"
                    : "Standard Registered Member";
            return View(nameof(Status), model);
        }

        TempData["ProfileSuccess"] = "Your profile has been updated successfully.";
        return RedirectToAction(nameof(Status));
    }

    [AllowAnonymous, HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectAfterLogin(string? returnUrl)
    {
        var safeReturnUrl = SafeLocalReturnUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(safeReturnUrl))
            return LocalRedirect(safeReturnUrl);

        return User.IsInRole(AppRoles.Admin)
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction(nameof(Status));
    }

    private async Task<IActionResult> RedirectAfterLoginAsync(string? returnUrl, ApplicationUser? user)
    {
        var safeReturnUrl = SafeLocalReturnUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(safeReturnUrl))
            return LocalRedirect(safeReturnUrl);

        if (user is not null && await users.IsInRoleAsync(user, AppRoles.Admin))
            return RedirectToAction("Index", "Admin");

        return RedirectToAction(nameof(Status));
    }

    private string? SafeLocalReturnUrl(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    private void AddRegistrationErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            var message = error.Code.StartsWith("Password", StringComparison.Ordinal)
                ? error.Description : "Unable to register with these details. Try signing in instead.";
            ModelState.AddModelError(string.Empty, message);
        }
    }

    private PasswordResetAuthTicket? UnprotectResetToken(string token)
    {
        try
        {
            var protector = dataProtectionProvider.CreateProtector("JamesThew.PasswordResetAuthorization");
            var json = protector.Unprotect(token);
            var ticket = System.Text.Json.JsonSerializer.Deserialize<PasswordResetAuthTicket>(json);
            if (ticket is null) return null;
            if (DateTime.UtcNow - ticket.IssuedAtUtc > TimeSpan.FromMinutes(15)) return null;
            return ticket;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed record PasswordResetAuthTicket(string UserId, string Email, string IdentityToken, DateTime IssuedAtUtc);
}
