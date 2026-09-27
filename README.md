# JamesThew.com

James Thew ki recipes, cooking tips, paid membership, feedback aur contests ke Aptech eProject ka ASP.NET Core MVC solution.

## Current Phase

**Phase 3A: Manual Membership Approval Foundation implemented, build aur 32 integration tests pass.**
Simulated/manual demo membership subscription request flow for logged-in Member users, admin approval/rejection queue, and content unlocking implemented:
- Demo Pricing: strictly $10 monthly (`SubscriptionPlan.Monthly`), $100 yearly (`SubscriptionPlan.Yearly`). Zero real payment gateway or card collection.
- Member Membership Portal (`/membership`): Clear tier comparison, guest login challenge, demo plan request submission form with optional subscriber notes, real-time subscription status banner (Pending / Active / Rejected with admin feedback), and complete user request history.
- Admin Subscriptions Management (`/admin/subscriptions`): Admin-only management view with status filter tabs (All, Pending, Approved, Rejected), subscriber details, timestamps, and inline Approve/Reject action forms with feedback notes. Admin dashboard (`/admin`) updated with Subscriptions card and dynamic pending badge.
- Content Unlocking: Approved members immediately gain full access to protected Members-Only recipes (ingredients and steps) and cooking tips (full technique body).
- Access Boundary: Pending, rejected, and guest visitors continue to see locked preview boxes with `Cache-Control: no-cache, no-store, must-revalidate`. Admin role continues to bypass locked content.
- Database: EF Core entity `SubscriptionRequest`, migration `20260927143304_Phase3AMembershipSubscriptions` applied to `JamesThew_Development`, 0 pending model changes.
- Test Suite: 32 passed, 0 failed, 0 skipped (19 Foundation + 7 PublicContent + 6 Subscription in `SubscriptionTests.cs`).

## Next Phase

Next phase **Phase 3B: Profile editing aur member feedback** hai:
- User profile editing (`/account/profile`).
- Member-only recipe and site feedback submission forms and inbox storage.

## Documents aur sources

- [PRD](docs/PRD.md): approved behavior, original PDF differences aur remaining decisions.
- [DESIGN](docs/DESIGN.md): editorial UI/layouts aur brand tokens.
- [SYSTEM_DESIGN](docs/SYSTEM_DESIGN.md): implemented schema, aggregate relationships aur policies.
- [TASKS](docs/TASKS.md): scoped completion, dependencies, Phase 2 actual results aur dates.
- [TEST_PLAN](docs/TEST_PLAN.md): actual foundation & Phase 2 verification results, test matrix aur inventory.
- Original 10-page specification: both source PDFs are retained locally and excluded from Git. Public requirements are summarized in PRD; no public PDF download is provided.

Private screenshots and request attachments remain local; their identifiers and paths are not published. Separate submission TXT, Excel/feedback forms aur original ZIP available nahi thay.

## Verified local prerequisites

Windows par existing .NET SDK **10.0.401**, ASP.NET Core runtime **10.0.12** aur SQL Server **2025 LocalDB 17.0.4025.3** use hue. Target `net10.0` unchanged. EF Core SQL Server/Design, Identity EF stores, MVC Testing aur local dotnet-ef **10.0.12** pinned hain. xUnit **2.9.3**, runner **3.1.4**, Microsoft.NET.Test.Sdk **17.14.1** tests mein hain. Faculty ka institutional version approval ab bhi independent requirement hai.

Is terminal mein dotnet PATH par nahi tha. Neeche PowerShell commands repository root se hain; pehli line installed SDK ko sirf current shell ke PATH mein add karti hai.

```powershell
$env:PATH = 'C:\Program Files\dotnet;' + $env:PATH
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool restore
dotnet restore JamesThew.slnx
dotnet build JamesThew.slnx
```

## Database aur migrations

Development default Windows integrated authentication use karta hai: `(localdb)\MSSQLLocalDB`, database `JamesThew_Development`. Is default mein SQL password nahi. Program.cs mein default sirf Development ke liye hai. Doosre environment/server ke liye `ConnectionStrings:DefaultConnection` User Secrets ya `ConnectionStrings__DefaultConnection` environment variable se dein; secret source/appsettings mein na likhein. Production environment missing configuration par fail hota hai.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project JamesThew/JamesThew.csproj
dotnet ef migrations has-pending-model-changes --project JamesThew/JamesThew.csproj
```

Migrations `20260926181611_InitialIdentity`, `20260927100700_Phase2PublicContent`, aur `20260927143304_Phase3AMembershipSubscriptions` Development database par apply ho chuki hain. `database update` repeat karna safe hai. Startup schema migrate nahi karta; migration pehle run karein. Startup Member/Admin role names idempotently ensure karta hai aur demo content (`ContentSeeder.cs`) seed karta hai.

## Run aur test

```powershell
dotnet run --project JamesThew/JamesThew.csproj --launch-profile https
dotnet test JamesThew.slnx --logger 'trx;LogFileName=phase2.trx' --results-directory artifacts/TestResults
```

URLs: [local HTTPS](https://localhost:7054), HTTP `http://localhost:5052` HTTPS par redirect hota hai. Authentication cookies Secure/HttpOnly hain; HTTPS profile use karein. 27 September ko localhost development certificate trusted mila aur browser HTTPS verification pass hui. Fresh machine par browser use se pehle developer certificate trust verify/configure karein; `dotnet dev-certs https --trust` aapke local trust store ko change karta hai aur confirmation dikha sakta hai. Acceptance review mein trust warning bypass ya trust store change nahi kiya gaya; existing trusted certificate use hua.

