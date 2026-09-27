# JamesThew.com - Phased Task Plan

## Execution contract

Yeh implementation backlog hai. Phase 1 foundation ke baad Phase 2 execute hui: public read-only ASP.NET Core MVC website, recipes/tips lists/details/search, 7 FAQs, guest vs paid locked preview states, demo content seeder, aur 26 integration tests pass. Future feature tasks (Phase 3 onwards: manual membership approval, member feedback, admin CRUD, member contributions, contests) Not started hain; actual status neeche hai. T-01 ka source/document baseline tayyar hai; latest user assumptions incorporated hain. Remaining faculty details aur screen review alag pending hain. [PRD](PRD.md) requirement authority aur Open Decisions rakhta hai; [TEST_PLAN](TEST_PLAN.md) expected evidence define karta hai. Har Task ID stable rahe; completion par date, evidence aur actual Git commit record ho.

Har task ki row mein scope, dependencies, acceptance aur verification hai. `G` ka matlab neeche common phase gate hai. REQ references coverage dikhate hain, implementation complete hone ka claim nahi. Har phase ke baad build, relevant tests, browser check aur Git checkpoint planned hain. Planning phase mein build/browser application tests N/A hain, kyunke app execute/change nahi ki ja rahi.

## ASP.NET Core MVC phase boundaries

Phase 1 aur Phase 2 complete ho chuki hain. Is phase mein public read-only website, content entities, migration, seeder, aur preview/locked authorization implement hue; Admin CRUD, payments, membership approval, feedback writes, contributions aur contests future phases ke liye deferred hain. Existing starter mein vertical MVC flow (route/controller -> service/policy -> view model -> Razor view -> integration tests) follow hua hai.

| Phase | MVC-specific output, future | Entry / exit boundary |
|---|---|---|
| 0 Planning | Approved assumptions, remaining version decisions, wireframes aur traceability | Planning baseline available; remaining faculty details tracked |
| 1 Foundation | Existing MVC host, DI/config, Identity-only DbContext/migration, roles, registration/login/logout aur safe local admin seed | Acceptance passed: build/schema/tests and trusted-HTTPS Member/Admin browser checks; Phase 1 Git checkpoint authorized |
| 2 Public read-only website | Home/Recipes/Tips/FAQ controllers, read services, public view models, Razor shared layout/search/detail, demo seeder, locked states | Acceptance passed: build 0 errors/0 warnings, 26/26 tests pass, migration applied, live HTTPS curl checks pass; Phase 2 Git checkpoint authorized |
| 3 Identity and membership | Profile editing/Membership controllers/views, admin manual demo approval, member-only feedback endpoints/forms; authentication Phase 1 mein complete | No gateway; pending approval cannot unlock member features; role/access tests |
| 4 Content and moderation | Admin area content controllers/views; member contributions; Pending approval queue; public publication | Published + Free after admin approval; pending hidden except owner/admin |
| 5 Contests and winners | Admin contest/review/result pages, member entry form/service, public contest/result views | Guest entry blocked; only admin manages winners/results |
| 6 UI polish | Razor partial consistency, responsive CSS/JS, selected optional motion | Keyboard/reduced-motion/browser evidence |
| 7 Local verification | Local fresh setup, EF migration/restore rehearsal, regression evidence, accurate README commands | Reproducible local academic demo; hosting not required |
| 8 Submission | Reports, source/docs/backup package, checkpoint and final readiness | Private academic schedule; no automatic email/send |

T-02 ka unresolved form/recipient/rubric work foundation ko blanket block nahi karta. Phase 1 ki local version compatibility verify aur implementation authorization fulfill ho chuki hai; forms/recipient final submission se pehle required hain. Approved guest/payment/moderation policies dobara approval ka intezar nahi karengi.

Task IDs stable rakhe gaye hain: T-09 ab Phase 3 mein T-10/T-11 ke baad hai kyunke feedback member-only ho gaya hai. Numeric ID ke bajaye phase aur dependency order follow karein. Har phase ka G tamam us phase ke tasks complete hone ke baad apply hoga.

