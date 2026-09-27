# JamesThew.com - Product Requirements Document

## Haalat aur source authority

26 September 2026: original planning baseline ke baad latest user instruction se scoped Phase 1 foundation implement hui: Identity-only database/migration aur account auth. Payment/content/contest workflows abhi future scope hain. Pehle se mojood MVC starter ko requirement implementation na samjha jaye. Source requirement, Proposed Design aur Open Decision alag labels hain; neeche acceptance criteria requirement ko verify karne ki planning hain, completed tests nahi.

PDF original functional requirements ka primary source hai; latest user-approved academic-demo assumptions effective implementation baseline ko override karti hain, jahan neeche explicitly record hai. Private planning evidence is retained locally; identifying details are excluded from this public repository. Pasted text user ki is phase ki request hai, Aptech ka submission circular nahi. PDF ke instructions deliverable requirements ke taur par record hain; un ki wajah se email sending ya current Phase 1 se bahar implementation automatically authorized nahi hoti.

## Source inventory aur inspection

PDF page numbers file ke 1-based physical pages hain; printed numbering nahi. Tamam 10 pages extract karke parhe aur rendered overview inspect kiya; pricing wali page 4 ko bari rendering mein bhi dekha. Koi substantive unreadable hissa nahi. Rendering mein Symbol font warning aayi, magar text aur bullets extraction/rendering se samajh aaye.

| Source | Type / pages | Inspection aur istemal |
|---|---|---|
| Local original specification (not published) | PDF, 10 pages | Primary specification; requirements summarized below; both original copies retained locally |
| Private planning screenshot (not published) | Local planning evidence | Identifiers and student portal details omitted |
| `Pasted text.txt` | TXT, user request; pagination nahi | Attachment se poora parha; six files, Roman Urdu aur planning-only scope sab docs mein |
| [JamesThew.slnx](../JamesThew.slnx), [JamesThew.csproj](../JamesThew/JamesThew.csproj) | Solution / project | Existing starter, project ka observed target `net10.0`; SYSTEM_DESIGN aur README mein context |
| Existing `Program.cs`, `Controllers/HomeController.cs`, `Views/Home/Index.cshtml` | C# / Razor | Read-only inspection mein default MVC home mila; implemented eProject features ka saboot nahi |
| Submission TXT, Excel forms, original ZIP | Available nahi | Relevant folder inventory mein nahi mile; feedback form ka PDF mein zikr hai magar file nahi mili |

Source PDFs and private planning attachments remain on the owner's laptop. They are not public repository assets; machine paths and attachment IDs are omitted.

