# JamesThew.com - Visual aur Interaction Design

## Proposed Design ki haisiyat

Yeh design proposal hai, approved mockup ya implemented UI nahi. Functional authority [PRD](PRD.md) ke REQ-001 se REQ-020 aur latest user-approved overrides hain; architecture [SYSTEM_DESIGN](SYSTEM_DESIGN.md) mein hai. Food editorial direction user ki planning request hai, PDF ki prescribed theme nahi.

Current policy: guest read-only public browsing; feedback/entry/contribution forms member login aur active demo membership ke baad. Member contributions Pending rahengi aur admin approval ke baad public. Manual demo approval UI use hogi; real gateway screen nahi.

## Visual direction

Warm ivory background, ink headings, tomato-red actions, olive highlights aur bari natural food photography se ek polished culinary magazine ka ehsaas banayein. Home par James ki kahani aur food content ki hierarchy saaf ho. Recipe padhte waqt decoration kam aur ingredients/procedure zyada prominent hon. Admin mein isi identity ka restrained, information-dense version ho.

| Token | Proposed value / rule |
|---|---|
| Canvas / surface | `#FFF9F0` / `#FFFFFF` |
| Main text / secondary text | `#24211D` / `#5C554B` |
| Primary action | `#9C3526`, white label; hover `#78281D` |
| Accent | `#46583B`, warm decorative gold `#C89847`; gold small text ke liye nahi |
| Borders / errors | `#DDD2C2` / `#A12622`; error icon aur text bhi |
| Headings | Georgia fallback; optional licensed editorial serif baad mein select karein |
| Body | System UI sans-serif; optional locally hosted licensed font |
| Type scale | Body 16-18px, line-height 1.6; headings 24/32/44/64 responsive; readable line 60-75 characters |
| Spacing | 4, 8, 12, 16, 24, 32, 48, 64, 96px |
| Width / corners | Content max 1200px; reading max 760px; cards 12px, buttons 8px |
| Shadows | Sirf light card elevation; form boundaries shadow par depend nahi |

Implementation par actual color pairs ko contrast checker se verify karein: normal text >=4.5:1, large text aur meaningful non-text boundaries >=3:1 proposed accessibility target. Color tokens ko already certified accessible na samjhein.

## Responsive layout aur navigation

Proposed breakpoints: mobile <640px, tablet 640-1023px, desktop >=1024px. 320px se layout usable ho; 200% zoom par essential controls na chhupein. Mobile 16px gutters/1 column, tablet 24px/2 cards, desktop 32px/3 cards. Long forms max 760px. Admin tables mobile par labeled cards ya accessible horizontal region ban sakein; essential actions missing na hon.

Public header mein logo/Home, Recipes & Tips, Contests, Announcements, Feedback, FAQ, Login/Register. Logged-in header mein Profile, My Contributions aur membership status. Admin ke liye dashboard/content/feedback/contests/announcements navigation. Mobile drawer button accessible expanded state rakhe; Escape close, focus return, focus trap sirf modal drawer mein. Footer mein FAQ, site feedback aur proposed privacy/contact information; invented address/phone nahi.

## Reusable components

RecipeCard (image, title, author, Free/Member badge), TipCard, visibility badge, membership badge, search form, pagination, ingredient list, ordered steps, form field/error summary, notice, skeleton, empty panel, confirmation dialog, upload preview, content editor, moderation status, contest deadline label, entry receipt, winner panel aur FAQ disclosure reuse hon. Badges text ke saath hon. All interactive elements native link/button semantics use karein. Recipe cards ka nested buttons/links conflict avoid ho.

## Page-by-page layout aur states

Neeche `L` loading, `E` empty, `V` validation, `F` failure aur `S` success ka matlab hai. Har row ka state applicable interaction ke liye hai. Static Razor GET par artificial spinner nahi; L sirf real async/navigation work par. Non-form page mein V route/query validation ho sakti hai. Shared failure page request reference dikhaye, stack trace nahi.