## Phase 0 - Planning/design

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-01 / REQ-001, REQ-021, REQ-023, REQ-027 | Sources, 27 requirements aur six docs baseline | None | 10 PDF pages covered; inventory, proposals, open decisions distinct | Source-page review, links/REQ cross-check; user review pending |
| T-02 / REQ-002, REQ-011, REQ-017, REQ-019, REQ-024, REQ-025, REQ-026, REQ-027 | Resolved assumptions record; remaining OD details track | T-01 | Payment/guest/moderation/working dates resolved; local versions, missing forms aur contest details status separately recorded | Dated actual response/reference attach; no assumed approval |
| T-03 / REQ-001, REQ-003, REQ-004, REQ-005, REQ-020, REQ-023 | Key-page wireframes/state inventory approve | T-01; policy screens T-02 | Mobile/desktop home, detail, membership, entry, profile, admin sketches reviewed | DESIGN checklist, keyboard order walkthrough; G0 |

G0: Markdown review + traceability + source hash check. Application build/tests/browser N/A; proposed screen review applies. Original planning inspection mein Git repository nahi thi. Ab Phase 1 mein Git initialize hua; commit create nahi hua.

## Phase 1 - Foundation

### Actual scoped completion

User ki latest scope purane broad T-05/T-06 domain plan ko narrow karti hai: sirf ApplicationUser + built-in Identity tables. T-10 ka register/login/logout hissa Phase 1 mein explicitly moved; profile edit later. Payment/subscription, recipes/tips, feedback, contributions aur contests ki entities/seeds/migrations intentionally deferred hain.

| Item | Current status / evidence |
|---|---|
| T-04 | Complete for local scope: net10.0 preserved; SDK 10.0.401, runtime/packages 10.0.12; Git initialized, ignore rules; Phase 1 acceptance checkpoint |
| T-05 | Complete for narrowed foundation: SQL Server LocalDB, Identity-only migration, Member/Admin policies, account auth; broader domain schema deferred |
| T-06 | Complete for foundation checks: idempotent roles, Development-only one-shot admin seeder, isolated fresh migration and real SQL backup/restore tests; future domain fixtures deferred |
| T-10 authentication portion | Complete: registration/login/logout, server validation, lockout, CSRF and role enforcement; profile editing and membership still Not started |
| Foundation verification | Build 0 warnings/errors; 19/19 integration tests pass; migration applied and no model drift; evidence in TEST_PLAN |
| Browser/Git checkpoint | 27 September acceptance review: trusted HTTPS, registration/login/logout, Member account access and Member denial from Admin passed in Codex browser. Chosen-credential Admin seed, login, Admin/account access and logout also passed. Bootstrap password removed from User Secrets. Original acceptance inventory contained 110 files; public preparation excludes the two locally retained PDFs, leaving 108 tracked files. User-chosen author identity configured only in this repository; exact checkpoint commit is recorded in Git history. |

27 September acceptance review found a missing navbar target ID in `Views/Shared/_Layout.cshtml`; fixed and rebuilt, with 19/19 integration tests and no pending model changes. No confirmed auth/role/seeding/migration security defect found in the scoped source review. Exact current inventory and review limits are recorded in TEST_PLAN. Phase 2 remains Not started.

Rozana backups operational obligation hain; automatic schedule install nahi hua. User ko apne chosen local admin credentials User Secrets mein set karne hain; koi default credential nahi banaya gaya. [README](../README.md) mein verified migration/run/seed commands hain.


| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-04 / REQ-021, REQ-027 | Local approved runtime/packages, existing MVC starter assessment, DI/config aur Git setup | T-02 local version subset, T-03 aur next implementation instruction | Versions pinned/documented; meaningful code comments rule; source preservation | Restore/build approved environment par; baseline smoke; G |
| T-05 / REQ-002, REQ-005, REQ-006, REQ-007, REQ-010, REQ-017, REQ-019 | Identity-only EF Core DbContext/ApplicationUser/migration, Member/Admin roles/policies, account authentication | T-04 | Blank SQL DB migrate; unique email, Identity relationships and role boundaries verified; business aggregates deferred | Relational DB integration checks, invalid relationships fail |
| T-06 / REQ-021, REQ-022, REQ-023 | Role seed, explicit local admin bootstrap, isolated backup/restore evidence; future content seeds deferred | T-05 | Idempotent roles/admin seed; no hardcoded credentials; Identity backup restores; daily operation documented | Fresh restore and fixture comparison; dated backup log; G |