Root aur relevant subfolders inventory: root mein `.vs/`, `JamesThew/`, PDF aur solution; app folder mein project files, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`, `Controllers/HomeController.cs`, `Models/ErrorViewModel.cs`, `Views/Home/{Index,Privacy}.cshtml`, shared layout/error/validation views, view imports/start, `wwwroot/css/site.css`, `wwwroot/js/site.js`, favicon aur Bootstrap/jQuery/validation vendor assets. Vendor `LICENSE.txt` submission instructions nahi. `.vs` IDE metadata hai. Koi relevant AGENTS.md nahi mila. Git status ke waqt folder Git repository nahi tha.

### Private scheduling evidence

Student portal identifiers, screenshot details and academic calendar dates are kept locally. Public documentation retains relative reporting milestones and project requirements only.

## Overview, goal aur scope

James Thew apni recipes aur cooking tips share karna, paid membership dena, feedback lena aur recipe/tip contests conduct karna chahte hain. Public ko free content aur contests milenge; paid member ko tamam recipes/tips aur apni contributions ka flow milega; James ko publishing aur winner announcement ka control milega. PDF p4 Introduction mein classes, publishers aur television shows background hain; online class booking, books ki sales ya video streaming ki requirement nahi.

## User-approved academic-demo baseline

Latest user request ki six assumptions is revision ki authority hain. Yeh policy decisions approved hain; latest instruction ne sirf Phase 1 database/Identity/register/login/logout implementation authorize ki hai.

| Area | Effective baseline | Original source se relation |
|---|---|---|
| Payments / REQ-002 | Simulated/manual academic demo; no real gateway. Planning flow: member plan request kare, request Pending rahe, admin manual demo approval se Active kare. Har receipt Demo ho; koi real charge/card collection nahi | PDF p4 payment maangta hai, mechanism specify nahi karta; demo simulation user-approved interpretation hai |
| Guest access / REQ-003, REQ-013, REQ-015, REQ-017 | Guest public published content, contests, results aur FAQ browse/search kare; account registration/login entry points available. Feedback, contributions aur contest submission member login ke baad | PDF p4-5 guest recipe feedback aur contest entry allow karta hai; latest user policy un guest writes ko explicitly supersede karti hai |
| Contributions / REQ-010, REQ-011, REQ-012 | Member submission Pending; admin approval par Published + Free, yani guests samet public-readable. Pending/rejected content sirf owner/admin ko; approved item edit hone par proposed re-review, tab public se hide | PDF p5 customer content reading deta hai, moderation/publication lifecycle specify nahi karta; approved-before-public policy user se |
| Contests / REQ-016 to REQ-019 | Admin contest create/manage kare; logged-in members entries bhejein; admin review, results aur winners manage kare. Public result browse kar sakti hai | Guest participation clause user-approved override hai; creator/reviewer/winner role PDF se aligned |
| Delivery / REQ-027 | Local ASP.NET Core MVC development aur academic submission pehle; hosting optional, release blocker nahi | PDF p10 exact modern toolchain ya deployed site demand nahi karta |
| Dates / REQ-024 to REQ-026 | Internal checkpoint, final submission and two status reports required | Calendar dates retained privately; relative milestones in TASKS |

Member-feature policy mein login lazmi hai; existing planning default ke mutabiq active demo subscription bhi required rahegi. Pending/expired account apna profile, membership request aur previous own submissions read kar sake; member writes aur premium reading activation tak blocked. Login akela active membership ka proof nahi. Exact active-membership expiry rules abhi OD-01 ke technical defaults hain.

## Original PDF requirements aur effective traceability

`CRS` ka matlab PDF ka `Customer Requirement Specification` heading hai. Role `Team` documentation/delivery zimmedari hai. Requirement/source columns original PDF ko preserve karte hain; acceptance column current user-approved baseline follow karta hai. Superseded source clauses ko PDF-compliant implemented feature claim nahi karna. Validation ki exact limits aur remaining lifecycle details neeche Proposed Design hain.

| REQ-ID | Requirement | Role | Source page / section | Testable acceptance criteria |
|---|---|---|---|---|
| REQ-001 | Home par James/owner ka taaruf aur required main menu | Sab | p4 CRS points 1-2 | Home, Login/Register, Free Recipes, Contests, Announcements, Feedback, FAQ reachable hon; home owner details dikhaye |
| REQ-002 | Payment se membership registration, login, monthly $10 / yearly $100 | Guest, Member | p4 CRS 1 Login/Register | Dono amounts demo labels ke saath hon; admin manual approval se active access; pending/rejected ko premium access nahi; real gateway nahi |
| REQ-003 | Free recipes/tips bina registration view aur search | Guest | p4 CRS 1 Free Recipes; p5 CRS 6 | Free item khule aur mile; members-only body guest ko na mile |
| REQ-004 | Tamam cookery recipes aur recipes/tips search/view | Member, Admin | p5 CRS 4-5 | Active member/admin dono visibility levels dekh aur search kar saken |
| REQ-005 | Profile view aur usi page par edit | Member, Admin | p5 CRS 4-5 User Profile | Apna profile change persist ho; doosre ka profile edit na ho |
| REQ-006 | James recipes ingredients aur cooking procedures ke saath upload kare | Admin | p5 CRS 4 Upload recipes | Recipe mein ingredients aur ordered procedure save/view hon |
| REQ-007 | James tips upload kare | Admin | p5 CRS 4 Upload tips | Tip create aur detail view mein content mile |
| REQ-008 | James apni recipes/tips Free ya MembersOnly mark kare | Admin | p5 CRS 4 visibility bullet | Visibility change ke baad guest access accordingly badle |
| REQ-009 | James apni uploaded recipes/tips view/update/delete kare | Admin | p5 CRS 4 CRUD bullet | Dono content types edit hon; deleted item catalog se hate |
| REQ-010 | Customer tips aur ingredients/procedure wali recipes bheje | Member | p5 CRS 5 Send tips/recipes | Dono submission types Pending persist hon; incomplete recipe reject; sirf admin approval se public hon |
| REQ-011 | Customers ki bheji recipes/tips dekhna | Member | p5 CRS 5 View all customers' content | User override: do customers ki approved contributions guest/member ko milen; pending sirf owner/admin ko |
| REQ-012 | Apni bheji recipes/tips view/update/delete | Member | p5 CRS 5 ownership bullet | Owner dono types manage kare; doosra member deny ho |
| REQ-013 | Recipe feedback bhejna, free recipe par guest feedback | Guest, Member | p4 CRS 1; p5 CRS 5-6 | User override: member accessible recipe feedback submit kare; guest form/POST login challenge kare, feedback save na ho |
| REQ-014 | Received recipe feedback sab dekhna | Admin | p5 CRS 4 feedback bullet | Recipe-linked feedback admin list/detail mein mile |
| REQ-015 | Site ke bare mein feedback page | Sab public visitors | p4 CRS 1 Feedback | User override: logged-in member ka site feedback RecipeId ke baghair capture ho; anonymous submission reject |
| REQ-016 | Contest banana aur remove karna | Admin | p4 CRS 3; p5 CRS 4 | Contest type/rules ke saath publish ho; remove ke baad public participation band ho |
| REQ-017 | Contest dekhna aur recipe/tip bhej kar participate karna, guest bhi | Guest, Member | p4 CRS 1,3; p5 CRS 5-6 | User override: member login/active policy ke saath entry; guest browse-only; recipe entry ingredients/procedure rakhe |
| REQ-018 | Har contest ki received posts review karna | Admin | p4 CRS 3; p5 CRS 4 View posts | Admin contest-specific recipe aur tip entries inspect kar sake |
| REQ-019 | Best post ka winner select aur announce, prize ka zikr | Admin, Sab viewers | p4 CRS 3; p5 CRS 4-6 Announcements | Reviewed contest ki valid entry winner bane; public latest winner dekhe; prize text ho sakta hai, payout implementation specified nahi |
| REQ-020 | FAQ ke specified sawalat aur jawab | Sab | p5 CRS 7; p6 continuation | Neeche ke tamam 7 sawal aur agreed flow se mutabiq jawab available hon |
| REQ-021 | Har code block comments, logic explanation, synopsis/code/documentation wala complete report | Team | p7 Standards plan 1-3; p2 Introduction closing | Code review mein comments/logic evidence aur final report mein synopsis/source/docs hon |
| REQ-022 | Rozana data backup | Team | p7 Standards plan Note | Dated backup record aur successful restore evidence ho |
| REQ-023 | Project report ke 13 listed documentation items | Team | p8 Documentation | [TEST_PLAN](TEST_PLAN.md) ki har report item checklist complete ho |
| REQ-024 | Do status mails aur timing rules | Team | p9 Deliverables a | Confirmed timeline ke mutabiq do reports ready/sent evidence ho; is phase mein send nahi |
| REQ-025 | Status description + review document; clarification aur correct email subject prefixes | Team | p9 Deliverables b aur closing | STATUS:, DOUBT:, PROJECT SUBMISSION: relevant draft mein correct hon; status mein actual progress ho |
| REQ-026 | Final soft-copy documentation aur supplied feedback form | Team | p9 Deliverables c; p2 closing | Documentation/source/report aur actual supplied form submit package mein hon; missing form flagged rahe |
| REQ-027 | Listed hardware/software options se faculty-compatible selection | Team | p10 Hardware/ Software Requirements | Local stack/versions faculty-compatible hon aur environment checklist mein record hon; hosting optional |

### FAQ content coverage

1. Member kaise banna hai? Account banayein, demo plan request karein aur admin ke manual demo approval ke baad member features use karein.
2. Subscription charges hain? Source amounts monthly $10 aur yearly $100; taxes/currency qualification OD-01.
3. Recipes/tips kaise dekhein, kya charges hain? Free content public; members-only ke liye active paid membership.
4. Unregistered visitor contest join kar sakta hai? Current demo policy mein nahi; contest browse kar sakta hai, entry ke liye member login required hai. Yeh original PDF guest-entry answer ka recorded override hai.
5. Recipes/tips upload/post kaise karna hai? Member contribution form se submit kare; Pending contribution admin approval ke baad public hoti hai. Contest entry alag member-only form hai.
6. Feedback kaise post karna hai? Member login ke baad accessible recipe detail ya site Feedback form; guest ko login prompt milega.
7. Contest post ke baad winner kaise pata chalega? Public Announcements aur contest result page.

## Permissions aur journeys

| Capability | Guest / inactive account | Active Member | James/Admin |
|---|---|---|---|
| Home, FAQ, announcements, contests | Haan | Haan | Haan |
| Recipe/tip view/search | Sirf published free | Tamam eligible published content | Sab content; review bhi |
| Recipe/site feedback | Submit nahi; login prompt | Accessible recipe/site par submit | Recipe feedback read; moderation proposed |
| Profile | Guest nahi; inactive apna account proposed | Apna view/edit | Apna view/edit |
| General recipe/tip contribution | Nahi | Apna create/view/edit/delete | Apni editorial content CRUD |
| Customer contributions | Admin-approved Published + Free read | Approved public read; own Pending/Rejected read | Pending review, approve/reject |
| Contest entry | Nahi; sirf contest/result browse | Haan, logged-in active member | Entries review, results aur winner manage |
| Paid visibility setting / contest management | Nahi | Nahi | James ki content / contests |

Guest journey: Home -> public search -> free recipe/approved contribution; ya Contests -> rules -> Announcements. Protected action -> Login/Register. Membership journey: Register -> plan -> Pending demo request -> admin manual approval -> active login -> profile -> contribution Pending -> admin approval -> public listing. Admin journey: Login -> own profile -> content create/visibility -> feedback review -> contest create -> entries review -> winner announcement. Payment pending/failed account ko active membership na samjhein.

## Proposed Design: business rules aur validation

Yeh limits, security controls aur states PDF ke verbatim rules nahi; implementation baseline ke proposals hain. Architecture details [SYSTEM_DESIGN](SYSTEM_DESIGN.md), page states [DESIGN](DESIGN.md), verification [TEST_PLAN](TEST_PLAN.md) mein hain.

| Module | Proposed rules / validation | Module exit condition |
|---|---|---|
| Identity/profile | Trimmed name 2-100, valid normalized unique email, Identity password policy; role/expiry client se editable nahi | Duplicate email handled; owner-only profile; failed login generic message |
| Subscription | Plan amount server-side; Pending -> Active sirf authorized admin manual demo approval; Failed/Expired paid access band; UTC start/end, end-exclusive | Active iff start <= now < end; duplicate activation se duration do baar na barhe |
| Recipes | Title 3-150, summary <=500, kam az kam 1 ingredient aur 1 nonblank ordered step; quantity/unit optional descriptive values | Dono author roles ki valid recipe readable; invalid nested rows reject |
| Tips | Title 3-150, body 10-10000; plain text initially | Valid tip persist aur safe render ho |
| Content/search | Publication aur access alag fields; query <=100 chars; 12 items/page, deterministic sort; title/summary/body matching authorized scope mein | Paid text HTML, JSON, search snippets, image endpoints ya cache mein leak na ho |
| Contributions | Member owner server se; Pending -> Published/Rejected; approval par Visibility=Free; edit se re-review proposed; deletion soft hide | Pending private owner/admin; approved content public; member khud publish/visibility change na kare |
| Feedback | RecipeId recipe feedback ke liye required, site ke liye null; message 5-2000; authenticated member author; pending moderation proposed | Receipt aaye; contact public na ho; spam throttled; inaccessible recipe feedback reject |
| Contests | Type Recipe/Tip, title/rules required; proposed opening/closing UTC window; admin removal archive; entry allowed sirf open window mein | Closed/removed contest direct POST bhi reject; type mismatch reject |
| Entries/winner | Authenticated member author required; submission ka immutable snapshot; review status; one winner/contest proposed | Winner isi contest ki eligible reviewed entry ho; announcement atomically publish; no-entry contest winner na banaye |
| Announcements/FAQ | Winner display name public, private email nahi; FAQ answers final policy se aligned | Public winner read aur 7 FAQ answers available |
| Backup/report | Rozana source aur database backup; restore test; evidence sachchi aur dated | Fresh-machine restore aur report checklist independently verify ho |

## Proposed enhancements, assumptions aur exclusions

Admin approval aur public contribution publication user-approved policy hai. Design tokens, responsive layouts, accessibility, audit events, soft delete, image uploads, demo payment service structure aur selected animation implementation proposals hain. Recipe categories PDF p4 background mein examples hain; category filters useful optional enhancement hain. Ratings, bookmarks, nutrition, videos, email notifications, social login aur analytics PDF requirements nahi aur initial scope se bahar hain.

James/Admin ek owner identity ka assumption hai. Member access ke liye active demo subscription planning default hai. Guest submissions current baseline se excluded hain. Real gateway/payments, automated renewals, refunds, tax engine, actual prize fulfillment aur class booking scope se bahar hain. Hosting optional hai; local academic package mandatory delivery target hai. Yeh six Markdown files final Aptech report/forms ka replacement nahi.

## Numbered Open Decisions aur resolution register

Closed ka matlab latest user assumptions se planning policy resolved hai; faculty ki source-variance acceptance ka fabricated claim nahi. Original source aur current choice dono retained hain.

| ID / status | Source mein kya hai | Current decision / remaining question | Authority / next action |
|---|---|---|---|
| OD-01 - Partly open | p4 monthly $10, yearly $100 clear | Amounts fixed demo prices; real charges/tax engine nahi. Proposed USD display code aur calendar-month/year calculation, month-end/leap-year behavior abhi settle karna hai | User/faculty: sirf currency/period detail pending, price association resolved |
| OD-02 - Closed | p4 payment required, mechanism unspecified | Simulated/manual demo only; planning default Pending request -> admin approval -> Active, rejection leaves access blocked; no real gateway or automatic charging | Latest user assumption; workflow detail documented, future implementation separately authorized |
| OD-03 - Closed / superseded | p5 guest feedback/entry allowed | Guests browse-only; feedback/contributions/entries login-protected. Guest identity/contact collection aur guest moderation flow baseline se removed | Latest user policy overrides PDF; variance register above |
| OD-04 - Private schedule | Academic scheduling evidence retained locally | Checkpoint, final deadline and reporting dates maintained privately; confirm timezone/counting rules with faculty | User/faculty; relative reporting rules in TASKS |
| OD-05 - Partly resolved | p10 software options; existing csproj net10.0 | Local MVC/Razor/C#/EF Core/SQL Server/Identity plan; hosting optional. Local implementation observed/verified: existing net10.0, SDK 10.0.401, EF/Identity 10.0.12, SQL Server 2025 LocalDB 17.0.4025.3. Faculty-specific version/IDE approval remains open | Faculty/local environment check; optional hosting selection is not a foundation blocker |
| OD-06 - Open | p9 feedback form attached; p8 report list | Original feedback form/certificate/template, ZIP format/size and recipient missing | Faculty/eProjects team; obtain actual documents, no fabricated signatures/forms |
| OD-07 - Closed | p5 customer content read; moderation unspecified | New contributions Pending; admin approval makes Published + Free/public. Owner/admin alone see Pending/Rejected. Proposed edited content returns Pending and hides until reapproval | Latest user assumption resolves approval/public visibility; edit lifecycle remains documented implementation default |
| OD-08 - Partly resolved | p4 contest best entry/prize; p5 contest management | Admin creates/manages contests/results/winners; members submit. Ties, max entries, prize text, deadline edits and removal history still detail decisions. Proposed one winner and one entry/member/contest; immutable snapshots, archive removal | Roles resolved by user; remaining contest details user/faculty review |
| OD-09 - Open | p7 every code block comments | Meaningful logical-block comments proposed; exact faculty rubric/report formatting pending | Faculty |
| OD-10 - Partly resolved | p5 admin own uploads CRUD | Admin approves/rejects member contributions. Author-text editing/deletion powers beyond moderation not specified; proposed no silent author-text edits, audit decisions. Guest correction process removed | Moderation user-approved; broader admin powers user/faculty review |

Faculty changes agar milen to source/date ke saath baseline revise karein. Original PDF ko rewrite nahi kiya gaya. Approved demo assumptions par dobara permission maangna zaroori nahi; unresolved version/form/rubric details ko completed decision na label karein.
