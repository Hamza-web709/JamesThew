# JamesThew.com

James Thew ki recipes, cooking tips, paid membership, feedback aur contests ke Aptech eProject ka ASP.NET Core MVC solution.

## Current Phase

**Phase 1 foundation implemented, build aur 19 integration tests pass.** Existing `net10.0` MVC project reuse hua; EF Core SQL Server, Identity Member/Admin roles, registration/login/logout, protected account/admin landing pages aur safe local admin seed ready hain. Database mein sirf Identity tables aur ApplicationUser.DisplayName hai. Public registration Member account banati hai, paid membership activate nahi karti.

27 September acceptance review mein trusted local HTTPS par browser registration/login/logout aur Member/Admin access checks pass hue. Latest evidence aur scoped review findings docs/TEST_PLAN.md mein hain. Payment, profile editing, recipe/contribution/contest workflows abhi implemented nahi. Hosting optional hai.

## Next Phase

Next phase **Phase 2: public read-only MVC website** hai, agle instruction ke baad. Account authentication user ke current scope se Phase 1 mein aa gayi; Phase 3 mein profile editing, simulated/manual membership approval aur member feedback baqi hain. Billing periods aur contest edge rules abhi guess nahi kiye gaye.

## Documents aur sources

- [PRD](docs/PRD.md): approved behavior, original PDF differences aur remaining decisions.
- [DESIGN](docs/DESIGN.md): future editorial UI/layouts.
- [SYSTEM_DESIGN](docs/SYSTEM_DESIGN.md): implemented foundation vs future entities.
- [TASKS](docs/TASKS.md): scoped completion, dependencies aur dates.
- [TEST_PLAN](docs/TEST_PLAN.md): actual foundation results aur future scenarios.
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

Migration `20260926181611_InitialIdentity` Development database par apply ho chuki hai. `database update` repeat karna safe hai. App startup schema migrate nahi karta; migration pehle run karein. Startup Member/Admin role names idempotently ensure karta hai. Future feature migration us feature ki implementation mein add hogi; payment/contribution/contest entities abhi nahi.

## Run aur test

```powershell
dotnet run --project JamesThew/JamesThew.csproj --launch-profile https
dotnet test JamesThew.slnx --logger 'trx;LogFileName=foundation.trx' --results-directory artifacts/TestResults
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

## Foundation access aur limits

- Public: existing Home/Privacy aur Login/Register pages. Registration always Member role assign karti hai; submitted Role/activation/email-confirmation fields bind nahi hote.
- Signed-in Member/Admin: `/account/status`, account identity ka limited landing page; profile edit ya paid features nahi.
- Admin only: `/admin`, minimal authorization check page; business management UI nahi.
- Logout sirf antiforgery-protected POST; GET logout nahi karta. Login external return URLs accept nahi karta.
- Unique normalized email SQL constraint, Identity password hashing, 12-character minimum and 5-failure/15-minute lockout configured. Email confirmation service absent hone ki wajah se login confirmation gate disabled hai; EmailConfirmed falsely true nahi set hota.
- MemberAccount policy account access ke liye hai, active subscription ke liye nahi. Later paid endpoints ko future subscription policy chahiye; registration ko approval na samjhein.

## Backup, Git aur academic delivery

Foundation tests SQL backup checksum, restore to separate database, user hash/role records aur migrations verify karte hain. Rozana source/docs aur database backup rakhna ab bhi operational requirement hai; automatic daily scheduler is phase mein nahi banaya. Local database backup apni protected separate location mein rakhein; restored copy par test karein, live data par destructive restore nahi.

Git repository initialize hui aur `.gitignore` local secrets, build outputs, database/backup files aur test artifacts exclude karta hai. Phase 1 acceptance checkpoint ke liye initial Git commit authorized hai; exact commit `git log -1 --oneline` se dekhein. Source PDFs, original settings, launch profiles, vendor assets aur unrelated files preserve hue.

Academic calendar dates are maintained privately. Internal checkpoint, final submission and two status reports remain required. PDF-required report/source, two review/status mails aur actual feedback form final package mein chahiye. Remaining input: billing period/currency detail for Phase 3, contest edge rules later, exact faculty constraints, actual forms/recipient/deadline timezone. Is phase ke liye billing/contest decision ki zaroorat nahi padi.

Implementation references: [Microsoft Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0), [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).