## Phase 2 - Public read-only MVC website

### Actual scoped completion

Phase 2 successfully implemented and verified:
- Shared responsive layout with culinary brand tokens (`--jt-canvas: #FFF9F0`, `--jt-primary: #9C3526`, `--jt-accent: #46583B`, `--jt-gold: #C89847`), skip link (`#main-content`), mobile navigation drawer, and 4-column culinary footer.
- Home page (`/`): hero presentation, quick search, featured recipes and tips, membership tier comparison cards ($10/mo, $100/yr demo), contests teaser, FAQ accordion preview.
- All 7 required FAQ items from PRD CRS 7 / p5-6 rendered with accessible semantic `<details>`/`<summary>` markup (works without JS), jump anchors, and policy links.
- Public recipe catalog (`/recipes`) and cooking tip catalog (`/tips`) with search, filter tabs (All, Free, Members-Only), and pagination.
- Recipe detail (`/recipes/{slug}`) and Tip detail (`/tips/{slug}`) with full guest access for Free content.
- Paid/Members-Only content locked state: protected ingredients, steps, and tip bodies are strictly withheld from guest and Member responses (Phase 3 approval not yet implemented; only Admin role unlocks masterclasses). `Cache-Control: no-cache, no-store, must-revalidate` set on locked responses. Search excludes protected text for unauthorized users.
- Database: EF Core aggregate entities (`ContentItem`, `Recipe`, `RecipeIngredient`, `RecipeStep`, `Tip`, `FaqItem`), migration `20260927100700_Phase2PublicContent` applied to LocalDB `JamesThew_Development`, 0 pending model changes.
- Seeder: `ContentSeeder.cs` runs idempotently on startup, adding 7 FAQs, 4 recipes (3 Free, 1 Paid), and 3 tips (2 Free, 1 Paid), clearly labelled as `[DEMO CONTENT]`.
- Testing: 26 passed, 0 failed, 0 skipped (19 Foundation + 7 PublicContent integration tests in `PublicContentTests.cs`).
- Live HTTPS verification: verified via `curl` on `https://localhost:7054` for all routes. Note: Playwright browser subagent encountered an upstream Azure CDN 404 for driver zip (`playwright-1.57.0-win32_x64.zip`), so live HTTPS curl checks were used for automated verification.

| Item | Current status / evidence |
|---|---|
| T-07 | Complete: Shared layout, James home, 7 FAQ answers with semantic details/summary, accessible navigation, and placeholder routes for menu completeness |
| T-08 | Complete: Recipe/tip catalogs, search filtering, Free guest access, locked preview state without protected body leakage, 0-leak search results, Admin-only bypass in Phase 2 |
| Phase 2 verification | Build 0 warnings/0 errors; 26/26 integration tests pass; migration applied and 0 model drift; live HTTPS checks pass; evidence in TEST_PLAN |

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-07 / REQ-001, REQ-020 | Shared responsive layout, James home, all seven FAQ answers | T-03, T-06 | Required menu/home details; FAQ aligned with policy | Passed: TC-001, TC-020; no-JS details/summary, skip link, responsive mobile drawer |
| T-08 / REQ-003, REQ-004, REQ-008 | Recipe/tip read + authorized search policies | T-06, T-07 | Free guest access; protected paid body; correct result counts | Passed: TC-003/004/008; 7 new integration tests, live HTTPS curl checks; G |

## Phase 3 - Identity, manual demo membership aur member feedback

### Actual scoped completion - Phase 3A: Manual Membership Approval Foundation