| Page / linked REQ | Layout | L / E / V / F / S |
|---|---|---|
| Home, REQ-001 | Header, James intro hero, featured free recipes, tips, contest feature, latest winner, footer | L image reserved space; E featured content ke baghair intro; V invalid links safe routing; F section fallback; S readable owner story |
| Recipes & Tips catalog, REQ-003, REQ-004 | Search, Recipe/Tip tabs, optional category filter, count, grid, pagination | L scoped skeleton; E reset-search action; V query length; F retry retaining query; S authorized results/count |
| Recipe detail, REQ-003, REQ-004, REQ-013 | Title/author/badge, image, ingredients side panel desktop, ordered steps, member recipe feedback form, guest ko login prompt | L image dimensions fixed; E feedback ki first-entry invitation; V missing rows/message; F unavailable item or submit retry; S feedback receipt |
| Tip detail, REQ-003, REQ-004 | Title, author, badge, readable body, related tips | L image optional; E no related tips; V invalid slug; F 404/access state; S readable full tip |
| Membership access state, REQ-002, REQ-008 | Minimal membership explanation, plan link, sign-in; paid body absent | L session check if needed; E no active plan; V returnUrl local-only; F access lookup fallback; S authorized redirect |
| Login/Register, REQ-002 | Tabbed/linked forms, plan selection summary and pending-access notice | L submit disabled temporarily; E initial form; V email/password/duplicate; F generic auth error; S login or pending registration |
| Membership/plans/result, REQ-002 | Monthly $10/yearly $100 comparison, selected plan, Manual Demo label, Pending request aur admin approval result | L processing; E no subscription; V plan tamper; F failure/cancel with retry; S confirmed Active or clearly Pending |
| Profile, REQ-005 | Read summary and edit fields same page; membership summary separate from editable data | L save state; E optional biography hint; V inline/summary; F concurrency/retry; S saved notice |
| Community contributions, REQ-011 | Customer recipes/tips tabs, author labels, grid; admin-approved public content; Pending items public grid se absent | L grid skeleton; E first contribution prompt; V filter limits; F retry; S eligible cross-customer content |
| My Contributions, REQ-010, REQ-012 | Own content list with status, create buttons, edit/delete actions | L list; E create first item; V invalid action/id; F stale/deleted item; S update/delete confirmation |
| Recipe/Tip editor, REQ-006, REQ-007, REQ-010, REQ-012 | Type-specific fields, ingredient and step rows, optional image, preview and save; admin visibility only | L save; E blank form with one row; V field/row errors; F preserved draft + retry; S saved/review status |
| Contests list/detail, REQ-016, REQ-017 | Cards with type/status; detail rules, deadline timezone, member entry action/guest login prompt, published result | L cards; E no active contests; V invalid route; F unavailable contest; S open/closed/winner state |
| Contest entry, REQ-017 | Rules reminder, signed-in member identity, recipe/tip form, receipt; no guest contact form | L submit; E blank fields; V identity/type/ingredients/window; F login/access/duplicate/closed-safe error; S reference receipt, no fabricated email-sent claim |
| Announcements, REQ-019 | Latest winner feature, contest-linked archive, winner display name and safe excerpt | L card spaces; E no announced winner; V invalid pagination; F retry; S result readable without login |
| Site Feedback, REQ-015 | Member message form, account-linked author, privacy note; guest ko login prompt | L submit; E first-use blank form; V message; F login/access/retry; S receipt |
| FAQ, REQ-020 | Seven specified Q&A, anchor links, optional disclosure groups | L none for static HTML; E configuration fault message; V invalid anchor harmless; F fallback content; S answers usable without JS |
| Admin dashboard, REQ-006 to REQ-019 | Pending-review counts, content/feedback/contest shortcuts, recent activity | L scoped cards; E zeros with create action; V invalid filter; F individual panel retry; S accurate counts |
| Admin content list/editor, REQ-006 to REQ-009 | Search/type/status filters, own content CRUD, Free/Member control, recipe rows | L table; E create action; V body/rows/visibility; F concurrency conflict; S published/updated/removed notice |
| Admin contribution moderation, REQ-010 to REQ-012 | Queue, full read preview, approve/reject with reason; author's text read-only by default | L queue; E no pending items; V reason/status; F stale review; S decision + audit record |
| Admin feedback, REQ-013 to REQ-015 | Recipe/site filters, message detail, private contact, proposed moderation action | L table; E none received; V invalid filter/action; F retry; S all received recipe feedback accessible |
| Admin contests/entries, REQ-016 to REQ-018 | Contest form/list, archive confirmation, entry review panel with recipe/tip snapshot | L list/detail; E no entries; V dates/type; F removed/stale contest; S review state saved |
| Admin winner/announcement, REQ-019 | Reviewed eligible entries, winner selection, announcement preview, publish | L publish; E no eligible entry; V wrong contest/winner; F concurrent selection; S public result link |
| Admin membership review, REQ-002 | Pending Manual Demo requests, approve/reject controls aur subscription ledger | L list; E none pending; V invalid transition; F conflict; S audited demo activation; guest/member cannot approve |
| Shared 403/404/error, sab web REQs | Plain explanation, Home/back action, safe request reference | L none; E unavailable item; V safe URL; F no technical secrets; S navigation recovery |

