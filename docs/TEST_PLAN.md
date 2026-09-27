# JamesThew.com - Test Plan aur Submission Evidence

## Status aur method

Neeche complete business scenarios future plan hain; scoped Phase 1 auth/database portion ke **19 integration tests pass** hue hain. Actual results neeche separate section mein hain; full payment/content/contest acceptance abhi Not run hai. Expected results ko actual pass claims na samjhein. [PRD](PRD.md) requirements aur Open Decisions ki authority hai; [TASKS](TASKS.md) execution dependencies aur selected working reporting schedule rakhta hai. Future evidence format: Test ID, REQ-ID, build/commit, environment, fixture, steps, expected/actual result, timestamp, screenshot/log path aur defect reference.

Proposed layered approach: business invariants ke unit tests; relational constraints/authorization/transactions ke integration tests actual disposable SQL Server database par; essential journeys browser E2E. In-memory DB ko SQL constraint proof na samjhein. Visual/accessibility manual checks supplement hon. Real gateway integration/tests scope se bahar hain. Manual demo request, admin approval/rejection aur idempotency test honge. Demo success real settlement proof nahi. Original PDF guest-submission clauses ke badle PRD ka user override expected behavior hai.

## Fixtures aur preconditions

Faculty-approved runtime/database versions; blank database + reviewed migrations; repeatable seed. Admin James, Active Member A, Active Member B, pending account, expired account aur anonymous session; no real passwords docs mein. Public free recipe/tip, MembersOnly recipe/tip, draft/rejected/deleted content, A/B contributions; open Recipe contest, open Tip contest, closed/archived contests, contest with no entries aur two reviewed entries. Frozen/test clock subscription and contest boundary checks ke liye. Guest identity fixture/entity nahi; anonymous session denial tests ke liye hai. Member private contact screenshots mein expose na ho. Payment flow clearly ManualDemo; Pending/Approved/Rejected request fixtures hon.

## Requirement-linked scenarios

| Test ID / REQ-ID | Role / setup aur steps | Expected result / evidence |
|---|---|---|
| TC-001 / REQ-001 | Guest Home khole; har required nav link follow kare; logged-in/admin menus compare | James details aur required links available; route/keyboard evidence |
| TC-002 / REQ-002 | Guest valid/duplicate/invalid registration; login fail/succeed/logout; $10 monthly/$100 yearly choose; Pending request, admin approve/reject, member self-approval, amount tamper aur replay simulate | Pending/rejected member access deny; sirf admin approval once activates; Demo label, no real gateway/network payment calls |
| TC-003 / REQ-003 | Guest free recipe/tip direct URL aur search; paid body phrase query aur direct route/media request | Free readable; protected text/ingredients/steps/media/snippets/cache absent; filtered counts correct |
| TC-004 / REQ-004 | Active Member/Admin free + paid recipe/tip search/view; expiry boundary par repeat | Both authorized roles full eligible results; expired ordinary account paid access lose kare |
| TC-005 / REQ-005 | Member aur Admin apna profile view/edit same page; A se B ID post; role/expiry extra fields bheje | Own fields persist; foreign profile/privilege tampering rejected; safe error summary |
| TC-006 / REQ-006 | Admin recipe ingredient rows/steps add/reorder/save/read; empty ingredient/step test | Correct ordered recipe persist; empty required rows fail server-side; no partial aggregate |
| TC-007 / REQ-007 | Admin valid tip create/read, blank title/body and script payload | Valid body persisted safely; invalid input rejected; script execution nahi |
| TC-008 / REQ-008 | Admin own recipe aur tip Free -> MembersOnly -> Free; guest existing session/cache repeat | Both types visibility enforce; paid data stale cache se leak nahi |
| TC-009 / REQ-009 | Admin apni recipe/tip view/edit/delete; stale concurrent edit; wrong author ID | Both CRUD flows correct; removed content absent; lost update silently overwrite nahi; ownership agreed policy |
| TC-010 / REQ-010 | Member recipe ingredients/procedure aur tip submit; guest general-create endpoint try | Dono contributions Pending saved; guest upload denied; public listing/detail/search/media mein Pending absent |
| TC-011 / REQ-011 | A/B recipe aur tip submit; owner/admin Pending view; other member/guest direct read; admin approve/reject; public catalog/detail/search repeat | Pending/Rejected owner/admin-only; approved Published + Free sab public ko; member self-publish/visibility tamper ignored |
| TC-012 / REQ-012 | Member A own recipe/tip view/update/delete; B se same GET edit/POST delete; owner field tamper | A succeeds, B denied; proposed published edit returns Pending and hides public version until reapproval; no unauthorized mutation |
| TC-013 / REQ-013 | Guest free/paid recipe feedback GET/POST; member accessible recipe feedback; pending/expired account, invalid message, double submit | Anonymous login challenge/no record; inactive denied; active member valid feedback linked; duplicate handled |
| TC-014 / REQ-014 | Admin all received recipe feedback including pending/rejected open kare; member admin route try | Correct message/recipe association, all statuses visible to admin; member access denied |
| TC-015 / REQ-015 | Guest site Feedback POST; active member submit without RecipeId; wrong Kind/RecipeId combination | Guest write blocked/no record; member site feedback accepted; invalid mixed record rejected |
| TC-016 / REQ-016 | Admin Recipe aur Tip contest create; invalid date range; remove/archive; member create try | Valid public listing; invalid interval/role rejected; removed contest new entries reject |
| TC-017 / REQ-017 | Guest contest browse then entry GET/POST; active member Recipe/Tip entries; pending/expired account; wrong type, duplicate, cutoff boundaries | Public contest/results readable; guest entry challenges login and saves nothing; active member input/window validated; inactive write denied |
| TC-018 / REQ-018 | Admin contest-specific entries including member recipe/tip snapshots inspect and review; author original later edit | Complete original submission remains; unrelated contest entries not mixed; review recorded |
| TC-019 / REQ-019 | Admin select reviewed winner; wrong-contest/rejected/unreviewed/no-entry cases; simultaneous publish; public Announcements read | Exactly agreed valid winner; invalid cases fail; single atomic result; latest winner visible; private email absent |
| TC-020 / REQ-020 | All roles FAQ read; count/reference compare to p5-6 seven questions; JS disabled | All seven answers align with manual demo approval, member-only feedback/entry aur approval-before-public policy; source override documented; accessible disclosures |
| TC-021 / REQ-021 | Source/report review for comments, logic explanations, synopsis/source/documentation | Meaningful comment coverage per OD-09; algorithm notes and complete report present; no false completion claims |
| TC-022 / REQ-022 | Check dated daily backup; restore database + media + source snapshot to clean environment | Matching fixture counts, relationships and protected content access; actual restore log |
| TC-023 / REQ-023 | Audit all 13 report components against final build | Every required item present, usable, referenced and internally consistent |
| TC-024 / REQ-024 | Compare two status reports, internal checkpoint and final submission to privately maintained schedule; faculty changes par recalculate | Selected dates and +10/+20 rule match; actual dispatch evidence only after authorized send; two reports accounted for |
| TC-025 / REQ-025 | Status/clarification/submission drafts review; status attachment and description open | Appropriate exact prefix, actual progress + review document, correct recipient after confirmation |
| TC-026 / REQ-026 | Final soft-copy package open; actual supplied feedback form compare | Original-required form completed appropriately; missing form remains blocker, fabricated replacement nahi |
| TC-027 / REQ-027 | Faculty-approved toolchain list vs installed/runtime/publish target compare; clean setup | Local version/setup compliance; hosting optional, not a pass requirement; existing net10.0 alone proof nahi |

## Authorization, validation aur edge-case checks