Phase 3A successfully implemented and verified:
- Subscription models & enums: `SubscriptionPlan` (Monthly $10, Yearly $100 demo), `SubscriptionStatus` (Pending, Approved, Rejected), `SubscriptionRequest` entity with relations, foreign keys, and indexes.
- EF Core Migration: `20260927143304_Phase3AMembershipSubscriptions` applied to `JamesThew_Development`, 0 pending model changes.
- Subscription service & helper: `ISubscriptionService` and `SubscriptionService` handling request submission, pending checks, admin approval/rejection with notes, active subscription validity window (30 days for monthly, 365 days for yearly), and `ContentAccessHelper` integration.
- Member membership page (`/membership`): Clear demo tier display ($10/mo, $100/yr), guest login notice, demo subscription request submission form with optional notes, real-time status banner (Pending / Active Masterclass Access / Rejected with reason), and user request history table.
- Admin subscriptions management (`/admin/subscriptions`): Admin-only management table with status filtering tabs (All, Pending, Approved, Rejected), subscriber email/name, plan, amount ($10.00 / $100.00), timestamps, inline Approve and Reject actions with admin notes. Admin dashboard (`/admin`) updated with Subscriptions card and dynamic pending count badge.
- Content access integration: Approved members now immediately unlock full recipe ingredients/steps and masterclass tip bodies; pending, rejected, and guest users continue to see locked preview boxes with `Cache-Control: no-cache, no-store, must-revalidate` headers. Admin role continues to bypass locked content.
- Testing: 32 passed, 0 failed, 0 skipped (19 Foundation + 7 PublicContent + 6 Subscription in `tests/JamesThew.Tests/SubscriptionTests.cs`).
### Actual scoped completion - Phase 3B: Member Feedback & Content Contribution Intake

Phase 3B successfully implemented and verified:
- Feedback model & entity: `Feedback` model with `FeedbackKind` (Site, Recipe), `FeedbackStatus` (Pending, Reviewed, Archived), ratings, categories, and foreign keys to `ApplicationUser` and `Recipe`.
- Content intake model: `ContentItem.ContributorNotes` column added, migration `20260927150554_Phase3BMemberFeedbackAndContributions` applied to `JamesThew_Development`, 0 pending model changes.
- Services: `IFeedbackService` / `FeedbackService` and `IContributionService` / `ContributionService` registered in DI.
- Member Feedback (`/feedback`): Interactive submission form with category selection, 1-5 rating, and message validation. Personal feedback history log. Guest challenge/redirect enforced. Feedback is private and never exposed publicly.
- Member Contributions (`/contributions`):
  - Recipe contribution intake (`/contributions/recipe/new`): title, summary, servings, prep/cook times, multiline ingredients and steps, and notes.
  - Tip contribution intake (`/contributions/tip/new`): title, summary, technique body, and notes.
  - Strict Pending state: Submissions are stored with `PublicationStatus = Pending` and `Origin = Community`. They do NOT appear in the public recipe catalog, tips catalog, search, home featured cards, or direct slug routes (which return 404).
  - Guest challenge/redirect enforced on all submission endpoints.
- Admin Read-Only Review:
  - Feedback intake review (`/admin/feedback`): filter by Pending / Reviewed, view submitter info, rating, topic, message.
  - Content intake review (`/admin/contributions`): filter by Recipes / Tips, view ingredients, steps, and tip body.
  - Admin dashboard (`/admin`) updated with dynamic counters for new feedback and pending intake.
  - Regular members accessing admin review pages receive access denied.
- Testing: 42 passed, 0 failed, 0 skipped (19 Foundation + 7 PublicContent + 6 Subscription + 10 Contribution & Feedback tests in `ContributionAndFeedbackTests.cs`).

### Actual scoped completion - Phase 3C: Admin Moderation of Member Feedback & Content Contributions

Phase 3C successfully implemented and verified:
- Feedback moderation workflow:
  - `POST /admin/feedback/{id}/moderate` with antiforgery token, status selection (`Reviewed`, `Archived`, `Approved`, `Rejected`), and administrator notes.
  - Feedback remains strictly private to author and admins; never published publicly or leaked to guests.
  - Member feedback history at `/feedback` displays `Reviewed` status badge and administrator response notes.