## Interaction, keyboard aur touch

Skip link, landmark regions, single main H1, descriptive page titles aur visible 2-3px focus ring dein. Tab order visual order follow kare; positive tabindex nahi. Touch targets preferably >=44x44px aur adequate gaps hon. Hover-only menus/actions na hon. Form labels permanent hon; errors `aria-describedby` se linked; submit failure par error summary focus ho. Success messages non-disruptive live region mein. Ingredient/step reorder ke liye keyboard Up/Down buttons bhi hon, sirf drag nahi.

Delete/archive confirmation item ka naam bataye aur cancel ko safe initial focus de. Permission errors underlying paid text reveal na karein. Membership badge ya admin button hide karna security implementation ka badal nahi. Mobile recipe ingredients steps se pehle aayein; sticky action content/focus ko cover na kare. Long procedure lines aur units wrap hon. Contest closure client clock se akela decide na ho.

## Assets, licensing aur optimization

James ka real portrait/biography user approval ke baad; stock chef ko James keh kar present nahi karna. Food images ke liye user-owned ya appropriately licensed assets, provenance/license/attribution register baad mein maintain karein. Initial placeholders neutral, labeled aur fixed aspect ratios hon; broken remote image URLs nahi. Image generation ya asset purchase is phase mein nahi hui.

Hero 16:10 ya 3:2, cards 4:3; meaningful food alt text, decorative image empty alt. Responsive `srcset`/sizes, AVIF/WebP with fallback, intrinsic width/height aur below-fold lazy loading proposed. Hero ko lazy-load na karein. Suggested budget hero <=250KB, cards <=120KB, initial image total <=1MB; real devices par measure karke adjust karein. Uploaded user images re-encode hon; exact limits [SYSTEM_DESIGN](SYSTEM_DESIGN.md) mein.

## Motion plan

| Interaction / scene | Proposed treatment | Fallback aur limits |
|---|---|---|
| Buttons/cards | CSS color/shadow 120-180ms; card optional translateY(-2px) | Touch par hover dependency nahi; focus equally clear |
| Form notices/disclosures | 150-200ms opacity; layout content ke mutabiq | Validation delay nahi; height animation optional |
| Home hero entrance | 300-450ms gentle opacity/translate, once | Content initially accessible; JS fail par visible |
| Home editorial chapter | Optional GSAP/ScrollTrigger text/image reveal, 16-24px shift, once | Native scroll; no pinning or scroll hijack; narrow screens par plain content |
| Contest feature | Optional GSAP stagger of max 3 cards | Detail pages/forms/admin par scroll scene nahi |
| Entry success | Optional short Lottie accent alongside textual receipt | Asset locally licensed, <=80KB target, no loop; SVG/static fallback |

`prefers-reduced-motion: reduce` par transforms, parallax, stagger aur Lottie playback disable; content instantly visible. Motion ko query se live re-evaluate karein aur listeners clean up karein. GSAP/ScrollTrigger versions/license implementation waqt verify hon; approval ya installed hone ka claim nahi. Transform/opacity prefer, layout thrashing avoid, animations below viewport pause. Keyboard users ke liye forced scroll nahi. Proposed performance targets LCP <=2.5s, CLS <=0.1, INP <=200ms representative deployment/device par; measurements abhi available nahi.

## Google Stitch se future Razor workflow

1. PRD decisions resolve karke Home, catalog, recipe, membership, entry, profile aur admin content ke wireframes review karein.
2. Agar baad mein Google Stitch use ho to screen export/reference ID, date, variant aur approval state record karein. Abhi koi Stitch design existing ya approved nahi.
3. Approved visual ko semantic Razor layout/partials aur view models mein map karein; exported UI ko authentication/payment logic ka source na banayein.
4. Tokens aur components reuse karke mobile/desktop + all states implement karein; assets ki licenses check karein.
5. Source design versus browser screenshots compare, keyboard/reduced-motion verify, deviations log karein; [TASKS](TASKS.md) phase gates follow hon.