| Check | Linked requirements | Verification |
|---|---|---|
| Server policies | REQ-002, REQ-003, REQ-004, REQ-005, REQ-008, REQ-009, REQ-012, REQ-014, REQ-016, REQ-019 | Guest/pending/expired/A/B/Admin har read/write route par; URL guessing, crafted POST, forged owner/role, hidden form fields |
| Subscription boundaries | REQ-002, REQ-004 | Exact start/end, month end, leap year, concurrent/replayed activation, changed server plan, cancelled payment; fixed clock use |
| Input safety | REQ-006, REQ-007, REQ-010, REQ-013, REQ-015, REQ-017 | Whitespace, Unicode, long text, SQL-like input, XSS strings, malformed nested arrays; encoded output/server validation |
| CSRF/session | REQ-002, REQ-005, REQ-009, REQ-012, REQ-016, REQ-019 | All mutation forms invalid/missing token reject; logout and expired session safe; external returnUrl blocked |
| Media | REQ-006, REQ-007, REQ-010 | If upload approved: renamed executable, MIME spoof, oversized/dimension bomb, unsafe SVG reject; access-controlled paid media |
| Search | REQ-003, REQ-004, REQ-011 | Blank query, no match, long query, page out of range, deterministic ordering; scope filter before pagination/count |
| Login gate aur moderation | REQ-013, REQ-015, REQ-017, REQ-018 | Anonymous feedback/entry/contribution POST deny; no anonymous author persisted; member duplicates/rate limits; only admin approves; pending hidden and approved community content public |
| Lifecycle integrity | REQ-009, REQ-012, REQ-016, REQ-018, REQ-019 | Delete winning source, archive contest, concurrent changes, cross-contest FK mismatch; snapshot/evidence retained per OD-08 |
| Failure recovery | REQ-002, REQ-010, REQ-013, REQ-017, REQ-019 | Database failure during multi-row save, manual approval save failure, repeated submit/back/reload; transaction rollback, safe retry, no double result |

## Responsive, browser, accessibility aur motion

Proposed checks apply to web REQ-001 through REQ-020 and report evidence REQ-023. Actual versions/devices testing waqt log hon; coverage ko untested browsers tak extend na karein.

- Desktop Chrome/Edge aur Firefox, representative mobile Chrome aur Safari where available; unavailable browser ko Not tested mark karein. Widths 320, 375, 768, 1024, 1440px; portrait/landscape, long titles, 200% zoom.
- Har [DESIGN](DESIGN.md) page row ke loading/empty/validation/error/success states inspect; horizontal overflow, clipped labels, overlapping fixed controls, admin table actions aur ingredient readability check.
- Keyboard-only main journeys: skip link, nav, search, register, feedback, recipe row editor, member entry, admin winner. Visible focus, proper order, Escape/return focus aur form-error announcement test.
- Semantic labels/headings, contrast measurements, alt text, screen-reader sample NVDA/VoiceOver where available; automated checks ke saath manual screen-reader findings record.
- Reduced-motion enabled before load aur preference changed mid-session; GSAP/Lottie effects off, content fully visible. JS disabled/network script failed: content and server forms usable.
- Slow network/CPU profile: image dimensions prevent shifts; hero/card budgets, no forced scroll, no endless decorative animation; actual LCP/CLS/INP or lab proxy separately label. Targets DESIGN mein proposed hain, pass metrics invented nahi.
- Touch target sizes, no hover-only info, mobile drawer trap/release, browser back/restored form state aur user-readable retry verify.

## Fresh setup, migration aur restore

1. Clean machine/environment par approved prerequisites record; README ke actual implementation commands follow karein. Unknown commands ko pass na karein.
2. Blank database migration, then repeat seed; duplicates/secrets na hon. Upgrade from prior schema with data; failed migration recovery document.
3. Config missing/wrong connection/secrets cases safe diagnostic dein. Correct config par home/login/free/paid/admin smoke paths.
4. Daily backup se separate blank database aur private media restore; relationships, subscription expiry, contest winner, file access aur row counts compare. Source snapshot/hash include.
5. Local academic package clean local environment par run/restore; restart, protected files, DB access aur configuration verify. Optional hosting select ho to deploy/HTTPS/rollback checks alag add hon; no-hosting failure nahi. Deployment abhi performed nahi.

## Status reports aur final checklist

REQ-024/025 ke status reports: [TASKS working milestones](TASKS.md) ke relative rules ke mutabiq, exact subject `STATUS: ...`, actual work description, review document, blockers, clarification aur next work. `DOUBT: ...` for clarification. Sent timestamp/recipient/receipt sirf actual authorized sending ke baad; planned dates alone are not email delivery proof.

REQ-023 ke PDF p8 report components:

| Required component | Planned source / preparation | Completion evidence |
|---|---|---|
| Certificate of Completion | Faculty ka approved template/signature process; missing | Actual signed/approved certificate; invented signature nahi |
| Table of Contents | Final report ke headings/pages | Generated index matches final document |
| Problem Definition | PRD overview + PDF p4 | Accurate problem/background |
| Customer Requirement Specification | PRD traceability | All 27 requirements covered, proposals separated |
| Project Plan | TASKS | Actual milestones vs baseline |
| E-R Diagrams | SYSTEM_DESIGN | Final schema matches diagram |
| Algorithms | Future service/flow explanations | Membership/access, submit, review/winner logic matches code |
| GUI Standards Document | DESIGN tokens/accessibility | Implemented rules with evidence |
| Interface Design Document | DESIGN layouts plus future actual screens | Real screenshots/routes/states |
| Task Sheet | TASKS with actual status/owners | Dated completion and verification |
| Project Review and Monitoring Report | Two status reports + final review | Actual progress/changes/blockers |
| Unit Testing Check List | Test inventory + future runner evidence | Actual results/date/build |
| Final Check List | This plan converted to completed evidence | Each item passed or accepted exception |

Final readiness additionally includes REQ-021 synopsis, commented source code and logic documentation; REQ-022 backup/restore; REQ-026 soft-copy docs + original feedback form; REQ-027 environment record. Actual source/build package format, recipient, ZIP name/size, portal upload process OD-06 mein pending; checkpoint and final dates are maintained privately; exact time/timezone OD-04 mein pending. Demo credentials provisioning method without real passwords public docs mein; configuration secrets package se excluded. Final subject `PROJECT SUBMISSION: ...`. No fabricated pass screenshots, forms, emails, payment receipts ya deployment links.

## Coverage aur exit rule

TC-001 to TC-027 exactly REQ-001 to REQ-027 cover karte hain; TASKS mein har REQ ka implementation/report owner task hai. Major modules: public content/search, membership/profile, admin/member content CRUD, feedback, contests/review/winner, FAQ aur deliverables. Source reading/document links/REQ coverage abhi check kiye ja sakte hain; runtime pass tabhi jab implementation aur actual evidence ho. Unresolved faculty decision se dependent tests Pending rahen, automatically Pass nahi.

### Original planning pass ka verification record - 26 September 2026

Six required Markdown files dobara read karke non-empty hone, relative links ke targets, code-fence balance aur table column consistency verify ki gayi. PRD mein 27 unique requirement rows, TASKS mein tamam 27 IDs aur 26 tasks, TEST_PLAN mein 27 mapped scenarios mile. Task dependency graph mein missing task ya cycle nahi mila. Mermaid ER relationships aur entity names manually review hue; diagram renderer execute nahi hua. Source PDF ke tamam 10 pages ki reading complete hai; koi substantive unreadable section nahi. Pre-existing 97 non-IDE files ka before/after SHA-256 comparison unchanged raha. Application build, runtime tests, browser E2E aur deployment is planning pass mein run nahi hue.


## Phase 1 actual verification - 26 September 2026

Latest scope: existing net10.0 MVC host + SQL Server/EF Core + Identity Member/Admin + register/login/logout + safe local admin seeding. No billing/contest assumptions implemented. Test source [FoundationTests.cs](../tests/JamesThew.Tests/FoundationTests.cs), isolated host [FoundationFixture.cs](../tests/JamesThew.Tests/FoundationFixture.cs).