Integration tests har run mein unique `JamesThew_Test_<id>` LocalDB database create/migrate karte hain, actual SQL backup/restore validate karte hain aur apne test databases cleanup karte hain. Test passwords run-time random hain. Development database tests se reset nahi hota. Results `artifacts/TestResults/foundation.trx` mein hain (Git ignored).

## Safe local admin seeding

Koi default admin/password source mein nahi. Acceptance review mein user ke explicitly chosen credentials se local Admin configure hua; bootstrap password baad mein User Secrets se remove hua. Admin seed explicit one-shot command hai, **sirf Development** mein. Pehle migration apply karein. Credentials current user's User Secrets store mein set karein; yeh repo ke bahar local-development storage hai, encrypted production vault nahi.

PowerShell secure prompt password ko command history mein literal banne se bachata hai. Yeh commands user khud apne chosen credentials ke saath run kare:

```powershell
$localAdminEmail = Read-Host 'Local admin email (new account)'
$localAdminSecret = Read-Host 'Local admin password (12-128 chars, upper/lower/digit/symbol)' -AsSecureString
$localAdminPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($localAdminSecret)
try {
    @{
        'LocalAdmin:Email' = $localAdminEmail
        'LocalAdmin:Password' = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($localAdminPointer)
    } | ConvertTo-Json | dotnet user-secrets set --project JamesThew/JamesThew.csproj
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($localAdminPointer)
    Remove-Variable localAdminSecret,localAdminPointer,localAdminEmail
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project JamesThew/JamesThew.csproj --no-launch-profile -- --seed-admin
# Successful seed ke baad bootstrap password config hata sakte hain; Identity hash DB mein rehta hai.
dotnet user-secrets remove 'LocalAdmin:Password' --project JamesThew/JamesThew.csproj
```

Seed existing Admin ko duplicate/reset nahi karta; existing Member ko promote karne se refuse karta hai. Missing/weak credentials ya non-Development environment fail hota hai. User creation aur Admin role assignment atomic hain. Credentials logs mein print nahi hote. Admin ka apna chosen password login ke liye retain karein; recovery/email sending abhi implement nahi.

## Phase 3A access aur limits
 
- Public Home (`/`): Culinary hero presentation, quick search bar, featured Free/Paid recipes and tips, membership tier comparison cards ($10/mo, $100/yr demo), contests teaser, FAQ accordion preview.
- Public Recipes (`/recipes`, `/recipes/{slug}`): Catalog with search and filter tabs (All, Free, Members-Only). Free recipes show full ingredients and preparation steps. Members-Only recipes show locked preview box, login/join CTA, zero ingredients/steps exposed, and `Cache-Control: no-cache, no-store, must-revalidate` response header.
- Public Tips (`/tips`, `/tips/{slug}`): Catalog with search and filter tabs. Free tips show full text. Members-Only tips show locked preview box with zero protected body exposure and no-cache header.
- Public Search: Searches across titles, summaries, and tags. Secret body text (e.g. ingredients, steps, tip text) is strictly omitted from search matching for unauthorized users to prevent indirect leakage.
- Public FAQ (`/faq`): Semantic HTML5 `<details>`/`<summary>` elements display all 7 required answers from PRD CRS 7; works completely without JavaScript; includes deep-link anchors and policy links.
- Member Membership Portal (`/membership`): Clear tier comparison, guest login challenge, demo plan request submission form with optional subscriber notes, real-time subscription status banner (Pending / Active / Rejected with admin feedback), and complete user request history.
- Admin Subscriptions Management (`/admin/subscriptions`): Admin-only management view with status filter tabs (All, Pending, Approved, Rejected), subscriber details, timestamps, and inline Approve/Reject action forms with feedback notes.
- Content Unlocking: Approved members immediately gain full access to protected Members-Only recipes (ingredients and steps) and cooking tips (full technique body).
- Access Boundary: Pending, rejected, and guest visitors continue to see locked preview boxes with `Cache-Control: no-cache, no-store, must-revalidate`. Admin role continues to bypass locked content.
- Informational Menus (`/contests`, `/announcements`, `/feedback`): Informational placeholder pages keep the top-level 7-item navigation functional and clear without broken links; indicate phase deferral.
- Account & Admin (`/account/status`, `/admin`): Preserved from Phase 1. Logout remains antiforgery-protected POST only. External return URLs rejected. Lockout and password policies remain active.

## Backup, Git aur academic delivery

Foundation tests SQL backup checksum, restore to separate database, user hash/role records aur migrations verify karte hain. Rozana source/docs aur database backup rakhna ab bhi operational requirement hai; automatic daily scheduler is phase mein nahi banaya. Local database backup apni protected separate location mein rakhein; restored copy par test karein, live data par destructive restore nahi.

Git repository initialize hui aur `.gitignore` local secrets, build outputs, database/backup files aur test artifacts exclude karta hai. Phase 1 acceptance checkpoint ke liye initial Git commit authorized hai; exact commit `git log -1 --oneline` se dekhein. Source PDFs, original settings, launch profiles, vendor assets aur unrelated files preserve hue.

Academic calendar dates are maintained privately. Internal checkpoint, final submission and two status reports remain required. PDF-required report/source, two review/status mails aur actual feedback form final package mein chahiye. Remaining input: billing period/currency detail for Phase 3, contest edge rules later, exact faculty constraints, actual forms/recipient/deadline timezone. Is phase ke liye billing/contest decision ki zaroorat nahi padi.

Implementation references: [Microsoft Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0), [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).