- Community contribution moderation workflow:
  - `POST /admin/contributions/{id}/approve` with antiforgery token; atomically sets `PublicationStatus = Published`, `Visibility = Free` (default documented rule), clears rejection reason, and updates `UpdatedAtUtc`.
  - `POST /admin/contributions/{id}/reject` with antiforgery token; sets `PublicationStatus = Rejected`, validates and records `RejectionReason` (up to 500 chars), and updates `UpdatedAtUtc`.
  - Missing IDs, repeated approvals, and invalid status transitions handled gracefully with user-facing alerts and zero unhandled exceptions.
- Content isolation and protection:
  - Pending and Rejected contributions are strictly withheld from `/recipes`, `/tips`, home featured cards, public search, and direct slug lookups (`404 Not Found`).
  - Approved Free contributions are immediately published to public catalogs, search, and direct slug routes with full ingredients, steps, and tip bodies.
  - Members-Only protection preserved: if an approved contribution has `Visibility = MembersOnly`, locked box is strictly rendered for guests and unapproved members, while approved members and admins unlock full technique bodies.
  - Submitting members cannot self-approve or elevate publication state.
- Member transparency:
  - Member contributions dashboard (`/contributions`) displays live links for `Published` items and editorial rejection reasons for `Rejected` items.
- Testing: 52 passed, 0 failed, 0 skipped (19 Foundation + 7 PublicContent + 6 Subscription + 10 Contribution & Feedback + 10 Moderation tests in `ModerationTests.cs`).

### Actual scoped completion - Phase 4 Step 1: Admin Editorial Content CRUD

Phase 4 Step 1 successfully implemented and verified:
- Editorial Content Types:
  - Official Chef James Thew masterclass recipes (`ContentKind.Recipe`) and cooking tips (`ContentKind.Tip`).
  - Strict isolation: All items authored via editorial CRUD are marked `ContentOrigin.Editorial`, strictly keeping them separate from member community contributions (`ContentOrigin.Community`).
- Admin Editorial Management (`/admin/content`):
  - Catalog dashboard with live metrics: active items count, recipe count, tip count, published count, draft count, and removed count.
  - Multi-dimensional filters: Content Kind (`All`, `Recipes`, `Tips`), Access Tier (`All`, `Free`, `MembersOnly`), Publication Status (`All`, `Published`, `Draft`), and Removed / Trash toggle (`showDeleted=true`).
  - Dynamic navigation card on the Admin Portal dashboard (`/admin`) displaying live editorial counts and quick-create action links.
- Recipe Authoring & Updating:
  - Create endpoint (`GET/POST /admin/content/recipes/new`): title, custom slug, gastronomic summary, static image URL, servings, prep/cook times, multiline ingredients and ordered preparation steps, access tier (`Free` vs `MembersOnly`), and publication status (`Published` vs `Draft`).
  - Edit endpoint (`GET/POST /admin/content/recipes/{id}/edit`): populates existing recipe details, preserves original slug if not modified, safely parses multiline changes, and saves timestamps.
- Cooking Tip Authoring & Updating:
  - Create endpoint (`GET/POST /admin/content/tips/new`): title, custom slug, summary teaser, static image URL, full technique/wisdom body, access tier, and publication status.
  - Edit endpoint (`GET/POST /admin/content/tips/{id}/edit`): pre-populates existing technique body and metadata for streamlined editing.
- Slug Management & Duplicate Collision Resolution:
  - Clean kebab-case slug generation from title (`GenerateSlug`).
  - Deterministic suffixing for collisions: If a slug already exists, automatically appends `-2`, `-3`, etc., avoiding collisions and race condition failures.
- Unpublishing & Soft Deletion Policy:
  - Destructive action confirmation: Modal dialog warns admin that unpublishing will immediately pull the content from public catalogs and cause direct links to return HTTP 404.
  - `POST /admin/content/{id}/remove` sets `DeletedAtUtc = DateTime.UtcNow`.
  - Item immediately disappears from public `/recipes`, `/tips`, search, and home featured cards. Direct visits to `/{kind}/{slug}` return HTTP 404.
  - `POST /admin/content/{id}/restore` sets `DeletedAtUtc = null`, restoring public visibility and slug routes.