| Evidence | Actual result |
|---|---|
| Build | Solution build succeeded, 0 warnings / 0 errors |
| Integration test run | 19 passed, 0 failed, 0 skipped; real SQL Server 2025 LocalDB |
| Migration | 20260926181611_InitialIdentity applied to JamesThew_Development; fresh isolated DB migration + repeat tested |
| Schema scope | Only AspNet* Identity tables and migration history; unique normalized email; DisplayName field |
| Auth/authorization | Member-only registration role, password hashing, generic invalid login, five-failure lockout, anonymous challenge, member denied Admin, Admin allowed |
| Input/session | Invalid registration, duplicate normalized email, privileged-field tampering, antiforgery, POST-only logout, Secure/HttpOnly cookies, external returnUrl rejection |
| Admin seed | New admin works; repeat does not reset password; existing Member promotion refused; missing/weak password rejected; Production/Staging rejected |
| Backup/restore | SQL BACKUP with CHECKSUM + RESTORE VERIFYONLY + actual restore to isolated DB; user password hash/roles/migration state verified; disposable test DB cleaned up |
| Browser visual check | Blocked: localhost HTTPS certificate valid but untrusted (ERR_CERT_AUTHORITY_INVALID). No trust warning bypass; browser appearance not marked passed |
| Git | Repository initialized and ignore rules added; no commit created |

First test run 18/19 tha: GET logout routing returned 404 instead of expected 405. Test ab 404/405 dono accept karta hai aur verify karta hai ke session unchanged rahe, token-less POST reject ho, valid POST logout access remove kare. Final rerun 19/19 pass. TRX output local `artifacts/TestResults/foundation.trx` mein hai (ignored); run commands README mein.

REQ-002 ka auth hissa verified hai; subscription/payment portion pending. REQ-005 ka editable profile pending. REQ-022 ka foundation backup/restore verified, daily backup operational schedule user ko maintain karna hai. REQ-027 local versions verified, institutional approval independent hai. Full TC-001 to TC-027 suite ko completed na samjhein.


## Phase 1 changed-file inventory

The inventory below is the prior implementation handoff's historical classification. At that review the repository had no HEAD, so Git cannot independently reproduce those historical updated/unchanged counts. The 27 September acceptance review below records the exact current Git inventory separately.

Final handoff review: 27 September 2026. Baseline hash comparison mein 10 existing files updated, 23 new source/config/test files, 78 existing files unchanged aur koi original file deleted nahi. Build artifacts, .vs aur new .git metadata is count se excluded hain. No passwords/API keys/credential-bearing connection strings source mein add nahi kiye gaye.

| Status | Exact file |
|---|---|
| Updated | [JamesThew.slnx](../JamesThew.slnx) |
| Updated | [JamesThew/Controllers/HomeController.cs](../JamesThew/Controllers/HomeController.cs) |
| Updated | [JamesThew/JamesThew.csproj](../JamesThew/JamesThew.csproj) |
| Updated | [JamesThew/Program.cs](../JamesThew/Program.cs) |
| Updated | [JamesThew/Views/Shared/_Layout.cshtml](../JamesThew/Views/Shared/_Layout.cshtml) |
| Updated | [README.md](../README.md) |
| Updated | [docs/PRD.md](../docs/PRD.md) |
| Updated | [docs/SYSTEM_DESIGN.md](../docs/SYSTEM_DESIGN.md) |
| Updated | [docs/TASKS.md](../docs/TASKS.md) |
| Updated | [docs/TEST_PLAN.md](../docs/TEST_PLAN.md) |
| New | [.config/dotnet-tools.json](../.config/dotnet-tools.json) |
| New | [.gitignore](../.gitignore) |
| New | [JamesThew/Authorization/AppPolicies.cs](../JamesThew/Authorization/AppPolicies.cs) |
| New | [JamesThew/Authorization/AppRoles.cs](../JamesThew/Authorization/AppRoles.cs) |
| New | [JamesThew/Controllers/AccountController.cs](../JamesThew/Controllers/AccountController.cs) |
| New | [JamesThew/Controllers/AdminController.cs](../JamesThew/Controllers/AdminController.cs) |
| New | [JamesThew/Data/ApplicationDbContext.cs](../JamesThew/Data/ApplicationDbContext.cs) |
| New | [JamesThew/Data/IdentitySeeder.cs](../JamesThew/Data/IdentitySeeder.cs) |
| New | [JamesThew/Data/Migrations/20260926181611_InitialIdentity.Designer.cs](../JamesThew/Data/Migrations/20260926181611_InitialIdentity.Designer.cs) |
| New | [JamesThew/Data/Migrations/20260926181611_InitialIdentity.cs](../JamesThew/Data/Migrations/20260926181611_InitialIdentity.cs) |
| New | [JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs](../JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs) |
| New | [JamesThew/Models/ApplicationUser.cs](../JamesThew/Models/ApplicationUser.cs) |
| New | [JamesThew/ViewModels/LoginViewModel.cs](../JamesThew/ViewModels/LoginViewModel.cs) |
| New | [JamesThew/ViewModels/RegisterViewModel.cs](../JamesThew/ViewModels/RegisterViewModel.cs) |
| New | [JamesThew/Views/Account/AccessDenied.cshtml](../JamesThew/Views/Account/AccessDenied.cshtml) |
| New | [JamesThew/Views/Account/Login.cshtml](../JamesThew/Views/Account/Login.cshtml) |
| New | [JamesThew/Views/Account/Register.cshtml](../JamesThew/Views/Account/Register.cshtml) |
| New | [JamesThew/Views/Account/Status.cshtml](../JamesThew/Views/Account/Status.cshtml) |
| New | [JamesThew/Views/Admin/Index.cshtml](../JamesThew/Views/Admin/Index.cshtml) |
| New | [JamesThew/Views/Shared/_LoginPartial.cshtml](../JamesThew/Views/Shared/_LoginPartial.cshtml) |
| New | [tests/JamesThew.Tests/FoundationFixture.cs](../tests/JamesThew.Tests/FoundationFixture.cs) |
| New | [tests/JamesThew.Tests/FoundationTests.cs](../tests/JamesThew.Tests/FoundationTests.cs) |
| New | [tests/JamesThew.Tests/JamesThew.Tests.csproj](../tests/JamesThew.Tests/JamesThew.Tests.csproj) |

## Phase 1 acceptance review - 27 September 2026

Scoped Phase 1 acceptance **passed**; this record accompanies the authorized Phase 1 Git checkpoint. No Phase 2 implementation was started.

| Check | Actual result |
|---|---|
| Restore | Local dotnet-ef 10.0.12 tool restore and solution restore passed |
| Build after fix | Passed, 0 warnings / 0 errors |
| Tests after fix | 19 passed, 0 failed, 0 skipped; isolated SQL LocalDB and actual backup/restore; `artifacts/TestResults/acceptance.trx` |
| Pending model changes | CLI check passed: no changes since the last migration |
| Certificate | `dotnet dev-certs https --check --trust` found trusted localhost certificate (machine-specific fingerprint omitted); also present in CurrentUser Root, valid through 26 September 2027. No trust-store modification or certificate-warning bypass performed |
| HTTPS | Normal certificate-validating HTTPS request returned 200; Codex in-app browser opened `https://localhost:7054` without a certificate warning |
| Browser anonymous access | `/account/status` and `/admin` both redirected to Login with a local ReturnUrl |
| Browser registration and Member | Registered a unique `phase1-...@example.test` Member with a runtime-generated password; account page showed the correct display name and no paid entitlement; `/admin` showed Access denied |
| Browser login/logout | Logout restored anonymous navigation and protected account access challenged Login; login with the same Member succeeded; logged out again |
| Visual review | Desktop registration, account and Admin pages inspected; forms/text/navigation readable. Full cross-browser, mobile and assistive-technology coverage not claimed |
| Browser Admin | Passed: user explicitly supplied chosen credentials; configured via User Secrets, ran Development-only --seed-admin successfully, then removed LocalAdmin:Password. Browser login reached Admin access, account page succeeded, logout restored the login challenge. No credentials in source or application logs |
| Git gate | Started with no HEAD and 110 nonignored untracked files; all scoped acceptance checks passed, authorizing one initial Phase 1 commit. Use git log -1 for its identity |

### Concrete source findings and review coverage

