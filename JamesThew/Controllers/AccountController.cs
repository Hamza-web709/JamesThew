using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Controllers;

[Route("account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController(UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, ApplicationDbContext db,
    ISubscriptionService subscriptionService) : Controller
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
            UserName = model.Email, Email = model.Email, DisplayName = model.DisplayName
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

        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction(nameof(Status));
    }

    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => User.Identity?.IsAuthenticated == true
        ? RedirectAfterLogin(returnUrl) : View(new LoginViewModel { ReturnUrl = SafeLocalReturnUrl(returnUrl) });

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Identity handles password hashes and lockout; no account-existence details leak.
        var result = await signIn.PasswordSignInAsync(model.Email, model.Password,
            model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await users.FindByEmailAsync(model.Email);
            return await RedirectAfterLoginAsync(model.ReturnUrl, user);
        }

        ModelState.AddModelError(string.Empty, "Unable to sign in. Check your details or try again later.");
        return View(model);
    }

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
}
