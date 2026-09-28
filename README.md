# JamesThew.com

James Thew ki recipes, cooking tips, paid membership, feedback aur contests ke Aptech eProject ka ASP.NET Core MVC solution.

## Current Phase

**Phase 6A: Release-Readiness Review & Automated Demo QA complete. Build 0 warnings/0 errors, 153/153 tests pass.**

### Phase 6A Deliverables & Verification
1. **Neutral Winner Display Name Fallback & Zero Data Leakage**:
   - When a contest entrant's `DisplayName` is missing or whitespace, the system falls back to a neutral, professional moniker: `"Culinary Member"`.
   - Never falls back to email or email prefixes (e.g. `user@example.com` will never be shown as `user`).
   - Public HTML strictly excludes email addresses, private ingredients, preparation steps, and admin qualitative review notes.
2. **Atomic Winner Replacement & Durable Multi-Revocation Audit**:
   - Replacing an announced winner atomically clears public announcement state (`WinnerAnnouncedAtUtc = null`). The replacement winner is NEVER publicly visible until an explicit "Announce Winner Publicly" action is taken.
   - Repeated revocations preserve a durable, timestamped audit log (`[yyyy-MM-dd HH:mm UTC] reason`) in both `Contest.WinnerRevocationReason` and `ContestEntry.RevocationReason` without overwriting prior audit entries.
3. **Dedicated Clean Demo Database (`JamesThew_Demo`)**:
   - Connection String: `Server=(localdb)\MSSQLLocalDB;Database=JamesThew_Demo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`
   - Migrated through all 8 EF Core migrations up to `20260928074105_Phase5CDurableRevocationAudit`.
   - Seeded with clean baseline data: 7 ContentItems (recipes/tips), 7 Faqs, 4 Contests, 1 Admin User, 2 Roles.
   - Admin credentials configured securely in User Secrets (`LocalAdmin:Email = "admin@jamesthew.com"`, `LocalAdmin:Password = "Admin@Pass1234!"`).
   - Development database `JamesThew_Development` and its 16 ContentItems (including all 9 QA items) are 100% preserved and untouched.
4. **Automated End-to-End Browser QA (`ReleaseReadinessBrowserE2ETests.cs`)**:
   - Chromium Playwright E2E testing across 7 comprehensive lifecycle phases:
     - Part 1: Guest browsing & responsive layout on Desktop (1280x800) and Mobile (390x844). Verified 0 horizontal overflow, 0 broken images, free content access vs members-only locked state.
     - Part 2: Member registration and login with Alex Rivers.
     - Part 3: Subscription request submission, admin approval from `/admin/subscriptions`, and subsequent unlock of members-only masterclass recipe.
     - Part 4: Community contribution submission, admin moderation approval from `/admin/contributions`, and public publication.
     - Part 5: Editorial media library upload from admin dashboard.
     - Part 6: Contest entry submission, admin contest closure, winner selection, unannounced winner privacy verification (0 leak in public HTML), winner announcement verification, and durable revocation with audit record.
     - Part 7: Zero unhandled console or server errors across all browser contexts.
   - All QA screenshots are captured into system temporary directories and excluded from Git tracking.
5. **Full Verification**:
   - Solution Build: 0 Warnings, 0 Errors (`dotnet build JamesThew.slnx`).
   - Test Suite: 153/153 Tests Passed (100% Pass Rate).
   - EF Core Model: 0 Pending Model Changes (`dotnet ef migrations has-pending-model-changes`).

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

Migrations `20260926181611_InitialIdentity`, `20260927100700_Phase2PublicContent`, `20260927143304_Phase3AMembershipSubscriptions`, `20260927150554_Phase3BMemberFeedbackAndContributions`, `20260927212751_Phase5AContests`, `20260927221035_Phase5BContestEntries`, `20260928015849_Phase5CJudgingAndWinners`, aur `20260928074105_Phase5CDurableRevocationAudit` Development database par apply ho chuki hain. `database update` repeat karna safe hai. Startup schema migrate nahi karta; migration pehle run karein. Startup Member/Admin role names idempotently ensure karta hai aur demo content (`ContentSeeder.cs`) seed karta hai.

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

## Application features aur access boundaries
 