| Files | Finding / disposition |
|---|---|
| `JamesThew/Views/Shared/_Layout.cshtml` | Fixed: navbar toggle's `aria-controls="navbarSupportedContent"` referenced a nonexistent ID. Added the matching ID to the collapsible navigation; rebuilt and observed the target in browser accessibility state |
| `JamesThew/Controllers/AccountController.cs`, `ViewModels/RegisterViewModel.cs`, `ViewModels/LoginViewModel.cs` | No confirmed security defect: allow-listed registration input, fixed Member role, transaction around creation/assignment, encoded Razor output, local-only redirect, generic login failure, lockout and POST logout. Global antiforgery filter covers mutation actions |
| `JamesThew/Program.cs`, `Authorization/AppPolicies.cs`, `Authorization/AppRoles.cs`, `Controllers/AdminController.cs` | No confirmed authorization defect: authenticated fallback, explicit Member/Admin policies, secure HttpOnly auth cookie. MemberAccount is not subscription authorization |
| `JamesThew/Data/IdentitySeeder.cs` | No confirmed defect in documented single-process local seed flow: Development-only explicit command, chosen configuration credentials, refuses Member promotion, existing Admin not reset, atomic new Admin creation/assignment |
| `JamesThew/Data/ApplicationDbContext.cs`, `Models/ApplicationUser.cs`, `Data/Migrations/*` | Identity-only schema, unique normalized email, foreign keys and snapshot reviewed; migration/model checks passed |
| `tests/JamesThew.Tests/FoundationFixture.cs`, `FoundationTests.cs` | Real SQL tests isolate development data and generate ephemeral passwords. Coverage limitation: sequential duplicate/seed tests do not prove concurrent first-start seeding; failures after user creation but before role assignment are not fault-injected. No claim of exhaustive concurrency/failure-recovery verification |

The security plugin's diff workflow could not initialize because it requires a resolvable HEAD. The results above are a direct scoped source acceptance review, not a completed plugin security scan. No separate scan was substituted or claimed complete.

### Exact Git inventory and preservation

`git ls-files --others --exclude-standard` returned 110 paths before edits; the complete path list is in local ignored `artifacts/Acceptance/initial-files.txt`, with SHA-256 values in `initial-hashes.csv`. Final inventory and hash comparison are in `final-files.txt` and `preservation.json` in the same folder. Git reports these files as untracked, not modifications against a commit. This review changes only `README.md`, `_Layout.cshtml`, `docs/TASKS.md` and `docs/TEST_PLAN.md`; all other initial file bytes are preserved. No initial files deleted; the original local checkpoint included the project, docs, PDFs and vendor assets; public preparation removes both PDFs from tracking while preserving their local bytes. The temporary browser Member remains in the development database; the integration-test databases are cleaned by the fixture. Admin seed ran only after the user supplied and authorized chosen credentials.

Git whitespace check reported one pre-existing trailing-space line in vendor jquery.validate.unobtrusive.js:17; preserved unchanged. Scoped application/document whitespace check passes. This is not an acceptance failure.

Commit author identity supplied by the user and configured only in this repository. The original acceptance inventory held 110 files; the public checkpoint tracks 108 after excluding both local PDFs; exact commit identity is recorded in Git history. Acceptance checks passed.

## Public repository preparation

Before first publication, the unpublished root commit was amended to exclude both source PDFs while retaining their local copies. Documentation removes local user paths, attachment IDs, student portal identifiers and academic calendar details; relative reporting requirements remain. PDF download links were replaced with local-source explanations. Local certificate fingerprint and source PDF hash were omitted. No application code changed. Historical acceptance evidence above remains valid.

## Phase 2 actual verification - 27 September 2026

Scope: Public read-only ASP.NET Core MVC website (`T-07` and `T-08`), including shared layout, Home page, all 7 required FAQs, public recipe/tip catalogs and search, Free guest access, Members-Only locked preview with zero leakage, no-cache headers, demo content seeder, DB entities and migration. Test source [PublicContentTests.cs](../tests/JamesThew.Tests/PublicContentTests.cs) and updated [FoundationTests.cs](../tests/JamesThew.Tests/FoundationTests.cs).

| Check | Actual result |
|---|---|
| Build | Passed, 0 warnings / 0 errors (`dotnet build JamesThew.slnx`) |
| Test suite run | 26 passed, 0 failed, 0 skipped (19 Foundation + 7 PublicContent); `artifacts/TestResults/phase2.trx` |
| EF Migration | `20260927100700_Phase2PublicContent` applied to `JamesThew_Development`; CLI check `dotnet ef migrations has-pending-model-changes` confirmed 0 pending model changes |
| Demo content seeding | `ContentSeeder.cs` runs idempotently on startup, seeding all 7 PRD CRS 7 FAQs, 4 recipes (3 Free, 1 Paid), and 3 tips (2 Free, 1 Paid), clearly tagged `[DEMO CONTENT]` |
| TC-001 Shared Layout & Home | Responsive 7-item navigation menu, keyboard skip link (`#main-content`), mobile drawer, 4-column culinary footer, hero culinary presentation, quick search, featured cards, tier comparison ($10/mo, $100/yr demo) |
| TC-003 Free Recipe Access | Guest access to Free recipe (`/recipes/jamess-classic-roast-herb-chicken`) returns full ingredients and preparation steps; no login prompt |
| TC-004 Paid Recipe Lockout | Guest and Member requests to Members-Only recipe (`/recipes/jamess-masterclass-beef-wellington`) show locked preview box and login/join CTA; zero ingredients or steps in HTML payload; `Cache-Control: no-cache, no-store, must-revalidate` set |
| TC-003 Free Tip Access | Guest access to Free tip (`/tips/mastering-chefs-knife-grip-and-precision-cuts`) returns complete culinary guide text |
| TC-004 Paid Tip Lockout | Guest and Member requests to Members-Only tip (`/tips/masterclass-french-sauce-emulsions-and-pan-deglazing`) show locked preview box; protected tip body completely omitted from response payload; `Cache-Control: no-cache, no-store, must-revalidate` set |
| TC-008 Search Isolation | Public search for protected body text (`"chilled butter cubes"`, `"tenderloin"`) returns 0 results for unauthorized users. Search for Free titles/summaries returns correct items |
| TC-020 Seven FAQ Answers | Semantic HTML5 `<details>`/`<summary>` elements render all 7 required answers from PRD CRS 7 p5-6; fully functional without JavaScript; jump anchors and policy links verified |
| Authorization boundaries | Phase 3 membership approval is not implemented yet; ordinary Member accounts do NOT grant access to paid content. Only Admin role unlocks paid masterclasses in Phase 2 |
| Informational menus | Placeholder routes (`/contests`, `/announcements`, `/feedback`) provide complete navigation structure without dead links, clearly stating phase deferral |
| Live HTTPS verification | Verified via trusted HTTPS (`https://localhost:7054`) with `curl`; status 200 on all public endpoints, headers verified, zero secret leakage in response bodies |
| Tool environment note | Playwright browser subagent encountered an upstream Azure CDN 404 for driver zip (`playwright-1.57.0-win32_x64.zip`); automated verification was executed via direct HTTPS integration checks and curl assertions |

### Phase 2 changed-file inventory