- Security & Invariants:
  - Guest and regular Member access to editorial admin endpoints is strictly denied (redirecting to `/account/login` or `/account/access-denied`).
  - Anti-forgery validation (`[ValidateAntiForgeryToken]`) enforced on all mutation POST endpoints.
  - Members-Only subscription locking is preserved: unauthenticated visitors see teaser summaries with locked subscription banners, while authorized subscribers unlock full ingredients, steps, and techniques.
  - Community contribution moderation queues (`/admin/contributions`) and member intake endpoints remain completely untouched and isolated.
- Verification:
  - 63 passed, 0 failed, 0 skipped (52 previous + 11 new focused integration tests in `EditorialContentCrudTests.cs`).
  - `dotnet ef migrations has-pending-model-changes` verified clean: 0 pending model changes.
- Deferred Phase 4 Work:
  - Full file upload / media management system (storing binary files, media gallery picker, thumbnail generation) is deferred to the next step; image references currently use existing static asset paths.

| Item | Current status / evidence |
|---|---|
| T-11 | Complete (Phase 3A): Demo plan requests ($10 monthly, $100 yearly), pending queue, admin approval/rejection, activation window, zero real gateway |
| T-12 | Complete (Phase 3A): Content authorization matrix updated; approved members unlock paid content; pending/rejected/guest locked; admin bypasses |
| T-09 | Complete (Phase 3B): Member-only recipe/site feedback submission forms, database storage, guest redirect, private history, and admin review |
| T-14 intake portion | Complete (Phase 3B): Member recipe and tip contribution forms, multiline ingredients/steps, strict Pending storage, complete public exclusion, and admin read-only review |
| T-14 moderation portion | Complete (Phase 3C): Admin approve/reject workflows, atomical Free publication, public catalog appearance, 404 on rejection, rejection reason in member dashboard |
| T-15 feedback moderation | Complete (Phase 3C): Admin feedback review/notes workflow, Reviewed status, private member history notes, 0 public leakage |
| T-13 editorial CRUD | Complete (Phase 4 Step 1): Admin recipe & tip list, create, edit, unpublish (soft delete), restore, auto-slug collisions, and subscription lock preservation |
| T-10 | Auth complete in Phase 1; Profile edit deferred |
| Phase 4 Step 1 verification | Build 0 warnings/0 errors; 63/63 integration tests pass; migration check clean (0 pending model changes); evidence in TEST_PLAN |

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-10 / REQ-002, REQ-005 | Registration/login/logout DONE in Phase 1; profile edit deferred | T-05, T-07 | Own profile only; role fields ignored; pending != paid | TC-002/005 duplicate/login/profile tampering |
| T-11 / REQ-002, REQ-004 | Membership controller/views, demo plan request, admin approval/rejection, activation/expiry | T-02, T-10 | $10 monthly/$100 yearly demo labels; Pending until admin approval; rejected/expired excluded; no gateway | Passed: TC-002; 6 new integration tests in SubscriptionTests.cs, live HTTPS verified; G |
| T-12 / REQ-003, REQ-004, REQ-005, REQ-008 | Full guest/member/admin access integration | T-08, T-11 | UI and direct endpoints consistent; approved members unlock paid content; pending/rejected locked | Passed: TC-003/004/008; direct recipe & tip authorization assertions, no-cache headers; G |
| T-09 / REQ-013, REQ-015 | Member-only recipe/site FeedbackController, view models aur Razor forms | T-08, T-10, T-11 | Login + active member required; guest POST reject/no record; accessible recipe check; receipt | Passed: TC-013/015; 3 new integration tests in ContributionAndFeedbackTests.cs; G |
| T-14 / REQ-010, REQ-011 | Intake & Moderation: Member contribution forms, Pending storage, admin approve/reject, public publication, member dashboard | T-08, T-10, T-12 | Both types Pending; admin approval publishes Free; rejection displays reason; public isolation; member self-publish blocked | Passed: TC-010/011; 17 integration tests across ContributionAndFeedbackTests.cs and ModerationTests.cs; G |
| T-15 / REQ-013, REQ-014 | Admin feedback moderation & review inbox | T-09, T-14 | Admin notes, status transitions, member history reflection, private from public | Passed: TC-014; integration tests in ModerationTests.cs; G |
| T-13 / REQ-006, REQ-007, REQ-008, REQ-009 | Editorial Content CRUD: Admin recipe & tip create, edit, remove (soft-delete), restore, slug collision handling | T-12 | Multiline ingredients/steps; tip body; Free/Members-Only; Draft/Published; unique slug resolution; public visibility | Passed: TC-006 to TC-009; 11 new integration tests in EditorialContentCrudTests.cs; G |