- Public Home (`/`): Culinary hero presentation, quick search bar, featured Free/Paid recipes and tips, membership tier comparison cards ($10/mo, $100/yr demo), contests teaser, FAQ accordion preview.
- Public Recipes (`/recipes`, `/recipes/{slug}`): Catalog with search and filter tabs (All, Free, Members-Only). Free recipes show full ingredients and preparation steps. Members-Only recipes show locked preview box, login/join CTA, zero ingredients/steps exposed, and `Cache-Control: no-cache, no-store, must-revalidate` response header.
- Public Tips (`/tips`, `/tips/{slug}`): Catalog with search and filter tabs. Free tips show full text. Members-Only tips show locked preview box with zero protected body exposure and no-cache header.
- Public Search: Searches across titles, summaries, and tags. Secret body text (e.g. ingredients, steps, tip text) is strictly omitted from search matching for unauthorized users to prevent indirect leakage.
- Public FAQ (`/faq`): Semantic HTML5 `<details>`/`<summary>` elements display all 7 required answers from PRD CRS 7; works completely without JavaScript; includes deep-link anchors and policy links.
- Member Membership Portal (`/membership`): Clear tier comparison, guest login challenge, demo plan request submission form with optional subscriber notes, real-time subscription status banner (Pending / Active / Rejected with admin feedback), and complete user request history.
- Admin Subscriptions Management (`/admin/subscriptions`): Admin-only management view with status filter tabs (All, Pending, Approved, Rejected), subscriber details, timestamps, and inline Approve/Reject action forms with feedback notes.
- Content Unlocking: Approved members immediately gain full access to protected Members-Only recipes (ingredients and steps) and cooking tips (full technique body).
- Access Boundary: Pending, rejected, and guest visitors continue to see locked preview boxes with `Cache-Control: no-cache, no-store, must-revalidate`. Admin role continues to bypass locked content.
- Member Feedback (`/feedback`): Authenticated member feedback intake with category selection, 1-5 rating, and message validation. Personal feedback history log displays `Reviewed` status badge and administrator feedback notes. Guests challenged/redirected to login. Submitted feedback is stored privately and strictly withheld from public pages.
- Member Contributions (`/contributions`): Authenticated members can submit recipes (`/contributions/recipe/new`) and cooking tips (`/contributions/tip/new`). The member dashboard reflects real-time moderation outcomes with direct live links for `Published` contributions and editorial explanations for `Rejected` contributions.
- Admin Moderation Queues (`/admin/feedback`, `/admin/contributions`): Full moderation workflows for administrators. For feedback: mark as Reviewed or Archived with admin notes. For contributions: Approve & Publish (Free by default, instantly appearing in public recipe and tip catalogs) or Reject with required explanation (retaining 404 public isolation). All actions protected by antiforgery and Admin policy.
- Admin Editorial Management (`/admin/content`): Catalog dashboard with live metrics, multi-dimensional filters, recipe and tip authoring/editing, kebab-case auto-slugs, unpublishing/soft-deletion policy, and Members-Only subscription lock preservation.
- Admin Media Management (`/admin/media`): Secure visual media library with upload validation (JPEG/PNG/WebP, magic bytes, 5 MB limit), path traversal defense, safe storage under `wwwroot/uploads/editorial/`, recipe/tip form integration with interactive picker modal, image unlinking, orphan file prevention, safe deletion unlinking, and full Playwright Chromium browser E2E test coverage.
- Public Culinary Contests (`/contests`, `/contests/{slug}`): Public read-only competition discovery for guests and members. Category filtering (Recipe vs Cooking Tip), timeline tabs (All, Open Now, Upcoming, Past), responsive card grid with timeline status badges, rich detail view with guidelines/rules, award distinctions, and clear member entry eligibility guidance. When a winner is officially announced, a prominent accolade banner `#announcedWinnerBanner` displays the winning recipe/tip, winning chef, and prize distinction. Draft and archived contests strictly return HTTP 404 for public visitors.
- Member Contest Entries (`/contests/{slug}/entry`, `/contests/my-entries`, `/contests/my-entries/{id}`): Authenticated members can submit official entries to open recipe or cooking tip contests. Features structured recipe ingredients/steps and tip technique validation, single-entry-per-member enforcement via database unique index and service rules, pre-deadline editing lifecycle with form auto-population, and private "My Contest Entries" dashboard. Entries are immediately locked from member editing if placed `UnderReview`, `Disqualified`, or `Selected`, or when the contest closes. Disqualified members see clear disqualification reasons while private admin review notes remain strictly confidential.
- Admin Contests Management & Judging (`/admin/contests`, `/admin/contests/{id}/entries`): Comprehensive contest management for site administrators. Create, edit, draft/publish toggle, close, archive, restore, and contest entry evaluation panel. Administrators can review submitted formulations, record qualitative review notes and disqualification reasons, select the official winning entry after the contest closes, decouple winner selection from public visibility, formally announce winners to the public, or revoke winner selections with an explicit audit reason. Enforces maximum 1 winner per contest with atomic status transitions and a SQL Server filtered unique index constraint (`IX_ContestEntries_ContestId_SingleWinner`).
- Public Announcements & Hall of Fame (`/announcements`): Dedicated contest winner recognition showcase displaying officially announced competition winners awarded by Chef James Thew. Shows contest title, category, winning chef display name, entry title, approved summary, and prize distinction with zero private recipe/tip ingredient/step or contact leakage. Unannounced selected winners remain strictly hidden from public view.
- Account & Admin (`/account/status`, `/admin`): Preserved from Phase 1. Logout remains antiforgery-protected POST only. External return URLs rejected. Lockout and password policies remain active.

## Backup, Git aur academic delivery

Foundation tests SQL backup checksum, restore to separate database, user hash/role records aur migrations verify karte hain. Rozana source/docs aur database backup rakhna ab bhi operational requirement hai; automatic daily scheduler is phase mein nahi banaya. Local database backup apni protected separate location mein rakhein; restored copy par test karein, live data par destructive restore nahi.

Git repository initialize hui aur `.gitignore` local secrets, build outputs, database/backup files aur test artifacts exclude karta hai. Phase 1 acceptance checkpoint ke liye initial Git commit authorized hai; exact commit `git log -1 --oneline` se dekhein. Source PDFs, original settings, launch profiles, vendor assets aur unrelated files preserve hue.

Academic calendar dates are maintained privately. Internal checkpoint, final submission and two status reports remain required. PDF-required report/source, two review/status mails aur actual feedback form final package mein chahiye. Remaining input: billing period/currency detail for Phase 3, contest edge rules later, exact faculty constraints, actual forms/recipient/deadline timezone. Is phase ke liye billing/contest decision ki zaroorat nahi padi.

Implementation references: [Microsoft Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0), [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).