| Status | File path | Description |
|---|---|---|
| Updated | [JamesThew/Controllers/HomeController.cs](../JamesThew/Controllers/HomeController.cs) | Injected IContentService, query featured recipes/tips/FAQs for Home view |
| Updated | [JamesThew/Data/ApplicationDbContext.cs](../JamesThew/Data/ApplicationDbContext.cs) | Added ContentItems, Recipes, RecipeIngredients, RecipeSteps, Tips, FaqItems DbSets, unique slug index and constraints |
| Updated | [JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs](../JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs) | Updated EF Core model snapshot for Phase 2 entities |
| Updated | [JamesThew/Program.cs](../JamesThew/Program.cs) | Registered IContentService DI, added ContentSeeder execution on startup |
| Updated | [JamesThew/Views/Home/Index.cshtml](../JamesThew/Views/Home/Index.cshtml) | Hero banner, search bar, featured cards, tier comparison, FAQ preview |
| Updated | [JamesThew/Views/Shared/_Layout.cshtml](../JamesThew/Views/Shared/_Layout.cshtml) | 7-item navigation menu, skip link, mobile toggle, 4-column footer |
| Updated | [JamesThew/Views/Shared/_LoginPartial.cshtml](../JamesThew/Views/Shared/_LoginPartial.cshtml) | Culinary styled auth buttons and account links |
| Updated | [JamesThew/wwwroot/css/site.css](../JamesThew/wwwroot/css/site.css) | Culinary brand variables, card layouts, locked box styles, responsive rules |
| Updated | [JamesThew/wwwroot/js/site.js](../JamesThew/wwwroot/js/site.js) | Accessible drawer toggle, Escape key listener, FAQ hash deep-linking |
| Updated | [tests/JamesThew.Tests/FoundationTests.cs](../tests/JamesThew.Tests/FoundationTests.cs) | Updated schema table assertions to include Phase 2 public content tables |
| New | [JamesThew/Authorization/ContentAccessHelper.cs](../JamesThew/Authorization/ContentAccessHelper.cs) | Authorization rule: only Admin unlocks paid content in Phase 2 |
| New | [JamesThew/Controllers/AnnouncementsController.cs](../JamesThew/Controllers/AnnouncementsController.cs) | Informational placeholder for Announcements (Phase 5) |
| New | [JamesThew/Controllers/ContestsController.cs](../JamesThew/Controllers/ContestsController.cs) | Informational placeholder for Contests (Phase 5) |
| New | [JamesThew/Controllers/FaqController.cs](../JamesThew/Controllers/FaqController.cs) | FAQ controller querying published FAQs |
| New | [JamesThew/Controllers/FeedbackController.cs](../JamesThew/Controllers/FeedbackController.cs) | Informational placeholder for Feedback (Phase 3) |
| New | [JamesThew/Controllers/RecipesController.cs](../JamesThew/Controllers/RecipesController.cs) | Recipe catalog, search, filtering, and detail endpoint with lock enforcement |
| New | [JamesThew/Controllers/TipsController.cs](../JamesThew/Controllers/TipsController.cs) | Cooking tips catalog, search, filtering, and detail endpoint with lock enforcement |
| New | [JamesThew/Data/ContentSeeder.cs](../JamesThew/Data/ContentSeeder.cs) | Idempotent seeder for 7 FAQs, 4 recipes, and 3 tips ([DEMO CONTENT]) |
| New | [JamesThew/Data/Migrations/20260927100700_Phase2PublicContent.cs](../JamesThew/Data/Migrations/20260927100700_Phase2PublicContent.cs) | EF Core migration creating public content tables |
| New | [JamesThew/Data/Migrations/20260927100700_Phase2PublicContent.Designer.cs](../JamesThew/Data/Migrations/20260927100700_Phase2PublicContent.Designer.cs) | EF Core migration designer file |
| New | [JamesThew/Models/ContentEnums.cs](../JamesThew/Models/ContentEnums.cs) | ContentKind, ContentOrigin, ContentVisibility, PublicationStatus enums |
| New | [JamesThew/Models/ContentItem.cs](../JamesThew/Models/ContentItem.cs) | Core content aggregate root entity |
| New | [JamesThew/Models/Recipe.cs](../JamesThew/Models/Recipe.cs) | Recipe entity 1-to-1 with ContentItem |
| New | [JamesThew/Models/RecipeIngredient.cs](../JamesThew/Models/RecipeIngredient.cs) | Recipe ingredient child entity |
| New | [JamesThew/Models/RecipeStep.cs](../JamesThew/Models/RecipeStep.cs) | Recipe step child entity |
| New | [JamesThew/Models/Tip.cs](../JamesThew/Models/Tip.cs) | Tip entity 1-to-1 with ContentItem |
| New | [JamesThew/Models/FaqItem.cs](../JamesThew/Models/FaqItem.cs) | FAQ item entity with QuestionKey and SortOrder |
| New | [JamesThew/Services/IContentService.cs](../JamesThew/Services/IContentService.cs) | Public content service contract |
| New | [JamesThew/Services/ContentService.cs](../JamesThew/Services/ContentService.cs) | Public content service implementation with leak-proof search & detail queries |
| New | [JamesThew/ViewModels/PublicContentViewModels.cs](../JamesThew/ViewModels/PublicContentViewModels.cs) | View models for Home, Recipes, Tips, and FAQ pages |
| New | [JamesThew/Views/Announcements/Index.cshtml](../JamesThew/Views/Announcements/Index.cshtml) | Informational announcement placeholder view |
| New | [JamesThew/Views/Contests/Index.cshtml](../JamesThew/Views/Contests/Index.cshtml) | Informational contest placeholder view |
| New | [JamesThew/Views/Faq/Index.cshtml](../JamesThew/Views/Faq/Index.cshtml) | Accessible 7-item FAQ view with details/summary |
| New | [JamesThew/Views/Feedback/Index.cshtml](../JamesThew/Views/Feedback/Index.cshtml) | Informational feedback placeholder view |
| New | [JamesThew/Views/Recipes/Index.cshtml](../JamesThew/Views/Recipes/Index.cshtml) | Recipe catalog view with search and filters |
| New | [JamesThew/Views/Recipes/Detail.cshtml](../JamesThew/Views/Recipes/Detail.cshtml) | Recipe detail view with Free content and Members-Only locked state |
| New | [JamesThew/Views/Tips/Index.cshtml](../JamesThew/Views/Tips/Index.cshtml) | Cooking tips catalog view with search and filters |
| New | [JamesThew/Views/Tips/Detail.cshtml](../JamesThew/Views/Tips/Detail.cshtml) | Tip detail view with Free content and Members-Only locked state |
| New | [tests/JamesThew.Tests/PublicContentTests.cs](../tests/JamesThew.Tests/PublicContentTests.cs) | 7 integration tests for TC-001, TC-003, TC-004, TC-008, TC-020 |
| New | `JamesThew/wwwroot/images/` | High quality culinary photography assets for hero, recipes, and tips |

## Phase 3A actual verification - 27 September 2026

Scope: Manual demo membership subscription requests ($10/mo, $100/yr), pending queue, admin approval/rejection with notes, active subscription window enforcement, and paid masterclass unlocking for approved members. No real payment gateway; no guest request submission. Phase 3B profile editing and feedback submission remain deferred.

| Evidence | Actual result |
|---|---|
| Build | Solution build succeeded, 0 warnings / 0 errors (`dotnet build JamesThew.slnx`) |
| Integration test suite | 32 passed, 0 failed, 0 skipped (`dotnet test JamesThew.slnx`) across Foundation, PublicContent, and Subscription test classes |
| Migration | `20260927143304_Phase3AMembershipSubscriptions` applied to `JamesThew_Development`; 0 pending model changes |
| Guest subscription challenge | `GET /membership` displays demo pricing and requires login; `POST /membership/subscribe` as guest redirects to `/Account/Login` with 302 |
| Member subscription submission | Authenticated member can submit demo request ($10 monthly or $100 yearly); enters `Pending` state; duplicate pending submission blocked |
| Pending / Rejected state enforcement | Pending or Rejected member cannot view protected recipe ingredients/steps or masterclass tip body; locked preview box displayed with CTA button |
| Admin subscription management | Admin accesses `/admin/subscriptions`, views pending requests queue, approves or rejects with optional admin notes |
| Content unlocking | Upon Admin approval, Member immediately gains full access to paid recipes and tips; no locked box rendered |
| Rejection with feedback | Upon Admin rejection, Member sees rejection notice and admin reason on `/membership`; content remains securely locked |
| Admin role bypass | Admin account continues to bypass locked masterclasses regardless of personal subscription status |
| Cache control | `Cache-Control: no-cache, no-store, must-revalidate` response header verified on all locked responses |

### Phase 3A changed-file inventory