## Phase 4 - Content management

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-13 / REQ-006, REQ-007, REQ-008, REQ-009 | Admin recipe/tip CRUD, visibility, validated media if approved | T-12 | Ingredients/steps valid; tips CRUD; free/paid changes enforce; soft-delete unpublish | TC-006 to TC-009; 11 integration tests in EditorialContentCrudTests.cs |
| T-14 / REQ-010, REQ-011, REQ-012 | Member submission/dashboard, admin contribution approval queue, public community catalog | T-13, T-02 | Both types Pending; own edits/deletes; admin approval publishes Free to guest/member; member self-publish blocked | TC-010/011/012 using members A/B |
| T-15 / REQ-013, REQ-014, REQ-015 | Admin feedback inbox, approved moderation queues | T-09, T-14 | All received recipe feedback visible; private account data protected | TC-014 and pending/approved/rejected transitions; G |
| T-14 / REQ-010, REQ-011, REQ-012 | Member submission/dashboard, admin contribution approval queue, public community catalog | T-13, T-02 | Both types Pending; own edits/deletes; admin approval publishes Free to guest/member; member self-publish blocked | TC-010/011/012 using members A/B |
| T-15 / REQ-013, REQ-014, REQ-015 | Admin feedback inbox, approved moderation queues | T-09, T-14 | All received recipe feedback visible; private account data protected | TC-014 and pending/approved/rejected transitions; G |

## Phase 5 - Contests aur announcements

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-16 / REQ-016, REQ-017 | Admin contests create/archive; public list/detail | T-12, T-02 | Rules/type/window displayed; removed contest closed to new entries | TC-016, server time/invalid window/role tests |
| T-17 / REQ-017, REQ-018 | Member-only MVC recipe/tip entry forms/services and admin review | T-16, T-14 | Guest GET/POST entry blocked; active member login required; complete snapshots and admin review | TC-017/018; anonymous denial, duplicate/type/window/member ownership |
| T-18 / REQ-019 | Winner choice, atomic announcement, archive display | T-17 | Valid reviewed same-contest winner; public result | TC-019 concurrency/cross-contest/zero-entry tests; G |

## Phase 6 - Polish

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-19 / REQ-001, REQ-003, REQ-005, REQ-017, REQ-020, REQ-023 | All pages/states responsive, accessibility, approved assets | T-15, T-18 | DESIGN states covered; licensed images; keyboard/touch paths work | Mobile/tablet/desktop, contrast, zoom, form errors |
| T-20 / REQ-001, REQ-017, REQ-023 | Optional selected motion, reduced-motion and performance budgets | T-19 | Content visible without JS/motion; lightweight animation | TEST_PLAN cross-cutting checks, actual performance evidence; G |

## Phase 7 - Testing aur release readiness

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-21 / REQ-001, REQ-002, REQ-003, REQ-004, REQ-005, REQ-006, REQ-007, REQ-008, REQ-009, REQ-010, REQ-011, REQ-012, REQ-013, REQ-014, REQ-015, REQ-016, REQ-017, REQ-018, REQ-019, REQ-020 | Functional/security E2E regression aur defect repair | T-20 | TC-001 to TC-020 pass or documented accepted exception | Guest/member/admin evidence; fix ke baad affected regression |
| T-22 / REQ-021, REQ-022, REQ-023, REQ-027 | Local fresh setup, migrations, restore aur academic-demo rehearsal; optional hosting excluded from gate | T-21 | Approved machine par reproducible setup; backup restore; accurate README | TC-021/022/023/027, actual commands record; G |

