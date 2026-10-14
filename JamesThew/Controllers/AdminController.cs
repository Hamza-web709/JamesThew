using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
[Route("admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminController(
    ISubscriptionService subscriptionService,
    IFeedbackService feedbackService,
    IContributionService contributionService,
    IAdminContentService adminContentService,
    IMediaService mediaService,
    IContestService contestService,
    IContestEntryService contestEntryService,
    IContestJudgingService contestJudgingService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    IEmailOtpService emailOtpService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.PendingSubscriptionsCount = await subscriptionService.GetPendingCountAsync();
        ViewBag.PendingFeedbackCount = await feedbackService.GetPendingFeedbackCountAsync();
        ViewBag.PendingContributionsCount = await contributionService.GetPendingContributionCountAsync();
        var editorialData = await adminContentService.GetEditorialContentListAsync();
        ViewBag.EditorialRecipesCount = editorialData.RecipesCount;
        ViewBag.EditorialTipsCount = editorialData.TipsCount;
        var mediaData = await mediaService.GetMediaLibraryAsync();
        ViewBag.MediaItemsCount = mediaData.TotalCount;
        ViewBag.MediaTotalSize = mediaData.FormattedTotalSize;
        ViewBag.TotalContestsCount = await contestService.GetTotalContestsCountAsync();
        ViewBag.ActiveOpenContestsCount = await contestService.GetActiveOpenCountAsync();
        return View();
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions(SubscriptionStatus? status)
    {
        var requests = await subscriptionService.GetAllRequestsAsync(status);
        var pendingCount = await subscriptionService.GetPendingCountAsync();
        var model = new SubscriptionAdminListViewModel
        {
            StatusFilter = status,
            TotalPendingCount = pendingCount,
            Requests = requests
        };
        return View(model);
    }

    [HttpPost("subscriptions/{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSubscription(int id, string? adminNotes)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var success = await subscriptionService.ApproveRequestAsync(id, adminUserId, adminNotes);
        if (success)
            TempData["SuccessMessage"] = $"Subscription request #{id} successfully approved. Paid masterclass access is now active.";
        else
            TempData["ErrorMessage"] = $"Unable to approve subscription request #{id}.";

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost("subscriptions/{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSubscription(int id, string? adminNotes)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var success = await subscriptionService.RejectRequestAsync(id, adminUserId, adminNotes);
        if (success)
            TempData["SuccessMessage"] = $"Subscription request #{id} has been rejected.";
        else
            TempData["ErrorMessage"] = $"Unable to reject subscription request #{id}.";

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(
        string? q,
        string role = "all",
        string verification = "all",
        string membership = "all",
        string status = "all",
        int page = 1)
    {
        var trimmedQ = q?.Trim();
        var normalizedRole = (role ?? "all").Trim().ToLowerInvariant();
        if (normalizedRole is not ("all" or "member" or "admin"))
            normalizedRole = "all";

        var normalizedVerification = (verification ?? "all").Trim().ToLowerInvariant();
        if (normalizedVerification is not ("all" or "verified" or "unverified"))
            normalizedVerification = "all";

        var normalizedMembership = (membership ?? "all").Trim().ToLowerInvariant();
        if (normalizedMembership is not ("all" or "active" or "inactive"))
            normalizedMembership = "all";

        var normalizedStatus = (status ?? "all").Trim().ToLowerInvariant();
        if (normalizedStatus is not ("all" or "active" or "inactive"))
            normalizedStatus = "all";

        if (page < 1) page = 1;
        const int pageSize = 15;

        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(trimmedQ))
        {
            query = query.Where(u => u.DisplayName.Contains(trimmedQ) || (u.Email != null && u.Email.Contains(trimmedQ)));
        }

        if (normalizedRole == "admin")
        {
            var adminRole = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == AppRoles.Admin);
            if (adminRole != null)
            {
                query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRole.Id));
            }
        }
        else if (normalizedRole == "member")
        {
            var memberRole = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == AppRoles.Member);
            if (memberRole != null)
            {
                query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == memberRole.Id));
            }
        }

        if (normalizedVerification == "verified")
        {
            query = query.Where(u => u.EmailConfirmed);
        }
        else if (normalizedVerification == "unverified")
        {
            query = query.Where(u => !u.EmailConfirmed);
        }

        var now = DateTime.UtcNow;
        if (normalizedMembership == "active")
        {
            query = query.Where(u => u.SubscriptionRequests.Any(s => s.Status == SubscriptionStatus.Approved && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now)));
        }
        else if (normalizedMembership == "inactive")
        {
            query = query.Where(u => !u.SubscriptionRequests.Any(s => s.Status == SubscriptionStatus.Approved && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now)));
        }

        if (normalizedStatus == "active")
        {
            query = query.Where(u => u.IsActive);
        }
        else if (normalizedStatus == "inactive")
        {
            query = query.Where(u => !u.IsActive);
        }

        var totalItems = await query.CountAsync();
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalItems / pageSize) : 0;
        if (page > totalPages && totalPages > 0)
        {
            page = totalPages;
        }

        var pagedUsers = await query
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Email)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = pagedUsers.Select(u => u.Id).ToList();

        var userRolesQuery = await (from ur in db.UserRoles.AsNoTracking()
                                    join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                    where userIds.Contains(ur.UserId)
                                    select new { ur.UserId, RoleName = r.Name })
                                   .ToListAsync();

        var rolesByUser = userRolesQuery
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Any(x => x.RoleName == AppRoles.Admin) ? AppRoles.Admin : AppRoles.Member
            );

        var subscriptions = await db.SubscriptionRequests
            .AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();

        var subsByUser = subscriptions
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var adminRoleItem = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == AppRoles.Admin);
        var totalActiveAdmins = 0;
        if (adminRoleItem != null)
        {
            totalActiveAdmins = await (from ur in db.UserRoles.AsNoTracking()
                                       join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                                       where ur.RoleId == adminRoleItem.Id && u.IsActive
                                       select u.Id).CountAsync();
        }

        var items = pagedUsers.Select(user =>
        {
            var roleName = rolesByUser.TryGetValue(user.Id, out var rName) ? rName : AppRoles.Member;
            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            var isCurrentAdmin = string.Equals(user.Id, currentAdminId, StringComparison.Ordinal);
            var isDemoAdmin = user.IsDemoAdminOtpBypass;
            var isLastActiveAdmin = roleName == AppRoles.Admin && user.IsActive && totalActiveAdmins <= 1;

            string membershipLabel = "Free / No Active Membership";
            if (subsByUser.TryGetValue(user.Id, out var userSubs))
            {
                var activeSub = userSubs.FirstOrDefault(s => s.Status == SubscriptionStatus.Approved && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now));
                if (activeSub != null)
                {
                    membershipLabel = activeSub.Plan == SubscriptionPlan.Yearly ? "Active Annual" : "Active Monthly";
                }
                else if (userSubs.Any(s => s.Status == SubscriptionStatus.Approved && s.ExpiresAtUtc <= now))
                {
                    membershipLabel = "Expired";
                }
            }

            return new AdminUserListItemViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = roleName,
                EmailConfirmed = user.EmailConfirmed,
                IsActive = user.IsActive,
                MembershipLabel = membershipLabel,
                IsTemporarilyLocked = isLocked,
                LockoutEnd = user.LockoutEnd,
                IsCurrentAdmin = isCurrentAdmin,
                IsDemoAdmin = isDemoAdmin,
                IsLastActiveAdmin = isLastActiveAdmin
            };
        }).ToList();
        var totalUsers = await db.Users.CountAsync();
        var totalAdmins = adminRoleItem != null ? await db.UserRoles.CountAsync(ur => ur.RoleId == adminRoleItem.Id) : 0;
        var totalMembers = totalUsers - totalAdmins;
        var totalVerified = await db.Users.CountAsync(u => u.EmailConfirmed);
        var totalActiveMemberships = await db.SubscriptionRequests.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Approved && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now))
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync();

        var viewModel = new AdminUsersIndexViewModel
        {
            Users = items,
            SearchQuery = trimmedQ,
            RoleFilter = normalizedRole,
            VerificationFilter = normalizedVerification,
            MembershipFilter = normalizedMembership,
            StatusFilter = normalizedStatus,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalUsersCount = totalUsers,
            TotalMembersCount = totalMembers,
            TotalAdminsCount = totalAdmins,
            TotalVerifiedCount = totalVerified,
            TotalActiveMembershipsCount = totalActiveMemberships
        };

        return View(viewModel);
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> UserDetails(string id, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        var safeReturnUrl = "/admin/users";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            safeReturnUrl = returnUrl;
        }

        var roleNames = await (from ur in db.UserRoles.AsNoTracking()
                               join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                               where ur.UserId == id
                               select r.Name)
                              .ToListAsync();

        var role = roleNames.Contains(AppRoles.Admin) ? AppRoles.Admin : AppRoles.Member;

        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        var subscriptions = await db.SubscriptionRequests
            .AsNoTracking()
            .Where(s => s.UserId == id)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var activeSub = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatus.Approved && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now));
        var lastApprovedSub = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatus.Approved);

        string membershipLabel = "Free";
        string currentPlan = "Free";
        bool isMembershipActive = false;
        DateTime? membershipExpiry = null;

        if (activeSub != null)
        {
            isMembershipActive = true;
            currentPlan = activeSub.Plan == SubscriptionPlan.Yearly ? "Annual" : "Monthly";
            membershipLabel = activeSub.Plan == SubscriptionPlan.Yearly ? "Active Annual" : "Active Monthly";
            membershipExpiry = activeSub.ExpiresAtUtc;
        }
        else if (subscriptions.Any(s => s.Status == SubscriptionStatus.Approved && s.ExpiresAtUtc <= now))
        {
            membershipLabel = "Expired";
            currentPlan = "None";
            membershipExpiry = subscriptions.Where(s => s.Status == SubscriptionStatus.Approved).Max(s => s.ExpiresAtUtc);
        }

        var recipeContributionsCount = await db.ContentItems
            .AsNoTracking()
            .CountAsync(c => c.AuthorUserId == id && c.Kind == ContentKind.Recipe);

        var tipContributionsCount = await db.ContentItems
            .AsNoTracking()
            .CountAsync(c => c.AuthorUserId == id && c.Kind == ContentKind.Tip);

        var contestEntriesCount = await db.ContestEntries
            .AsNoTracking()
            .CountAsync(e => e.AuthorUserId == id);

        var winningEntriesCount = await db.ContestEntries
            .AsNoTracking()
            .CountAsync(e => e.AuthorUserId == id && e.Status == ContestEntryStatus.Selected);

        var feedbackCount = await db.Feedbacks
            .AsNoTracking()
            .CountAsync(f => f.AuthorUserId == id);

        var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isCurrentAdmin = string.Equals(user.Id, currentAdminId, StringComparison.Ordinal);
        var isDemoAdmin = user.IsDemoAdminOtpBypass;
        var isOnlyAdmin = false;
        var isLastActiveAdmin = false;
        if (role == AppRoles.Admin)
        {
            var adminRoleId = await db.Roles
                .Where(r => r.Name == AppRoles.Admin)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (adminRoleId != null)
            {
                var totalAdmins = await db.UserRoles
                    .Where(ur => ur.RoleId == adminRoleId)
                    .CountAsync();
                isOnlyAdmin = totalAdmins <= 1;

                var totalActiveAdmins = await (from ur in db.UserRoles
                                               join u in db.Users on ur.UserId equals u.Id
                                               where ur.RoleId == adminRoleId && u.IsActive
                                               select u.Id).CountAsync();
                isLastActiveAdmin = user.IsActive && totalActiveAdmins <= 1;
            }
        }

        var viewModel = new AdminUserDetailsViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Role = role,
            EmailConfirmed = user.EmailConfirmed,
            IsActive = user.IsActive,
            IsTemporarilyLocked = isLocked,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            MembershipLabel = membershipLabel,
            CurrentPlan = currentPlan,
            IsMembershipActive = isMembershipActive,
            MembershipExpiry = membershipExpiry,
            SubscriptionHistoryCount = subscriptions.Count,
            LatestSubscriptionStatus = subscriptions.FirstOrDefault()?.Status.ToString(),
            LastApprovedSubscriptionDate = lastApprovedSub?.ReviewedAtUtc ?? lastApprovedSub?.CreatedAtUtc,
            RecipeContributionCount = recipeContributionsCount,
            TipContributionCount = tipContributionsCount,
            ContestEntryCount = contestEntriesCount,
            WinningContestEntryCount = winningEntriesCount,
            FeedbackCount = feedbackCount,
            ReturnUrl = safeReturnUrl,
            IsCurrentAdmin = isCurrentAdmin,
            IsDemoAdmin = isDemoAdmin,
            IsOnlyAdmin = isOnlyAdmin,
            IsLastActiveAdmin = isLastActiveAdmin
        };

        return View(viewModel);
    }

    [HttpGet("users/{id}/edit")]
    public async Task<IActionResult> EditUser(string id, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var safeReturnUrl = Url.Action(nameof(UserDetails), new { id = user.Id }) ?? $"/admin/users/{user.Id}";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            safeReturnUrl = returnUrl;
        }

        var roles = await userManager.GetRolesAsync(user);
        var currentRole = roles.Contains(AppRoles.Admin) ? AppRoles.Admin : AppRoles.Member;

        var model = new AdminUserEditViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            ReturnUrl = safeReturnUrl,
            CurrentRole = currentRole,
            EmailConfirmed = user.EmailConfirmed,
            IsDemoAdmin = user.IsDemoAdminOtpBypass,
            IsActive = user.IsActive
        };

        return View(model);
    }

    [HttpPost("users/{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, AdminUserEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(model.Id) || !string.Equals(id, model.Id, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var safeReturnUrl = Url.Action(nameof(UserDetails), new { id = user.Id }) ?? $"/admin/users/{user.Id}";
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) && model.ReturnUrl.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            safeReturnUrl = model.ReturnUrl;
        }

        var roles = await userManager.GetRolesAsync(user);
        model.CurrentRole = roles.Contains(AppRoles.Admin) ? AppRoles.Admin : AppRoles.Member;
        model.EmailConfirmed = user.EmailConfirmed;
        model.IsDemoAdmin = user.IsDemoAdminOtpBypass;
        model.IsActive = user.IsActive;
        model.ReturnUrl = safeReturnUrl;

        var trimmedDisplayName = model.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedDisplayName))
        {
            ModelState.AddModelError(nameof(model.DisplayName), "Display Name is required and cannot be empty or whitespace.");
        }
        else if (trimmedDisplayName.Length > 100)
        {
            ModelState.AddModelError(nameof(model.DisplayName), "Display Name cannot exceed 100 characters.");
        }

        var trimmedEmail = model.Email?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedEmail) || !new EmailAddressAttribute().IsValid(trimmedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "Please provide a valid email address.");
        }
        else if (trimmedEmail.Length > 256)
        {
            ModelState.AddModelError(nameof(model.Email), "Email address cannot exceed 256 characters.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var isEmailChanged = !string.Equals(user.Email, trimmedEmail, StringComparison.OrdinalIgnoreCase);
        var isDisplayNameChanged = !string.Equals(user.DisplayName, trimmedDisplayName, StringComparison.Ordinal);

        if (!isEmailChanged && !isDisplayNameChanged)
        {
            TempData["SuccessMessage"] = "No changes were made to the user profile.";
            return RedirectToAction(nameof(UserDetails), new { id = user.Id });
        }

        if (isEmailChanged)
        {
            if (user.IsDemoAdminOtpBypass)
            {
                ModelState.AddModelError(nameof(model.Email), "The demo administrator email address is protected and cannot be changed.");
                return View(model);
            }

            var existingWithEmail = await userManager.FindByEmailAsync(trimmedEmail!);
            if (existingWithEmail != null && existingWithEmail.Id != user.Id)
            {
                ModelState.AddModelError(nameof(model.Email), "Another account is already registered with this email address.");
                return View(model);
            }
        }

        var originalDisplayName = user.DisplayName;
        var originalEmail = user.Email;
        var originalUserName = user.UserName;
        var originalEmailConfirmed = user.EmailConfirmed;

        if (isDisplayNameChanged)
        {
            user.DisplayName = trimmedDisplayName!;
        }

        if (isEmailChanged)
        {
            var setEmailResult = await userManager.SetEmailAsync(user, trimmedEmail);
            if (!setEmailResult.Succeeded)
            {
                foreach (var err in setEmailResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                user.DisplayName = originalDisplayName;
                return View(model);
            }

            var setUserNameResult = await userManager.SetUserNameAsync(user, trimmedEmail);
            if (!setUserNameResult.Succeeded)
            {
                foreach (var err in setUserNameResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                await userManager.SetEmailAsync(user, originalEmail);
                user.DisplayName = originalDisplayName;
                return View(model);
            }

            user.EmailConfirmed = false;
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var err in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            user.DisplayName = originalDisplayName;
            if (isEmailChanged)
            {
                await userManager.SetEmailAsync(user, originalEmail);
                await userManager.SetUserNameAsync(user, originalUserName);
                user.EmailConfirmed = originalEmailConfirmed;
            }

            return View(model);
        }

        if (isEmailChanged)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (isEmailChanged && string.Equals(user.Id, currentAdminId, StringComparison.Ordinal))
        {
            await signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "Your email address has been updated. Please sign in with your new email address.";
            return RedirectToAction("Login", "Account");
        }

        TempData["SuccessMessage"] = "User profile updated successfully.";
        return RedirectToAction(nameof(UserDetails), new { id = user.Id });
    }

    [HttpPost("users/{id}/role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, AdminUserChangeRoleViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(model.Id) || !string.Equals(id, model.Id, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var safeReturnUrl = Url.Action(nameof(UserDetails), new { id = user.Id }) ?? $"/admin/users/{user.Id}";
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) && model.ReturnUrl.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            safeReturnUrl = model.ReturnUrl;
        }

        var targetRole = model.TargetRole?.Trim();
        if (string.IsNullOrWhiteSpace(targetRole) ||
            (!string.Equals(targetRole, AppRoles.Member, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(targetRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["ErrorMessage"] = "Invalid role specified. Only 'Member' and 'Admin' roles are permitted.";
            return Redirect(safeReturnUrl);
        }

        var normalizedTargetRole = string.Equals(targetRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? AppRoles.Admin
            : AppRoles.Member;

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count != 1 || (!currentRoles.Contains(AppRoles.Member) && !currentRoles.Contains(AppRoles.Admin)))
        {
            TempData["ErrorMessage"] = "Account has an invalid or conflicting role configuration. Role changes cannot be performed automatically.";
            return Redirect(safeReturnUrl);
        }

        var currentRole = currentRoles.First();
        if (string.Equals(currentRole, normalizedTargetRole, StringComparison.Ordinal))
        {
            TempData["SuccessMessage"] = $"User already possesses the '{normalizedTargetRole}' role. No changes were made.";
            return Redirect(safeReturnUrl);
        }

        var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(user.Id, currentAdminId, StringComparison.Ordinal) && string.Equals(normalizedTargetRole, AppRoles.Member, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "You cannot revoke your own Administrator role.";
            return Redirect(safeReturnUrl);
        }

        if (user.IsDemoAdminOtpBypass && string.Equals(normalizedTargetRole, AppRoles.Member, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "The demo administrator account must retain the Administrator role.";
            return Redirect(safeReturnUrl);
        }

        if (string.Equals(currentRole, AppRoles.Admin, StringComparison.Ordinal) && string.Equals(normalizedTargetRole, AppRoles.Member, StringComparison.Ordinal))
        {
            var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
            var otherAdminsCount = admins.Count(u => u.Id != user.Id);
            if (otherAdminsCount < 1)
            {
                TempData["ErrorMessage"] = "The system must retain at least one Administrator.";
                return Redirect(safeReturnUrl);
            }
        }

        if (string.Equals(normalizedTargetRole, AppRoles.Admin, StringComparison.Ordinal))
        {
            var removeResult = await userManager.RemoveFromRoleAsync(user, AppRoles.Member);
            if (!removeResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to update user role: " + string.Join("; ", removeResult.Errors.Select(e => e.Description));
                return Redirect(safeReturnUrl);
            }

            var addResult = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!addResult.Succeeded)
            {
                // Rollback
                await userManager.AddToRoleAsync(user, AppRoles.Member);
                TempData["ErrorMessage"] = "Failed to grant Administrator role: " + string.Join("; ", addResult.Errors.Select(e => e.Description));
                return Redirect(safeReturnUrl);
            }
        }
        else
        {
            var removeResult = await userManager.RemoveFromRoleAsync(user, AppRoles.Admin);
            if (!removeResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to revoke Administrator role: " + string.Join("; ", removeResult.Errors.Select(e => e.Description));
                return Redirect(safeReturnUrl);
            }

            var addResult = await userManager.AddToRoleAsync(user, AppRoles.Member);
            if (!addResult.Succeeded)
            {
                // Rollback
                await userManager.AddToRoleAsync(user, AppRoles.Admin);
                TempData["ErrorMessage"] = "Failed to grant Member role: " + string.Join("; ", addResult.Errors.Select(e => e.Description));
                return Redirect(safeReturnUrl);
            }
        }

        await userManager.UpdateSecurityStampAsync(user);

        TempData["SuccessMessage"] = "User role updated successfully.";
        return RedirectToAction(nameof(UserDetails), new { id = user.Id });
    }

    [HttpPost("users/{id}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateUser(string id, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var safeReturnUrl = Url.Action(nameof(UserDetails), new { id = user.Id }) ?? $"/admin/users/{user.Id}";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            safeReturnUrl = returnUrl;
        }

        if (!user.IsActive)
        {
            TempData["SuccessMessage"] = "User account is already inactive.";
            return Redirect(safeReturnUrl);
        }

        var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(user.Id, currentAdminId, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account.";
            return Redirect(safeReturnUrl);
        }

        if (user.IsDemoAdminOtpBypass)
        {
            TempData["ErrorMessage"] = "The demo administrator account cannot be deactivated.";
            return Redirect(safeReturnUrl);
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin))
        {
            var adminRoleId = await db.Roles
                .Where(r => r.Name == AppRoles.Admin)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (adminRoleId != null)
            {
                var remainingActiveAdmins = await (from ur in db.UserRoles
                                                   join u in db.Users on ur.UserId equals u.Id
                                                   where ur.RoleId == adminRoleId && u.Id != user.Id && u.IsActive
                                                   select u.Id).CountAsync();
                if (remainingActiveAdmins < 1)
                {
                    TempData["ErrorMessage"] = "The system must retain at least one active Administrator.";
                    return Redirect(safeReturnUrl);
                }
            }
        }

        user.IsActive = false;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["ErrorMessage"] = "Failed to deactivate user: " + string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return Redirect(safeReturnUrl);
        }

        await userManager.UpdateSecurityStampAsync(user);
        await emailOtpService.InvalidateUserChallengesAsync(user.Id);

        TempData["SuccessMessage"] = "User account deactivated successfully.";
        return Redirect(safeReturnUrl);
    }

    [HttpPost("users/{id}/activate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateUser(string id, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var safeReturnUrl = Url.Action(nameof(UserDetails), new { id = user.Id }) ?? $"/admin/users/{user.Id}";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            safeReturnUrl = returnUrl;
        }

        if (user.IsActive)
        {
            TempData["SuccessMessage"] = "User account is already active.";
            return Redirect(safeReturnUrl);
        }

        user.IsActive = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["ErrorMessage"] = "Failed to activate user: " + string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return Redirect(safeReturnUrl);
        }

        await userManager.UpdateSecurityStampAsync(user);

        TempData["SuccessMessage"] = "User account activated successfully.";
        return Redirect(safeReturnUrl);
    }



    [HttpGet("feedback")]
    public async Task<IActionResult> Feedback(FeedbackStatus? status)
    {
        var model = await feedbackService.GetAdminFeedbackListAsync(status);
        return View(model);
    }

    [HttpPost("feedback/{id:int}/moderate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateFeedback(int id, FeedbackStatus status, string? adminNotes)
    {
        var (success, message) = await feedbackService.ModerateFeedbackAsync(id, status, adminNotes);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Feedback));
    }

    [HttpGet("contributions")]
    public async Task<IActionResult> Contributions(ContentKind? kind, PublicationStatus? status)
    {
        var model = await contributionService.GetAdminContributionListAsync(kind, status);
        return View(model);
    }

    [HttpPost("contributions/{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveContribution(int id, ContentVisibility visibility = ContentVisibility.Free)
    {
        var (success, message) = await contributionService.ApproveContributionAsync(id, visibility);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contributions));
    }

    [HttpPost("contributions/{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectContribution(int id, string? rejectionReason)
    {
        var (success, message) = await contributionService.RejectContributionAsync(id, rejectionReason);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contributions));
    }

    // ==========================================
    // Phase 4: Editorial Content CRUD
    // ==========================================

    [HttpGet("content")]
    public async Task<IActionResult> Content(ContentKind? kind, ContentVisibility? visibility, PublicationStatus? status, bool showDeleted = false)
    {
        var model = await adminContentService.GetEditorialContentListAsync(kind, visibility, status, showDeleted);
        return View(model);
    }

    [HttpGet("content/recipes/new")]
    public IActionResult NewRecipe()
    {
        return View("RecipeForm", new AdminRecipeEditViewModel());
    }

    [HttpPost("content/recipes/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewRecipe(AdminRecipeEditViewModel model)
    {
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("RecipeForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("RecipeForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("RecipeForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, id, slug) = await adminContentService.SaveRecipeAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("RecipeForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Recipe });
    }

    [HttpGet("content/recipes/{id:int}/edit")]
    public async Task<IActionResult> EditRecipe(int id)
    {
        var model = await adminContentService.GetRecipeForEditAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = $"Editorial recipe #{id} was not found.";
            return RedirectToAction(nameof(Content));
        }

        return View("RecipeForm", model);
    }

    [HttpPost("content/recipes/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRecipe(int id, AdminRecipeEditViewModel model)
    {
        model.Id = id;
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("RecipeForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("RecipeForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("RecipeForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, _, _) = await adminContentService.SaveRecipeAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("RecipeForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Recipe });
    }

    [HttpGet("content/tips/new")]
    public IActionResult NewTip()
    {
        return View("TipForm", new AdminTipEditViewModel());
    }

    [HttpPost("content/tips/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewTip(AdminTipEditViewModel model)
    {
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("TipForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("TipForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("TipForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, id, slug) = await adminContentService.SaveTipAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("TipForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Tip });
    }

    [HttpGet("content/tips/{id:int}/edit")]
    public async Task<IActionResult> EditTip(int id)
    {
        var model = await adminContentService.GetTipForEditAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = $"Editorial tip #{id} was not found.";
            return RedirectToAction(nameof(Content));
        }

        return View("TipForm", model);
    }

    [HttpPost("content/tips/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTip(int id, AdminTipEditViewModel model)
    {
        model.Id = id;
        string? newlyUploadedFile = null;

        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            if (!ModelState.IsValid)
                return View("TipForm", model);

            var uploadResult = await mediaService.UploadImageAsync(model.ImageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError("ImageFile", uploadResult.Message);
                return View("TipForm", model);
            }
            model.ImageUrl = uploadResult.Url;
            newlyUploadedFile = uploadResult.FileName;
        }
        else if (model.RemoveImage)
        {
            model.ImageUrl = null;
        }

        if (!ModelState.IsValid)
            return View("TipForm", model);

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminDisplayName = User.Identity?.Name ?? "James Thew";

        var (success, message, _, _) = await adminContentService.SaveTipAsync(model, adminUserId, adminDisplayName);
        if (!success)
        {
            if (!string.IsNullOrEmpty(newlyUploadedFile))
            {
                await mediaService.DeleteMediaAsync(newlyUploadedFile);
            }
            ModelState.AddModelError(string.Empty, message);
            return View("TipForm", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Content), new { kind = ContentKind.Tip });
    }

    [HttpPost("content/{id:int}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveContent(int id)
    {
        var (success, message) = await adminContentService.SoftDeleteContentAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Content));
    }

    [HttpPost("content/{id:int}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreContent(int id)
    {
        var (success, message) = await adminContentService.RestoreContentAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Content));
    }

    // ==========================================
    // Phase 4 Step 2: Media Management
    // ==========================================

    [HttpGet("media")]
    public async Task<IActionResult> Media(string? search)
    {
        var model = await mediaService.GetMediaLibraryAsync(search);
        return View(model);
    }

    [HttpPost("media/upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadMedia([FromForm] MediaUploadInputModel input)
    {
        if (input.File == null || input.File.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select an image file to upload.";
            return RedirectToAction(nameof(Media));
        }

        var (success, message, url, fileName) = await mediaService.UploadImageAsync(input.File);
        if (success)
        {
            TempData["SuccessMessage"] = $"Image '{fileName}' successfully uploaded.";
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Media));
    }

    [HttpPost("media/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMedia([FromForm] string fileName)
    {
        var (success, message, _) = await mediaService.DeleteMediaAsync(fileName);
        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Media));
    }

    [HttpGet("media/picker")]
    public async Task<IActionResult> MediaPicker(string? search)
    {
        var items = await mediaService.GetPickerMediaAsync(search);
        return Json(new { items });
    }

    // ==========================================
    // Phase 5A: Contest Management
    // ==========================================

    [HttpGet("contests")]
    public async Task<IActionResult> Contests(ContestStatus? status, ContestType? type, bool showArchived = false)
    {
        var model = await contestService.GetAdminContestsAsync(status, type, showArchived);
        return View(model);
    }

    [HttpGet("contests/new")]
    public async Task<IActionResult> NewContest()
    {
        var availableMedia = await mediaService.GetPickerMediaAsync();
        var model = new AdminContestEditViewModel
        {
            OpensAt = DateTime.UtcNow,
            ClosesAt = DateTime.UtcNow.AddDays(30),
            AvailableMedia = availableMedia
        };
        return View("ContestEditor", model);
    }

    [HttpPost("contests/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContest(AdminContestEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var (success, message, contestId) = await contestService.CreateContestAsync(model, adminUserId);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Contests));
    }

    [HttpGet("contests/{id:int}/edit")]
    public async Task<IActionResult> EditContest(int id)
    {
        var model = await contestService.GetAdminContestByIdAsync(id);
        if (model == null)
        {
            return NotFound();
        }

        model.AvailableMedia = await mediaService.GetPickerMediaAsync();
        return View("ContestEditor", model);
    }

    [HttpPost("contests/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContest(int id, AdminContestEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var (success, message) = await contestService.UpdateContestAsync(model, adminUserId);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            model.AvailableMedia = await mediaService.GetPickerMediaAsync();
            return View("ContestEditor", model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishContest(int id)
    {
        var (success, message) = await contestService.ChangeStatusAsync(id, ContestStatus.Published);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/unpublish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishContest(int id)
    {
        var (success, message) = await contestService.ChangeStatusAsync(id, ContestStatus.Draft);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseContest(int id)
    {
        var (success, message) = await contestService.ChangeStatusAsync(id, ContestStatus.Closed);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveContest(int id)
    {
        var (success, message) = await contestService.ArchiveContestAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpPost("contests/{id:int}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreContest(int id)
    {
        var (success, message) = await contestService.RestoreContestAsync(id);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Contests));
    }

    [HttpGet("contests/{id:int}/entries")]
    public async Task<IActionResult> ContestEntries(int id)
    {
        var model = await contestEntryService.GetAdminContestEntriesAsync(id);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [HttpPost("contests/{id:int}/entries/{entryId:int}/review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewContestEntry(int id, int entryId, ReviewContestEntryInputModel input)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.ReviewEntryAsync(
            id,
            entryId,
            adminUserId,
            input.Status,
            input.AdminReviewNotes,
            input.DisqualificationReason);

        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }

    [HttpPost("contests/{id:int}/entries/{entryId:int}/select-winner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectContestWinner(int id, int entryId)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.SelectWinnerAsync(id, entryId, adminUserId);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }

    [HttpPost("contests/{id:int}/announce-winner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnounceContestWinner(int id)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.AnnounceWinnerAsync(id, adminUserId);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }

    [HttpPost("contests/{id:int}/revoke-winner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeContestWinner(int id, string? reason)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Challenge();

        var (success, message) = await contestJudgingService.RevokeWinnerAsync(id, adminUserId, reason);
        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(ContestEntries), new { id });
    }
}