| Status | File path | Description |
|---|---|---|
| Updated | [JamesThew/Models/ApplicationUser.cs](../JamesThew/Models/ApplicationUser.cs) | Added SubscriptionRequests navigation collection |
| Updated | [JamesThew/Data/ApplicationDbContext.cs](../JamesThew/Data/ApplicationDbContext.cs) | Configured SubscriptionRequest DbSet, foreign key, indexes, precision |
| Updated | [JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs](../JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs) | Updated EF Core model snapshot with SubscriptionRequest entity |
| Updated | [JamesThew/Program.cs](../JamesThew/Program.cs) | Registered ISubscriptionService / SubscriptionService in DI container |
| Updated | [JamesThew/Authorization/ContentAccessHelper.cs](../JamesThew/Authorization/ContentAccessHelper.cs) | Added hasActiveSubscription parameter for paid content access |
| Updated | [JamesThew/Controllers/RecipesController.cs](../JamesThew/Controllers/RecipesController.cs) | Injected ISubscriptionService, checks user subscription before rendering |
| Updated | [JamesThew/Controllers/TipsController.cs](../JamesThew/Controllers/TipsController.cs) | Injected ISubscriptionService, checks user subscription before rendering |
| Updated | [JamesThew/Controllers/HomeController.cs](../JamesThew/Controllers/HomeController.cs) | Injected ISubscriptionService for home preview access checks |
| Updated | [JamesThew/Controllers/AdminController.cs](../JamesThew/Controllers/AdminController.cs) | Added Subscriptions GET list, approve POST, reject POST actions |
| Updated | [JamesThew/Views/Admin/Index.cshtml](../JamesThew/Views/Admin/Index.cshtml) | Added Subscriptions management card with dynamic pending count badge |
| Updated | [JamesThew/Views/Shared/_LoginPartial.cshtml](../JamesThew/Views/Shared/_LoginPartial.cshtml) | Added Membership nav link for authenticated and guest users |
| Updated | [JamesThew/Views/Account/Status.cshtml](../JamesThew/Views/Account/Status.cshtml) | Added Membership card linking to `/membership` |
| Updated | [JamesThew/Views/Recipes/Detail.cshtml](../JamesThew/Views/Recipes/Detail.cshtml) | Updated locked preview CTA button to point to `/membership` for members |
| Updated | [JamesThew/Views/Tips/Detail.cshtml](../JamesThew/Views/Tips/Detail.cshtml) | Updated locked preview CTA button to point to `/membership` for members |
| Updated | [tests/JamesThew.Tests/FoundationTests.cs](../tests/JamesThew.Tests/FoundationTests.cs) | Added SubscriptionRequests table to schema test assertions |
| New | [JamesThew/Models/SubscriptionEnums.cs](../JamesThew/Models/SubscriptionEnums.cs) | SubscriptionPlan (Monthly $10, Yearly $100) and SubscriptionStatus enums |
| New | [JamesThew/Models/SubscriptionRequest.cs](../JamesThew/Models/SubscriptionRequest.cs) | SubscriptionRequest entity model |
| New | [JamesThew/ViewModels/SubscriptionViewModels.cs](../JamesThew/ViewModels/SubscriptionViewModels.cs) | View models & DTOs for membership and admin subscription flows |
| New | [JamesThew/Services/ISubscriptionService.cs](../JamesThew/Services/ISubscriptionService.cs) | Service contract for subscription operations & paid access validation |
| New | [JamesThew/Services/SubscriptionService.cs](../JamesThew/Services/SubscriptionService.cs) | Subscription service implementation with EF Core queries and expiry logic |
| New | [JamesThew/Controllers/MembershipController.cs](../JamesThew/Controllers/MembershipController.cs) | Membership controller for viewing tiers, submitting requests, checking status |
| New | [JamesThew/Views/Membership/Index.cshtml](../JamesThew/Views/Membership/Index.cshtml) | Culinary membership page with tiers, request form, status banner, request history |
| New | [JamesThew/Views/Admin/Subscriptions.cshtml](../JamesThew/Views/Admin/Subscriptions.cshtml) | Admin subscriptions management view with status filters, approve/reject forms |
| New | [JamesThew/Data/Migrations/20260927143304_Phase3AMembershipSubscriptions.cs](../JamesThew/Data/Migrations/20260927143304_Phase3AMembershipSubscriptions.cs) | EF Core migration creating SubscriptionRequests table |
| New | [JamesThew/Data/Migrations/20260927143304_Phase3AMembershipSubscriptions.Designer.cs](../JamesThew/Data/Migrations/20260927143304_Phase3AMembershipSubscriptions.Designer.cs) | EF Core migration designer file |
| New | [tests/JamesThew.Tests/SubscriptionTests.cs](../tests/JamesThew.Tests/SubscriptionTests.cs) | 6 integration tests verifying guest denial, pending, approval, rejection, unlock, and admin bypass |

## Phase 3B actual verification - 27 September 2026

Scope: Member feedback intake and member content contribution intake (recipes and tips) with read-only admin review. Guests challenged/redirected to login. Submissions stored as `Pending` and strictly withheld from public recipe/tip catalogs, search, home teasers, and slug details. No auto-publishing; admin publishing/moderation actions deferred to Phase 4.

| Evidence | Actual result |
|---|---|
| Build | Solution build succeeded, 0 warnings / 0 errors (`dotnet build JamesThew.slnx`) |
| Integration test suite | 42 passed, 0 failed, 0 skipped (`dotnet test JamesThew.slnx`) across Foundation, PublicContent, Subscription, and ContributionAndFeedback test classes |
| Migration | `20260927150554_Phase3BMemberFeedbackAndContributions` applied to `JamesThew_Development`; 0 pending model changes |
| Guest feedback challenge | Unauthenticated `POST /feedback/submit` redirects to `/Account/Login` with 302; guest GET displays guidance and login CTA |
| Member feedback submission | Authenticated member can submit feedback with topic, rating, message; saved in DB as `Pending`; member views own history on `/feedback` |
| Feedback privacy | Submitted feedback is strictly private and never exposed on public pages or to guest sessions |
| Admin feedback review | Admin accesses `/admin/feedback`, views pending/reviewed feedback list with author details, rating, and message; regular member receives access denied |
| Guest recipe contribution challenge | Unauthenticated `POST /contributions/recipe/new` redirects to `/Account/Login` with 302 |
| Member recipe contribution | Authenticated member submits recipe with title, summary, servings, times, ingredients, steps, notes; saved with `Origin = Community`, `PublicationStatus = Pending` |
| Pending recipe exclusion | Pending recipe does NOT appear in public catalog (`/recipes`), search (`/recipes?q=...`), home featured recipes, and direct slug route returns 404 |
| Guest tip contribution challenge | Unauthenticated `POST /contributions/tip/new` redirects to `/Account/Login` with 302 |
| Member tip contribution | Authenticated member submits cooking tip with title, summary, body, notes; saved with `Origin = Community`, `PublicationStatus = Pending` |
| Pending tip exclusion | Pending tip does NOT appear in public catalog (`/tips`), search (`/tips?q=...`), and direct slug route returns 404 |
| Admin content intake review | Admin accesses `/admin/contributions`, views full submitted recipe ingredients, steps, and tip bodies; regular member receives access denied |

### Phase 3B changed-file inventory