## Phase 8 - Reports aur submission

Yeh phase ki reporting tasks calendar se bhi trigger hongi; status mail ko final build tak delay na karein. Current planning day par jo kaam actually hua ho wahi report mein ho.

| Task ID / REQ | Scope | Dependencies | Acceptance criteria | Verification steps |
|---|---|---|---|---|
| T-23 / REQ-024, REQ-025 | Status report 1 aur review attachment | T-01; schedule/recipient T-02 | Actual progress, blockers, next work; correct subject | Confirmed date/recipient, attachment checklist; sending separately authorized |
| T-24 / REQ-024, REQ-025 | Status report 2 aur updated review | T-23; confirmed milestone | Actual completion/gaps and test state accurate | Report diff, remaining risks, dispatch evidence only if actually sent |
| T-25 / REQ-021, REQ-023, REQ-025, REQ-026 | Synopsis + complete final report, original feedback form, source package | T-22, T-24; forms from T-02 | All 13 report items + source + original form present; format verified | TEST_PLAN final checklist; clean package opens on another machine |
| T-26 / REQ-022, REQ-024, REQ-025, REQ-026, REQ-027 | Final readiness review and authorized submission | T-25 | Confirmed channel/date; final backup; no secrets; actual delivery receipt if sent | Package hash/version, G, recipient/file/readback check; no automatic send in this plan |

## Common phase gate G

1. Approved stack par clean build; applicable unit/integration tests. Test command actual repository banne par README mein record ho.
2. Phase ke relevant pages browser mein real seeded data ke saath; no-JS fallback where applicable, failed/empty states aur representative keyboard/mobile path.
3. Requirement evidence update; failed check unresolved ho to phase complete mark na karein. PDF p7 comments/logic review aur daily backup check.
4. Source diff review, secrets check, readable Git checkpoint. Git initialize hua ho tab actual commit; fictional hash nahi.

## Selected working reporting milestones

Source: local specification p9 requires two status reports at 10-day intervals from project start; projects shorter than 30 days use the 7-10 day / three-days-before-end rule. Exact academic dates are retained privately.

| Milestone | Relative milestone | Expected evidence |
|---|---|---|
| Status report 1 / T-23 | Start + 10 days | Actual progress description + review document; STATUS: prefix |
| Status report 2 / T-24 | Start + 20 days | Updated review, completed/pending work aur blockers |
| Internal submission/checkpoint | Private checkpoint | Local demo readiness, draft source/docs package, open-defect and missing-form list; unready work honestly record |
| Final academic due / T-26 | Private final deadline | Verified local package, source/report/form/backup evidence; actual delivery only after authorized send |

Yeh user-approved working schedule hai, jab tak faculty otherwise confirm na kare. Faculty ka different end date ya counting rule aaye to dono status milestones recalculate karein. Exact time/timezone, recipient, format aur delivery process OD-04/06 ke remaining particulars hain. Hosting optional hai aur checkpoint/final acceptance ka mandatory item nahi. PDF koi specific email address, ZIP name, Excel template ya hosting URL nahi deta.

Report mein date, actual completed Task/REQ IDs, current work, blockers/DOUBT items, next actions aur attached review document ho. Final deliverables list [TEST_PLAN](TEST_PLAN.md) mein single checklist ke taur par maintained hai. Email ke subject prefixes source ke mutabiq hon; no draft ko sent proof na samjhein.

## Future one-phase-per-prompt execution

Sirf jab user implementation explicitly authorize kare, Codex ya Antigravity ko approved phase ID dein: current docs parho -> dependency/decision readiness verify -> us phase ke tasks implement -> applicable G -> changed files aur actual test results report -> agle phase se pehle user instruction. Order: Phase 0 approvals, 1 foundation, 2 public, 3 membership, 4 content, 5 contests, 6 polish, 7 verification, 8 final packaging. T-23/T-24 calendar ke mutabiq parallel reporting obligations hain. Phase 1 ke scoped outputs implement ho chuke hain; next Phase 2 user ke agle instruction par. Payment aur contests ka work abhi start nahi hua.