| Status | File path | Description |
|---|---|---|
| Updated | [JamesThew/Models/ApplicationUser.cs](../JamesThew/Models/ApplicationUser.cs) | Added Feedbacks and ContributedContentItems navigation collections |
| Updated | [JamesThew/Models/ContentItem.cs](../JamesThew/Models/ContentItem.cs) | Added ContributorNotes property |
| Updated | [JamesThew/Data/ApplicationDbContext.cs](../JamesThew/Data/ApplicationDbContext.cs) | Added Feedbacks DbSet, configured relationships, indexes, constraints |
| Updated | [JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs](../JamesThew/Data/Migrations/ApplicationDbContextModelSnapshot.cs) | Updated EF Core model snapshot for Feedback and ContributorNotes |
| Updated | [JamesThew/Program.cs](../JamesThew/Program.cs) | Registered IFeedbackService and IContributionService in DI |
| Updated | [JamesThew/Controllers/FeedbackController.cs](../JamesThew/Controllers/FeedbackController.cs) | Implemented interactive feedback Index and Submit actions |
| Updated | [JamesThew/Controllers/AdminController.cs](../JamesThew/Controllers/AdminController.cs) | Added Feedback and Contributions read-only review actions, updated dashboard counters |
| Updated | [JamesThew/Views/Feedback/Index.cshtml](../JamesThew/Views/Feedback/Index.cshtml) | Interactive culinary feedback form, guest guidance card, user submission history |
| Updated | [JamesThew/Views/Admin/Index.cshtml](../JamesThew/Views/Admin/Index.cshtml) | Added Member Feedback and Content Intake cards with dynamic pending badges |
| Updated | [JamesThew/Views/Shared/_LoginPartial.cshtml](../JamesThew/Views/Shared/_LoginPartial.cshtml) | Added Contribute link for authenticated users |
| Updated | [JamesThew/Views/Account/Status.cshtml](../JamesThew/Views/Account/Status.cshtml) | Added quick action cards linking to My Contributions and Member Feedback |
| Updated | [tests/JamesThew.Tests/FoundationTests.cs](../tests/JamesThew.Tests/FoundationTests.cs) | Added Feedbacks table to schema test assertions |
| New | [JamesThew/Models/Feedback.cs](../JamesThew/Models/Feedback.cs) | Feedback entity model with FeedbackKind and FeedbackStatus enums |
| New | [JamesThew/ViewModels/FeedbackViewModels.cs](../JamesThew/ViewModels/FeedbackViewModels.cs) | View models & DTOs for feedback submission, history, and admin review |
| New | [JamesThew/ViewModels/ContributionViewModels.cs](../JamesThew/ViewModels/ContributionViewModels.cs) | View models & DTOs for recipe & tip contributions and admin review |
| New | [JamesThew/Services/IFeedbackService.cs](../JamesThew/Services/IFeedbackService.cs) | Feedback service contract |
| New | [JamesThew/Services/FeedbackService.cs](../JamesThew/Services/FeedbackService.cs) | Feedback service implementation |
| New | [JamesThew/Services/IContributionService.cs](../JamesThew/Services/IContributionService.cs) | Contribution service contract |
| New | [JamesThew/Services/ContributionService.cs](../JamesThew/Services/ContributionService.cs) | Contribution service implementation with multiline parsing & slug generation |
| New | [JamesThew/Controllers/ContributionsController.cs](../JamesThew/Controllers/ContributionsController.cs) | Member controller for contribution list and recipe/tip intake forms |
| New | [JamesThew/Views/Contributions/Index.cshtml](../JamesThew/Views/Contributions/Index.cshtml) | Member contributions dashboard with status tracking and create buttons |
| New | [JamesThew/Views/Contributions/NewRecipe.cshtml](../JamesThew/Views/Contributions/NewRecipe.cshtml) | Recipe contribution submission form with ingredients and steps intake |
| New | [JamesThew/Views/Contributions/NewTip.cshtml](../JamesThew/Views/Contributions/NewTip.cshtml) | Cooking tip contribution submission form with technique body intake |
| New | [JamesThew/Views/Admin/Feedback.cshtml](../JamesThew/Views/Admin/Feedback.cshtml) | Admin read-only feedback intake review view with status filters |
| New | [JamesThew/Views/Admin/Contributions.cshtml](../JamesThew/Views/Admin/Contributions.cshtml) | Admin read-only content intake review view with kind filters and collapsible content |
| New | [JamesThew/Data/Migrations/20260927150554_Phase3BMemberFeedbackAndContributions.cs](../JamesThew/Data/Migrations/20260927150554_Phase3BMemberFeedbackAndContributions.cs) | EF Core migration creating Feedbacks table and ContributorNotes column |
| New | [JamesThew/Data/Migrations/20260927150554_Phase3BMemberFeedbackAndContributions.Designer.cs](../JamesThew/Data/Migrations/20260927150554_Phase3BMemberFeedbackAndContributions.Designer.cs) | EF Core migration designer file |
| New | [tests/JamesThew.Tests/ContributionAndFeedbackTests.cs](../tests/JamesThew.Tests/ContributionAndFeedbackTests.cs) | 10 integration tests for feedback and contribution guest blocking, submission, pending exclusion, and admin view |

## Phase 3C actual verification - 27 September 2026

Scope: Admin moderation of member feedback and community content contributions (recipes & tips). Antiforgery-protected POST review actions, server-side validation, error handling for missing items and repeated submissions. Private feedback retention, public catalog appearance on approval, 404 isolation on rejection, rejection reason in member dashboard, and Members-Only paid lock preservation.

| Evidence | Actual result |
|---|---|
| Build | Solution build succeeded, 0 warnings / 0 errors (`dotnet build JamesThew.slnx`) |
| Integration test suite | 52 passed, 0 failed, 0 skipped (`dotnet test JamesThew.slnx`) across Foundation, PublicContent, Subscription, ContributionAndFeedback, and Moderation test classes |
| Migration check | `dotnet ef migrations has-pending-model-changes --project JamesThew/JamesThew.csproj` verified clean: 0 pending model changes |
| Guest moderation denial | Unauthenticated POST requests to `/admin/feedback/{id}/moderate`, `/admin/contributions/{id}/approve`, and `/admin/contributions/{id}/reject` redirect to `/account/login` (302) |
| Member moderation denial | Authenticated regular member POST requests to admin moderation endpoints are forbidden and redirect to `/account/access-denied` (302) |
| Feedback review & notes | Admin marks feedback as `Reviewed` with administrator note; member sees `Reviewed` status badge and the admin note in their history; feedback remains completely private from guests |
| Feedback edge cases | Moderating non-existent feedback ID or repeating identical status handled gracefully with informative warning alert |
| Recipe approval & publication | Admin approves recipe contribution; atomically sets `PublicationStatus.Published` and `Visibility.Free`; recipe immediately appears in public catalog `/recipes` and slug route `/recipes/{slug}` returns 200 OK with complete ingredients and steps |
| Recipe rejection & isolation | Admin rejects recipe contribution with reason; recipe is strictly omitted from public catalog and slug route returns 404; submitting member sees `Rejected` status and editorial feedback on `/contributions` |
| Tip approval & publication | Admin approves cooking tip contribution; tip immediately appears in public catalog `/tips` and slug route returns 200 OK with full body |
| Tip rejection & isolation | Admin rejects tip contribution with reason; tip remains hidden from public catalog and slug route returns 404 |
| Contribution edge cases | Moderating non-existent contribution ID or repeating approval/rejection handled gracefully with warning alert |
| Self-publishing blocked | Regular members cannot self-approve or elevate publication state; server-side values strictly enforced |
| Members-Only protection | If approved contribution has `Visibility = MembersOnly`, locked box is strictly rendered for guests with no-cache header, while approved members and admins unlock full technique body |

### Phase 3C changed-file inventory

| Status | File path | Description |
|---|---|---|
| Updated | [JamesThew/Models/Feedback.cs](../JamesThew/Models/Feedback.cs) | Added `Approved` and `Rejected` values to `FeedbackStatus` enum |
| Updated | [JamesThew/ViewModels/FeedbackViewModels.cs](../JamesThew/ViewModels/FeedbackViewModels.cs) | Added `AdminNotes` to `FeedbackHistoryItemDto` |
| Updated | [JamesThew/ViewModels/ContributionViewModels.cs](../JamesThew/ViewModels/ContributionViewModels.cs) | Added `RejectionReason` and `Visibility` to `MemberContributionListItemDto` and `AdminContributionItemDto`; added `FilterStatus` to `AdminContributionListViewModel` |
| Updated | [JamesThew/Services/IFeedbackService.cs](../JamesThew/Services/IFeedbackService.cs) | Added `ModerateFeedbackAsync` method contract |
| Updated | [JamesThew/Services/FeedbackService.cs](../JamesThew/Services/FeedbackService.cs) | Implemented `ModerateFeedbackAsync` and mapped `AdminNotes` in member history |
| Updated | [JamesThew/Services/IContributionService.cs](../JamesThew/Services/IContributionService.cs) | Added `ApproveContributionAsync`, `RejectContributionAsync`, and status filtering to `IContributionService` |
| Updated | [JamesThew/Services/ContributionService.cs](../JamesThew/Services/ContributionService.cs) | Implemented `ApproveContributionAsync`, `RejectContributionAsync`, status filtering, and rejection reason mapping |
| Updated | [JamesThew/Controllers/AdminController.cs](../JamesThew/Controllers/AdminController.cs) | Added `ModerateFeedback`, `ApproveContribution`, `RejectContribution` POST actions; updated `Contributions` GET to support status filtering |
| Updated | [JamesThew/Views/Admin/Feedback.cshtml](../JamesThew/Views/Admin/Feedback.cshtml) | Added inline feedback review form, admin notes input, status tabs, and TempData alerts |
| Updated | [JamesThew/Views/Admin/Contributions.cshtml](../JamesThew/Views/Admin/Contributions.cshtml) | Added Approve & Publish (Free) form, Reject form with reason, status filter pills, and live links |
| Updated | [JamesThew/Views/Contributions/Index.cshtml](../JamesThew/Views/Contributions/Index.cshtml) | Displayed live link for published items and editorial rejection reason for rejected items |
| Updated | [JamesThew/Views/Feedback/Index.cshtml](../JamesThew/Views/Feedback/Index.cshtml) | Displayed administrator notes and reviewed badge in member feedback history |
| Updated | [docs/TASKS.md](../docs/TASKS.md) | Documented Phase 3C completion, status matrix updates, and test results |
| Updated | [docs/TEST_PLAN.md](../docs/TEST_PLAN.md) | Documented Phase 3C verification results, scenario coverage, and changed-file inventory |
| Updated | [README.md](../README.md) | Updated Phase 3 completed capabilities, route documentation, and testing status |
| New | [tests/JamesThew.Tests/ModerationTests.cs](../tests/JamesThew.Tests/ModerationTests.cs) | 10 integration tests covering moderation authorization, transitions, public catalog appearance, 404 isolation, and member feedback visibility |

## Phase 4 Step 1 actual verification - 27 September 2026

Scope: Admin Editorial Content CRUD for existing content types (`Recipe` and `Tip`). Antiforgery-protected POST actions, view model server-side validation, duplicate slug automatic collision resolution, unpublish/soft-delete and restore policy with confirmation modal, guest/member access rejection, public visibility synchronization, and subscription-locked body protection preservation.

| Evidence | Actual result |
|---|---|
| Build | Solution build succeeded, 0 warnings / 0 errors (`dotnet build JamesThew.slnx`) |
| Integration test suite | 63 passed, 0 failed, 0 skipped (`dotnet test JamesThew.slnx`) across Foundation, PublicContent, Subscription, ContributionAndFeedback, Moderation, and EditorialContentCrud test classes |
| Migration check | `dotnet ef migrations has-pending-model-changes --project JamesThew/JamesThew.csproj` verified clean: 0 pending model changes |
| Guest admin denial | Unauthenticated requests to `/admin/content`, `/admin/content/recipes/new`, `/admin/content/tips/new`, and POST mutations redirect to `/account/login` (302) |
| Member admin denial | Authenticated regular member requests to editorial admin endpoints are forbidden and redirect to `/account/access-denied` (302) |
| Recipe creation & public view | Admin creates Free published recipe; immediately appears in `/recipes` catalog and `/recipes/{slug}` returns 200 OK with full ingredients and steps |
| Recipe editing & live sync | Admin updates recipe title, preparation time, and ingredients; updates immediately reflect on public slug route |
| Tip creation & editing | Admin creates and edits cooking tip; tip immediately accessible on `/tips` catalog and `/tips/{slug}` returns 200 OK with full technique body |
| Draft isolation | Admin creates recipe with `PublicationStatus = Draft`; item appears in `/admin/content?status=Draft`, strictly omitted from `/recipes`, and direct slug route returns 404 Not Found |
| Duplicate slug collision | Creating two items with identical titles generates unique slugs (e.g. `classic-roast-duck` and `classic-roast-duck-2`), preventing crashes and database key collisions |
| Soft-delete unpublish & restore | Admin removes editorial item via `POST /admin/content/{id}/remove`; item is soft-deleted (`DeletedAtUtc = now`), unpublishes immediately from catalog, and direct slug route returns 404; admin restores via `POST /admin/content/{id}/restore` and item re-appears publicly |
| Server-side validation | Submitting blank required fields (Title, Summary, Ingredients, Steps, Body) returns 200 OK with clear validation error summaries and preserves user input |
| Members-Only lock preservation | Members-Only recipes authored by Admin render teaser summary and locked subscription box for guests (`Cache-Control: no-cache`), while approved members unlock full recipe |
| Community moderation non-regression | Editorial CRUD strictly filters on `Origin == ContentOrigin.Editorial`; community contributions (`Origin == ContentOrigin.Community`) cannot be modified/removed via editorial endpoints and community moderation queues remain isolated |

### Phase 4 Step 1 changed-file inventory

| Status | File path | Description |
|---|---|---|
| New | [JamesThew/ViewModels/AdminEditorialViewModels.cs](../JamesThew/ViewModels/AdminEditorialViewModels.cs) | View models for editorial content listing, recipe creation/editing, and tip creation/editing with validation |
| New | [JamesThew/Services/IAdminContentService.cs](../JamesThew/Services/IAdminContentService.cs) | Service contract for editorial content retrieval, recipe saving, tip saving, soft-delete, and restore |
| New | [JamesThew/Services/AdminContentService.cs](../JamesThew/Services/AdminContentService.cs) | Service implementation for editorial CRUD, automatic slug collision resolution, multiline parsing, and soft delete/restore |
| Updated | [JamesThew/Program.cs](../JamesThew/Program.cs) | Registered `IAdminContentService` with DI container |
| Updated | [JamesThew/Controllers/AdminController.cs](../JamesThew/Controllers/AdminController.cs) | Added editorial content actions: `/admin/content`, `/admin/content/recipes/new`, `/admin/content/recipes/{id}/edit`, `/admin/content/tips/new`, `/admin/content/tips/{id}/edit`, `/admin/content/{id}/remove`, `/admin/content/{id}/restore` |
| Updated | [JamesThew/Views/Admin/Index.cshtml](../JamesThew/Views/Admin/Index.cshtml) | Added Editorial Content card to admin dashboard with live counts and quick action links |
| New | [JamesThew/Views/Admin/Content.cshtml](../JamesThew/Views/Admin/Content.cshtml) | Editorial catalog view with KPI metrics, kind/tier/status filters, removed toggle, and removal confirmation modal |
| New | [JamesThew/Views/Admin/RecipeForm.cshtml](../JamesThew/Views/Admin/RecipeForm.cshtml) | Editorial recipe authoring/edit form with multiline ingredients/steps and access tier controls |
| New | [JamesThew/Views/Admin/TipForm.cshtml](../JamesThew/Views/Admin/TipForm.cshtml) | Editorial cooking tip authoring/edit form with full technique body and access tier controls |
| New | [tests/JamesThew.Tests/EditorialContentCrudTests.cs](../tests/JamesThew.Tests/EditorialContentCrudTests.cs) | 11 integration tests covering admin authorization, CRUD, duplicate slugs, draft isolation, soft-delete/restore, and community non-regression |
| Updated | [docs/TASKS.md](../docs/TASKS.md) | Documented Phase 4 Step 1 completion, status matrix updates, and test results |
| Updated | [docs/TEST_PLAN.md](../docs/TEST_PLAN.md) | Documented Phase 4 Step 1 verification results, scenario coverage, and changed-file inventory |
| Updated | [README.md](../README.md) | Updated Phase 4 Step 1 completed capabilities, route documentation, and test status |




