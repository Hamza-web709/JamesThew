# JamesThew — Motion Choreography & Editorial Architecture Blueprint
**Comprehensive Scroll-Driven Redesign Blueprint Based on Reference Video Motion Analysis**
*Document Version: 1.0.0 | Date: September 30, 2026 | Author: Antigravity UI/UX & Motion Intelligence*

---

## Executive Summary

This blueprint defines the end-to-end redesign of the **JamesThew.com** landing page, translating the scroll choreography, editorial composition, 3D object transformation, horizontal gallery mechanics, and section-to-section fluid transitions demonstrated in the attached reference video (`Reference Video.mp4`, `@CodewithEluee` luxury scroll case study) into an original, world-class culinary and recipe platform.

In strict compliance with instructions:
- **No code will be implemented** until this motion blueprint is reviewed and approved.
- The reference video is used **strictly as an interaction, motion, composition, and storytelling benchmark** — no wine brand, assets, or text are copied.
- The redesign builds on the existing **ASP.NET Core MVC** architecture, Razor views, vanilla CSS, and **GSAP 3.12.5 + ScrollTrigger**, requiring zero React, Next.js, or heavyweight frontend frameworks.
- All recipe, tip, contest, FAQ, and membership features remain 100% backed by `HomeViewModel` and live database models.

---

## 1. Reference Video Motion & Interaction Analysis

Analysis of `Reference Video.mp4` (Duration: 14.52s, 853 frames @ 58.72fps, Viewport: 576×1280 mobile-view screen capture):

| Timestamp / Frame Range | Section in Reference | Visual Composition & Choreography | Motion & Technical Mechanism | Key Takeaway for JamesThew |
| :--- | :--- | :--- | :--- | :--- |
| **0.00s – 2.50s**<br>(Frames 001 – 005) | **Hero Section** | • Central organic object (cluster of white grapes on vine) hanging from top.<br>• Asymmetric editorial text on left & right.<br>• Floating capsule pill navbar at top center.<br>• Sunlit vineyard background with soft depth of field. | • On scroll initiation, grapes ascend upward with subtle rotation.<br>• Text elements slide horizontally outwards and cross-fade out.<br>• Background smoothly transitions from sunny green vineyard to sky horizon. | **Distributed Asymmetric Hero:** Object is not trapped in a box; raw hero object enters from the top/center and travels dynamically; text is distributed across left and right quadrants. |
| **2.50s – 5.00s**<br>(Frames 006 – 009) | **Object-to-Object Transformation** | • Floating grapes transform into an explosive 3D splash of liquid wine in mid-air.<br>• Floating copy: *"And every glass tells our story"* split across left & right.<br>• Splash gathers and pours into an elegant luxury wine glass.<br>• Glass descends onto a linen-draped table overlooking golden-hour vineyard. | • Fluid morph/cross-scale animation.<br>• Object scales up, bursts outward, then condenses into the finished glass.<br>• High-contrast lighting highlights liquid droplets.<br>• Seamless background shift to table setting. | **Culinary Transformation:** Raw ingredient (vine/earth) → Dynamic heat & pan kinetic burst → Gourmet finished plated presentation on luxury stoneware. |
| **5.00s – 7.50s**<br>(Frames 010 – 015) | **The Story (Light Editorial Canvas)** | • Canvas cross-fades from dark sunset to pristine ivory/cream (`#f7f5f0`).<br>• Large serif display typography: *"The story of..."*.<br>• Horizontal row of 5 vertical photo cards (estate, barrels, vineyard).<br>• 4 editorial milestone columns with stats & narrative text. | • Pinned section with staggered horizontal and vertical card drift.<br>• Parallax velocity differences between cards.<br>• Smooth transition from full-bleed photography to clean editorial canvas. | **Light Magazine Story:** Clean break from dark kitchen into warm French linen cream canvas. Multi-card photo deck illustrating Chef James Thew's craft, discipline, and kitchen sanctuary. |
| **7.50s – 9.20s**<br>(Frames 016 – 019) | **Recipe / Product Editorial Collage** | • Irregular magazine collage with 6 images of mixed aspect ratios (tall portraits, wide landscapes, squares).<br>• Floating around a center focal point. | • Cards drift with differential parallax.<br>• As scroll advances, one focal card (chilled bottle in dark cellar) scales up dramatically, pushing outer cards toward viewport edges. | **Dynamic Collage to Focal Zoom:** 6 real JamesThew recipe cards in an irregular mosaic. On scroll, the focal masterclass card expands and commands the center. |
| **9.20s – 10.50s**<br>(Frames 020 – 022) | **Zoom-into-Dark Transition** | • Expanding focal image grows to 100% viewport width and height.<br>• Background seamlessly shifts from light cream into deep obsidian charcoal.<br>• A single signature bottle in an ice bucket dominates with dramatic rim lighting. | • Seamless color transition (Light → Dark) driven by image scale.<br>• Deep z-index layering with typography appearing partially behind the bottle. | **Dark Signature Feature:** Transition into obsidian black. Giant serif typography behind the Beef Wellington: `SIGNATURE` [Plated Dish] `MASTERCLASS`. |
| **10.50s – 12.00s**<br>(Frames 022 – 024) | **Horizontal Signature Collection** | • Pinned viewport while vertical scroll drives horizontal track movement.<br>• 3 signature cards glide horizontally with glowing halos, product photos, and label chips.<br>• Next section heading enters from below with an echo/offset kinetic reveal. | • CSS `position: sticky` / GSAP `pin: true`.<br>• `xPercent: -X%` horizontal translation mapped to vertical scroll distance.<br>• Typography offset kinetic shadow reveal. | **Horizontal Signature Collection:** Vertical scroll drives horizontal showcase of James Thew's 4 core recipes (Beef Wellington, Risotto, Roast Chicken, Sourdough Boule) with glowing halos. |
| **12.00s – 13.20s**<br>(Frames 025 – 026) | **Tastings / Experiences (Card Deck)** | • 3 structured degustazione cards (Silver, Gold, Platinum packages with charcuterie boards and pricing tiers).<br>• Dark textured background. | • Cards elevate and reveal on scroll.<br>• Structured metadata with hover expansion. | **Culinary Masterclass & Contests:** 3 interactive competition/experience cards (Stew Showdown, Knife Skills Challenge, Pastry Masterclass). |
| **13.20s – 14.52s**<br>(Frames 027 – 029) | **Editorial CTA & Luxury Footer** | • Canvas returns to bright ivory.<br>• Huge serif headline: *"A signature stay..."* with overlapping room card.<br>• Deep dark brown/charcoal footer with logo emblem, navigation columns, and legal text. | • Smooth background cross-fade from dark to light.<br>• Large typographic hierarchy.<br>• Grounded footer with subtle ambient lighting. | **Final Editorial CTA & Footer:** Massive serif callout: *"Your Next Dish Starts Here"*, overlapping artisan sourdough imagery, member registration CTAs, and luxury dark footer. |

---

## 2. JamesThew Visual Direction

Following `.agents/skills/ui-ux-pro-max/SKILL.md` design intelligence:

### 2.1 Aesthetic Archetype: High-End Gastronomic Editorial
- **Mood:** Luxury culinary editorial meets fine-dining masterclass and cinematic commercial.
- **Atmosphere:** Warm ambient kitchen glow, solid walnut cutting surfaces, dark matte stoneware ceramics, polished copper pans, and refined linen table settings.
- **Rhythm:** Alternating visual acts (Dark Culinary Sanctuary → Light Magazine Editorial → Dark Immersion → Bright Final CTA → Grounded Obsidian Footer).

### 2.2 Color Token Architecture
```css
:root {
  /* Dark Sanctuary Canvas */
  --jt-obsidian: #090b0e;
  --jt-charcoal-deep: #11151c;
  --jt-charcoal-surface: #181d26;
  --jt-charcoal-glass: rgba(18, 22, 28, 0.76);

  /* Light Editorial Canvas */
  --jt-cream-canvas: #fbf9f5;
  --jt-linen-subtle: #f4efe6;
  --jt-parchment-border: #e8dfd1;

  /* Gastronomic Accents */
  --jt-gold-primary: #f3c669;
  --jt-gold-burnished: #c89847;
  --jt-gold-glow: rgba(243, 198, 105, 0.28);
  --jt-sear-ember: #c2412e;
  --jt-sear-copper: #d97736;

  /* Typography Colors */
  --jt-text-light-primary: #fdfbf7;
  --jt-text-light-secondary: #c9bfb2;
  --jt-text-dark-primary: #181b20;
  --jt-text-dark-secondary: #5f584e;
}
```

### 2.3 Typography Scale & Font Pairings
- **Display Serif:** `'Playfair Display', Georgia, serif`
  - High emotional resonance, italicized accents, classical European culinary heritage.
  - Used for hero statements, chapter eyebrows, and large editorial titles.
- **Body & Controls:** `'Plus Jakarta Sans', system-ui, sans-serif`
  - Crisp, geometric, humanist sans-serif with high legibility at 11px–15px for recipe chips, cooking times, author badges, and button labels.

---

## 3. Global Motion Language

### 3.1 GSAP & ScrollTrigger Core Rules
1. **Section-Level Master Timelines:** Rather than dozens of independent listeners, each major section owns a single master `gsap.timeline({ scrollTrigger: { ... } })`.
2. **Transform & Opacity Only:** Animate `x`, `y`, `scale`, `rotation`, and `opacity`. Never animate `width`, `height`, `top`, or `left` directly to maintain 120fps hardware acceleration.
3. **Continuous Reversibility:** Every timeline has `scrub: 0.8` or `scrub: 1.0`. Scrolling backward reverses every transition, card movement, and object assembly frame-for-frame.
4. **Adaptive Pinning:** Use `pin: true` only on stages where the user is meant to absorb an evolving scene (Hero, Transformation, Horizontal Collection, Dark Signature).
5. **No Scroll Jitter:** Wrap horizontal and overflow tracks in `overflow: clip` on the body and parent wrappers to eliminate accidental horizontal scrollbars.

---

## 4. Section-by-Section Motion Blueprint (10 Sections)

```
01 CINEMATIC HERO (Dark Sanctuary - Video Scrub + Asymmetric Editorial)
      ↓ (Grapes/Raw Ingredient Movement & Ascend)
02 INGREDIENT-TO-DISH TRANSFORMATION (Fluid Olive Oil Sear & Plated Arrival)
      ↓ (Cross-fade to Light Linen Canvas)
03 THE STORY OF FOOD (Light Editorial - Staggered Multi-Card Photo Row + Milestone Stats)
      ↓ (Parallax Card Recomposition)
04 RECIPE EDITORIAL COLLAGE (Dynamic 6-Card Mosaic with Focal Card Scale-Up)
      ↓ (Zoom-Into-Darkness Transition)
05 DARK SIGNATURE DISH FEATURE (Obsidian Immersion - Beef Wellington with Typography Behind Food)
      ↓ (Vertical Scroll Drives Horizontal Track)
06 HORIZONTAL SIGNATURE RECIPE COLLECTION (Pinned 4-Card Horizontal Track with Halo Backlights)
      ↓ (Kinetic Offset Heading Reveal)
07 CULINARY TECHNIQUE / MASTERCLASS (3-Card 3D Fan & Focus Switch)
      ↓ (Deck Fan-Out Mechanics)
08 CONTESTS & COMMUNITY EXPERIENCES (Interactive Competition Cards with Prize Badges)
      ↓ (Smooth Canvas Return to Bright Ivory)
09 FINAL EDITORIAL CTA (Massive Serif Callout with Overlapping Artisan Boule)
      ↓ (Subtle Depth Settle)
10 PREMIUM FOOTER (Obsidian Base with Giant Low-Opacity Watermark & Hierarchical Links)
```

---

### Section 01 — Cinematic Hero (01 CINEMATIC HERO)

```
+-----------------------------------------------------------------------------------+
|  [Cap-Nav: James Thew · Recipes · Tips · Contests · Masterclasses · [Join]]       |
|                                                                                   |
|  [Since 1970 · Master Kitchen]                       [Live in the Sanctuary]      |
|  Where Ingredients                                   A culinary destination where |
|  Become Craft                                        technique meets passion.     |
|                                                                                   |
|                               ( HANGING INGREDIENTS )                             |
|                               ( Vine Cherry Tomatoes)                             |
|                               ( Fresh Basil Sprig   )                             |
|                                                                                   |
|                                                                                   |
|  [Scroll to Explore v]               [Browse Masterclasses]                       |
+-----------------------------------------------------------------------------------+
```

- **Visual Composition:**
  - Distributed asymmetric typography matching the reference video: Top-left has chapter label and heading fragment; top-right has editorial statement; bottom-left has scroll cue; bottom-right has direct action.
  - Centered high-contrast foreground objects: Vine-ripened cherry tomatoes and whole sweet basil sprigs hanging into the frame with natural shadows.
  - Background: Scrub-synchronized cooking video (`hero-cooking-scroll.mp4` / `.webm`) showing Chef James Thew in his kitchen sanctuary.
- **Scroll Progression:**
  - **Start (0%):** Hanging raw tomatoes and basil occupy the upper center. Text is crisp and legible on both sides.
  - **Scroll (0% → 100% of hero range, 200vh):** Hanging ingredients ascend vertically into the upper atmosphere (`y: 0 → -120px`, `scale: 1 → 0.85`). Left and right typography slide outward horizontally (`x: 0 → -60px` left, `x: 0 → +60px` right) and fade to 0. Background video scrubs to frame 8 (knife preparation).
- **GSAP Strategy:** Pinned timeline with `scrub: 0.8`, `pin: true`.
- **Assets Needed:** `hero-cooking-scroll.mp4`, `hero-cherry-tomato.webp`, `hero-basil-leaf.webp`.
- **Mobile Behavior:** Single centered headline, single centered hanging tomato cluster, simplified vertical exit.

---

### Section 02 — Ingredient-to-Dish Transformation (02 TRANSFORMATION)

```
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  "And Every Pan Tells..."                                "...A Story of Mastery"  |
|                                                                                   |
|                               ( KINETIC BURST )                                   |
|                             ( Sizzling Olive Oil Sear )                           |
|                             ( Aromatics in High-Heat )                            |
|                                        ↓                                          |
|                          [ HANDCRAFTED CERAMIC PLATE ]                            |
|                       [ PLATED BEEF WELLINGTON MEDALLION ]                        |
|                                                                                   |
|                           [ 60 Min · Masterclass Seal ]                           |
+-----------------------------------------------------------------------------------+
```

- **Direct Reference Parallel:** Directly mirrors the reference video's transformation of hanging grapes → 3D liquid splash in mid-air → wine glass descending onto a dressed table.
- **Visual Composition:**
  - Raw garden aromatics converge toward center screen.
  - A kinetic burst of shimmering extra-virgin olive oil and rising vapor plumes expands in 3D mid-air.
  - As scroll continues, the fluid vapor condenses downward, revealing the dark charcoal stoneware ceramic plate.
  - Master Chef James Thew's signature Beef Wellington medallion slides into position on the plate with golden lattice crust, duxelles, and rich pan demi-glace.
  - Floating typography on both flanks:
    - Left: *"And every cut tells..."*
    - Right: *"...a story of mastery."*
- **Scroll Progression (180vh scroll range):**
  - **Phase 1 (0–35%):** Aromatics converge (`scale: 1 → 1.2`, `opacity: 1`).
  - **Phase 2 (35–65%):** Oil kinetic burst expands with radial illumination; video scrubs through high-heat sizzle (frames 11–14).
  - **Phase 3 (65–100%):** Plate slides up into place (`y: 60 → 0`, `opacity: 0 → 1`), Beef Wellington seats on plate (`x: 80 → 0`, `rotation: 4° → 0°`), recipe seal appears.
- **GSAP Strategy:** Seamless continuation timeline linked to hero exit.
- **Assets Needed:** `hero-plate-ceramic.webp`, `hero-dish-beef-wellington.webp`, `hero-garlic-clove-cutout.webp`, `hero-chili-cutout.webp`, `hero-lemon-slice-cutout.webp`.

---

### Section 03 — The Story of Food (03 THE STORY)

```
+-----------------------------------------------------------------------------------+
|  [LIGHT LINEN CANVAS: #fbf9f5]                                                    |
|                                                                                   |
|  THE CULINARY SANCTUARY                                                           |
|  Every Dish Has a Story                                                           |
|                                                                                   |
|  +-------------+  +-------------+  +-------------+  +-------------+               |
|  | Card 01     |  | Card 02     |  | Card 03     |  | Card 04     |  [drifts ->] |
|  | Butcher     |  | Copper Pan  |  | Herb Garden |  | Searing     |               |
|  | Knife Prep  |  | Reduction   |  | Harvest     |  | Emulsion    |               |
|  +-------------+  +-------------+  +-------------+  +-------------+               |
|                                                                                   |
|  [ 35+ Years ]          [ 100% Tested ]           [ 12 Classical ]                |
|  Gastronomic Legacy     Kitchen Precision         French Foundations              |
+-----------------------------------------------------------------------------------+
```

- **Visual Composition:**
  - Background cross-fades from dark kitchen to French linen cream (`#fbf9f5`) with delicate warm borders.
  - Large serif display headline: *"Every Dish Has a Story"* with italic accent.
  - A staggered horizontal deck of 4 vertical culinary cards (16:10 portrait format):
    1. *The Butcher's Table:* Solid walnut butcher block with Damascus chef's knife.
    2. *Copper & Flame:* Polished French copper saucepan reducing aromatics.
    3. *The Garden Sanctuary:* Fresh thyme, rosemary, and sage harvested at dawn.
    4. *The Final Pan Finish:* Glistening demi-glace emulsion poured over roasted cuts.
  - Beneath the cards: 3 editorial milestone columns with gold stat counters (`35+ Years Gastronomy`, `100% Tested Precision`, `12 Classical Techniques`).
- **Scroll Progression (150vh range):**
  - As user scrolls, the cards drift at alternating parallax speeds (`y: +40px`, `y: -30px`, `y: +20px`, `y: -45px`), creating a loose, tactile magazine feel.
- **GSAP Strategy:** ScrollTrigger with `scrub: 0.6`, gentle stagger on milestone columns.
- **Assets Needed:** High-resolution editorial photography for the 4 story cards (reused from project/Variant archives & optimized).

---

### Section 04 — Recipe Editorial Collage (04 RECIPE COLLAGE)

```
+-----------------------------------------------------------------------------------+
|  [LIGHT LINEN CANVAS]                                                             |
|                                                                                   |
|  GASTRONOMIC SELECTION                                                            |
|  Handcrafted Recipes For The Modern Table                                         |
|                                                                                   |
|  +---------------+    +-----------------------+    +----------------+             |
|  | [Card A]      |    | [Card B]              |    | [Card C]       |             |
|  | Roast Chicken |    | Saffron Risotto       |    | Sourdough      |             |
|  | (Tall)        |    | (Wide Landscape)      |    | (Square)       |             |
|  +---------------+    +-----------------------+    +----------------+             |
|                                                                                   |
|            +-----------------------------------------------+                      |
|            | [FOCAL CARD D]                                |                      |
|            | Masterclass Beef Wellington                   |                      |
|            | (Center Stage - EXPANDS ON SCROLL)            |                      |
|            +-----------------------------------------------+                      |
|                                                                                   |
|  +---------------------+                       +--------------------+             |
|  | [Card E]            |                       | [Card F]           |             |
|  | Lobster Thermidor   |                       | Pan Emulsion       |             |
|  +---------------------+                       +--------------------+             |
+-----------------------------------------------------------------------------------+
```

- **Visual Composition:**
  - An irregular magazine collage containing 6 real JamesThew recipes with varied aspect ratios.
  - Real database content: Title, Author, Prep time, Servings, and Member status badge.
- **Scroll Progression & Focal Zoom (220vh scroll range):**
  - **Start (0–40%):** Cards float in a loose asymmetric grid with smooth parallax drift as user scrolls.
  - **Transformation (40–100%):** The central Beef Wellington focal card begins to enlarge (`scale: 1 → 1.45`), while the surrounding cards (`Roast Chicken`, `Risotto`, `Sourdough`) translate outward toward the viewport boundaries and fade out (`opacity: 1 → 0`).
  - **Section Exit:** The focal Beef Wellington card expands to fill the viewport, executing a dark color flood that seamlessly transitions the page into Section 05.
- **GSAP Strategy:** Pinned timeline with `scrub: 1.0`, scaling the focal card and translating surrounding cards along radial trajectories (`x: ±150px`, `y: ±100px`).
- **Assets Needed:** 6 high-resolution recipe images (`classic-roast-chicken.jpg`, `seafood-saffron-risotto.jpg`, `sourdough-boule.jpg`, `beef-wellington.jpg`, plus 2 new generated masterclass dishes).

---

### Section 05 — Dark Signature Dish Feature (05 DARK SIGNATURE)

```
+-----------------------------------------------------------------------------------+
|  [OBSIDIAN CANVAS: #090b0e]                                                       |
|                                                                                   |
|                                   S I G N A T U R E                               |
|                                                                                   |
|                         +-----------------------------------+                     |
|                         |    ( AMBIENT AMBER HALO GLOW )    |                     |
|                         |                                   |                     |
|                         |   [ HERO BEEF WELLINGTON DISH ]   |                     |
|                         |    Center-cut prime beef,         |                     |
|                         |    black truffle duxelles,        |                     |
|                         |    golden all-butter lattice      |                     |
|                         +-----------------------------------+                     |
|                                                                                   |
|                             M A S T E R C L A S S                                 |
|                                                                                   |
|               [ 60 Mins Prep ]   [ 6 Servings ]   [ Members Exclusive ]           |
|                               [ Unlock Recipe -> ]                                |
+-----------------------------------------------------------------------------------+
```

- **Visual Composition:**
  - Deep obsidian black background (`#090b0e`).
  - A monumental gourmet presentation of the Beef Wellington medallion centered on the screen with warm amber backlighting (`--jt-gold-glow`).
  - Giant split editorial serif typography layered with 3D z-index:
    - `"SIGNATURE"` floats **behind** the upper plate edge (`z-index: 2`).
    - The plated dish sits in front (`z-index: 4`).
    - `"MASTERCLASS"` floats **behind/beneath** the lower plate rim (`z-index: 2`).
  - Floating micro-chips: `60 Mins Prep · 6 Servings · Members Exclusive · Unlock Recipe →`.
- **Scroll Progression (180vh scroll range):**
  - Dish scales up subtly (`scale: 0.92 → 1.05`) with a gentle vertical float (`y: 40px → -20px`).
  - Background typography slightly separates vertically (`SIGNATURE` moves up 30px, `MASTERCLASS` moves down 30px).
  - Ambient backlight expands with a warm cinematic pulse.
- **GSAP Strategy:** Pinned section (`pin: true`, `scrub: 0.8`), dual z-index typography planes.
- **Assets Needed:** `hero-dish-beef-wellington.webp`, `hero-plate-ceramic.webp`, SVG amber backlight gradient.

---

### Section 06 — Horizontal Signature Recipe Collection (06 HORIZONTAL COLLECTION)

```
+-----------------------------------------------------------------------------------+
|  [PINNED HORIZONTAL TRACK - VERTICAL SCROLL DRIVES HORIZONTAL TRANSLATION]        |
|                                                                                   |
|  01 SIGNATURE RECIPES                                                             |
|                                                                                   |
|  +----------------+    +----------------+    +----------------+    +------------+ |
|  | CARD 01        |    | CARD 02        |    | CARD 03        |    | CARD 04    | |
|  | Beef           |    | Saffron        |    | Roast Herb     |    | Artisanal  | |
|  | Wellington     |    | Risotto        |    | Chicken        |    | Sourdough  | |
|  |                |    |                |    |                |    |            | |
|  | (Halo Glow)    |    | (Halo Glow)    |    | (Halo Glow)    |    | (Halo Glow)| |
|  | [Explore ->]   |    | [Explore ->]   |    | [Explore ->]   |    | [Explore ->| |
|  +----------------+    +----------------+    +----------------+    +------------+ |
|                                                                     [---- TRACK -]|
+-----------------------------------------------------------------------------------+
```

- **Direct Reference Parallel:** Directly mirrors the horizontal bottle cards in `Reference Video.mp4` (Frames 022–024) where vertical scrolling drives horizontal movement of products illuminated with soft halo backlights.
- **Visual Composition:**
  - Pinned viewport (`height: 100vh`).
  - Horizontal track containing 4 large luxury recipe cards (420px wide × 580px high).
  - Each card features:
    - Index number (`01`, `02`, `03`, `04`) in subtle gold serif.
    - Large photorealistic dish visual mounted over a soft ambient halo backlight.
    - Recipe title, prep time, difficulty badge, and membership lock status.
    - Interactive "Explore Recipe →" button linking directly to `@recipe.Slug`.
- **Scroll Progression (300vh scroll range):**
  - User scrolls vertically down.
  - The horizontal track translates from `x: 0` to `x: -(trackWidth - windowWidth)`.
  - Individual cards exhibit subtle depth tilting on mouse move / scroll momentum.
- **GSAP Strategy:**
  ```javascript
  gsap.to(horizontalTrack, {
    x: function() { return -(horizontalTrack.scrollWidth - window.innerWidth + 80); },
    ease: "none",
    scrollTrigger: {
      trigger: horizontalSection,
      pin: true,
      scrub: 1,
      end: () => "+=" + (horizontalTrack.scrollWidth - window.innerWidth + 500),
      invalidateOnRefresh: true
    }
  });
  ```
- **Assets Needed:** Live seeded recipes: `beef-wellington.jpg`, `seafood-saffron-risotto.jpg`, `classic-roast-chicken.jpg`, `sourdough-boule.jpg`.
- **Mobile Behavior:** Horizontal snap-carousel with native touch swipe and progress indicator pills (zero JS pin on mobile).

---

### Section 07 — Culinary Technique / Masterclass (07 MASTERCLASS)

```
+-----------------------------------------------------------------------------------+
|  [OBSIDIAN CANVAS]                                                                |
|                                                                                   |
|  KITCHEN DISCIPLINE                                                               |
|  Technique Changes Everything                                                     |
|                                                                                   |
|        +-------------------+  +-------------------+  +-------------------+        |
|        | TECHNIQUE 01      |  | TECHNIQUE 02      |  | TECHNIQUE 03      |        |
|        | Knife Mastery     |  | Pan Kinetics      |  | Classical         |        |
|        | Pinch Grip & Claw |  | Maillard Reaction |  | Emulsions         |        |
|        | [Read Secret ->]  |  | [Read Secret ->]  |  | [Read Secret ->]  |        |
|        +-------------------+  +-------------------+  +-------------------+        |
|                                                                                   |
|  [Subtle kinetic text reveal rises from below as cards settle]                    |
+-----------------------------------------------------------------------------------+
```

- **Visual Composition:**
  - Heading: *"Technique Changes Everything"* with subline *"The difference between cooking and gastronomy is discipline."*
  - 3 large technique cards based on real JamesThew tips:
    1. *Knife Mastery & Precision Cuts* (`knife-skills-cut.jpg`)
    2. *The Science of Pan Searing & Maillard Reaction* (`classic-roast-chicken.jpg`)
    3. *Mastering Classical French Sauce Emulsions* (`pan-sauce-emulsion.jpg`)
- **Scroll Progression (160vh scroll range):**
  - **Start:** Cards enter from three distinct angles (left card enters from `-80px X`, right card from `+80px X`, center card ascends from `+60px Y`).
  - **Scroll:** Cards travel inward, overlap momentarily with 3D perspective shadows, and settle into an elegant tri-column arrangement. Hovering or scrolling through magnifies the active card.
- **GSAP Strategy:** Staggered `fromTo` timeline with `scrub: 0.6`.
- **Assets Needed:** Seeded tip assets (`knife-skills-cut.jpg`, `pan-sauce-emulsion.jpg`, and new high-res sear close-up).

---

### Section 08 — Contests & Community Experiences (08 EXPERIENCES)

```
+-----------------------------------------------------------------------------------+
|  [DARK CHARCOAL CANVAS]                                                           |
|                                                                                   |
|  CULINARY COMPETITIONS                                                            |
|  Test Your Craft Against The Community                                            |
|                                                                                   |
|     +------------------+     +------------------+     +------------------+        |
|     | CONTEST 01       |     | CONTEST 02       |     | CONTEST 03       |        |
|     | Autumn Heritage  |     | Zero-Waste Knife |     | Summer Seafood   |        |
|     | Stew Showdown    |     | Prep Challenge   |     | Showcase         |        |
|     | [ACTIVE NOW]     |     | [OPENS SOON]     |     | [WINNERS POSTED] |        |
|     | Prize: Chef      |     | Prize: Pro Chef  |     | Winner: Marcus V.|        |
|     | Master Trophy    |     | Artisan Apron    |     | View Entry ->    |        |
|     | [Submit Entry ->]|     | [View Rules ->]  |     | [Read Recap ->]  |        |
|     +------------------+     +------------------+     +------------------+        |
+-----------------------------------------------------------------------------------+
```

- **Direct Reference Parallel:** Matches the 3 degustazione packages (Silver, Gold, Platinum) in `Reference Video.mp4` (Frame 025).
- **Visual Composition:**
  - 3 luxury experience cards based on real JamesThew contests:
    1. *Autumn Heritage Stew Showdown* (Active: Prize Trophy & Editorial Feature).
    2. *Zero-Waste Knife & Prep Wisdom* (Upcoming: Professional Chef Apron).
    3. *Summer Artisanal Seafood Showcase* (Concluded: Winner Marcus Vance spotlight).
  - Status pill badges (`Active Now` in emerald/gold, `Opens Soon` in amber, `Winners Announced` in violet).
- **Scroll Progression:** Cards fan out from a stacked deck into a clean three-column comparative spread.
- **GSAP Strategy:** Card fan-out animation using `transformOrigin: "bottom center"`, `rotation: [-6, 0, 6] → [0, 0, 0]`.
- **Assets Needed:** Contest images (`classic-roast-chicken.jpg`, `seafood-saffron-risotto.jpg`).

---

### Section 09 — Final Editorial CTA (09 EDITORIAL CTA)

```
+-----------------------------------------------------------------------------------+
|  [LIGHT LINEN CANVAS: #fbf9f5]                                                    |
|                                                                                   |
|  THE JOURNEY BEGINS                                                               |
|  Your Next Dish                                                                   |
|  Starts Here                                                                      |
|                                                                                   |
|               +-------------------------------------------+                       |
|               |    [ OVERLAPPING SOURDOUGH & OLIVE OIL ]  |                       |
|               |    Crackling artisan loaf & herbs         |                       |
|               +-------------------------------------------+                       |
|                                                                                   |
|       [Browse All Recipes]    [Cooking Secrets]    [Join Masterclasses]           |
|                                                                                   |
|  Simulated Academic Project · Demonstration Mode · Approved by Administrator      |
+-----------------------------------------------------------------------------------+
```

- **Visual Composition:**
  - Seamless return to French linen ivory (`#fbf9f5`).
  - Monumental display serif typography: *"Your Next Dish Starts Here"*.
  - An artisanal bread & olive oil visual overlapping the right half of the typography with subtle paper drop-shadow.
  - Action Cluster:
    - Primary Button: `Browse All Recipes (@Model.TotalRecipesCount)`
    - Secondary Glass: `Cooking Secrets (@Model.TotalTipsCount)`
    - Gold Button: `Join Masterclasses`
  - Subtle disclaimer pill for academic demonstration integrity.
- **Scroll Progression:** As the section enters, the bread visual floats across the text from right to left (`x: 80px → 0px`), creating tactile physical depth.
- **GSAP Strategy:** ScrollTrigger scrub with parallax text/image offset.

---

### Section 10 — Premium Footer (10 FOOTER)

```
+-----------------------------------------------------------------------------------+
|  [OBSIDIAN CHARCOAL: #090b0e]                                                     |
|                                                                                   |
|   J  A  M  E  S     T  H  E  W     (GIANT LOW-OPACITY WATERMARK: 4% OPACITY)      |
|                                                                                   |
|  [LOGO & CREST]          [GASTRONOMY]     [MASTERCLASSES]    [COMMUNITY]          |
|  Master Chef             • All Recipes    • Beef Wellington  • Contests           |
|  James Thew Culinary     • Knife Skills   • French Sauces    • Hall of Fame       |
|  Platform                • Pan Searing    • Members Login    • FAQ & Help         |
|                          • Bread Boule    • Register Account • Site Feedback      |
|                                                                                   |
|  -------------------------------------------------------------------------------  |
|  © 2026 James Thew Culinary Excellence. Academic Project. All Rights Reserved.    |
+-----------------------------------------------------------------------------------+
```

- **Visual Composition:**
  - Rich obsidian background with giant background watermark text: `"JAMES THEW"` at 4% opacity in serif uppercase.
  - Multi-column culinary navigation links, verified routes (`asp-controller`, `asp-action`), clean legal disclaimer.
  - Subtle gold accent border on top.

---

## 5. Asset Generation & Reuse Architecture

### 5.1 Project Assets to Reuse
| File Path | Description | Usage in Redesign |
| :--- | :--- | :--- |
| `hero-cooking-scroll.mp4` / `.webm` | Primary cooking background video | Section 01 Hero & Section 02 Transformation background |
| `hero-dish-beef-wellington.webp` | Center-cut Beef Wellington on stoneware | Section 02 Transformation & Section 05 Dark Signature |
| `hero-plate-ceramic.webp` | Handcrafted charcoal ceramic plate base | Section 02 & Section 05 Presentation base |
| `hero-cherry-tomato.webp` | High-res cherry tomato cutout | Section 01 Hanging Hero & Section 02 Assembly |
| `hero-basil-leaf.webp` | High-res fresh basil cutout | Section 01 Hanging Hero & Section 02 Assembly |
| `hero-garlic-clove-cutout.webp` | High-res garlic clove cutout | Section 02 Transformation & Section 04 Collage |
| `hero-chili-cutout.webp` | High-res red chili cutout | Section 02 Transformation |
| `hero-lemon-slice-cutout.webp` | High-res translucent lemon wheel | Section 02 Transformation |
| `classic-roast-chicken.jpg` | Golden roasted chicken with vegetables | Section 04 Collage & Section 06 Horizontal Gallery |
| `seafood-saffron-risotto.jpg` | Creamy saffron risotto with prawns | Section 04 Collage & Section 06 Horizontal Gallery |
| `sourdough-boule.jpg` | Artisanal blistered sourdough boule | Section 04 Collage & Section 09 Editorial CTA |
| `knife-skills-cut.jpg` | Professional pinch grip knife cut | Section 03 Story & Section 07 Technique Masterclass |
| `pan-sauce-emulsion.jpg` | Velvety French pan sauce emulsion | Section 03 Story & Section 07 Technique Masterclass |

### 5.2 Antigravity Generative Images to Create (Nano Banana / Google Image Gen)
Only 4 high-impact assets are required to complete the editorial narrative:

1. **`story-copper-pan-reduction`** (1024×1024, WebP):
   - *Prompt:* Photorealistic luxury food photography of polished French copper saucepan simmering rich aromatic reduction with fresh thyme and shallots on professional gas burner, warm cinematic side lighting, dark moody kitchen backdrop.
   - *Target Use:* Section 03 Story card 02.
2. **`story-herb-garden-harvest`** (1024×1024, WebP):
   - *Prompt:* Photorealistic editorial photography of fresh culinary herbs (rosemary, sage, thyme) in rustic wooden trug on sunlit stone kitchen windowsill, morning dew, warm natural sunlight.
   - *Target Use:* Section 03 Story card 03.
3. **`recipe-lobster-thermidor`** (1024×1024, WebP):
   - *Prompt:* Photorealistic gourmet restaurant plating of classic Lobster Thermidor in shell, gratinated golden gruyère crust, tarragon cream, micro-greens, dark slate plate, high-end fine dining magazine aesthetic.
   - *Target Use:* Section 04 Recipe collage card E.
4. **`cta-sourdough-olive-oil`** (1024×1024, WebP):
   - *Prompt:* Photorealistic close-up of torn crusty artisanal sourdough loaf with dipping bowl of rich green extra virgin olive oil and sea salt flakes on natural linen cloth, bright warm daylight, luxury culinary editorial.
   - *Target Use:* Section 09 Final CTA overlapping visual.

---

## 6. Technical & Motion Architecture

### 6.1 JavaScript Architecture (`landing-scroll.js`)
All section timelines will be organized into modular builder functions governed by `gsap.matchMedia()`:

```javascript
document.addEventListener('DOMContentLoaded', function () {
    if (typeof window.gsap === 'undefined' || typeof window.ScrollTrigger === 'undefined') return;

    var gsap = window.gsap;
    var ScrollTrigger = window.ScrollTrigger;
    gsap.registerPlugin(ScrollTrigger);

    var mm = gsap.matchMedia();

    // DESKTOP (min-width: 992px)
    mm.add('(min-width: 992px) and (prefers-reduced-motion: no-preference)', function () {
        initHeroChoreography();           // Section 01
        initTransformationChoreography(); // Section 02
        initStoryParallax();              // Section 03
        initCollageFocalZoom();           // Section 04
        initDarkSignatureFeature();       // Section 05
        initHorizontalCollection();       // Section 06
        initMasterclassCards();           // Section 07
        initContestDeckFan();             // Section 08
        initEditorialCtaParallax();       // Section 09
    });

    // TABLET (768px - 991px)
    mm.add('(min-width: 768px) and (max-width: 991px) and (prefers-reduced-motion: no-preference)', function () {
        initTabletChoreography();
    });

    // MOBILE (< 768px)
    mm.add('(max-width: 767px) and (prefers-reduced-motion: no-preference)', function () {
        initMobileChoreography();
    });

    // REDUCED MOTION (WCAG 2.2 AA)
    mm.add('(prefers-reduced-motion: reduce)', function () {
        initReducedMotionStatic();
    });
});
```

### 6.2 Responsive Strategy Matrix

| Feature / Section | Desktop (≥ 992px) | Tablet (768px – 991px) | Mobile (< 768px) | Prefers-Reduced-Motion |
| :--- | :--- | :--- | :--- | :--- |
| **01 Hero** | Asymmetric distributed text + 2 hanging items + video scrub | Scaled text + 2 hanging items + video scrub | Centered headline + 1 hanging tomato + gentle video scrub | Static Stage 1 hero, video paused on poster, 0 scrub |
| **02 Transformation** | 3-stage kinetic burst + plate arrival + Wellington slide | Scaled burst + plate arrival | Clean direct fade into plated Beef Wellington | Static assembled dish on stoneware plate |
| **03 Story** | 4-card staggered parallax row + 3 milestone stats | 2×2 card grid with subtle drift + stats | Vertical single-column cards with standard scroll | Static editorial grid |
| **04 Collage** | 6-card irregular mosaic with focal zoom into darkness | 4-card mosaic with gentle focus | 2-column card list with clean transitions | Static recipe grid |
| **05 Dark Signature** | Z-index split typography behind/in front of dish + glow | Z-index typography + scaled dish | Plated dish with centered typography above/below | Static dark card presentation |
| **06 Horizontal Collection** | Pinned viewport, vertical scroll translates horizontal track | Horizontal track with 1.8x range | Native touch horizontal swipe-carousel | Native horizontal scroll list with scrollbar |
| **07 Masterclass** | 3-card 3D perspective fan-in and focus switch | 3-card soft stagger | Vertical stack of 3 technique cards | Static 3-column cards |
| **08 Contests** | 3D card deck fan-out with rotation | Card spread with subtle tilt | Vertical cards with status badges | Static card list |
| **09 Editorial CTA** | Massive serif + parallax bread visual crossover | Scaled serif + centered bread visual | Clean stacked CTA layout | Static CTA |
| **10 Footer** | Obsidian canvas + giant watermark + 4 link columns | 3-column layout + watermark | 2-column stacked layout | Static footer |

---

## 7. Accessibility & Performance Strategy

### 7.1 Accessibility (WCAG 2.2 AA Compliance)
- **Headings Hierarchy:** Exactly one `<h1>` on the entire page (`Where Ingredients Become Craft`), followed by semantic `<h2>` for each section and `<h3>` for cards.
- **ARIA & Roles:** All decorative floating assets have `aria-hidden="true"`. Interactive elements have descriptive accessible names.
- **Color Contrast:**
  - Dark surfaces: text ratio ≥ 7:1 (pure white/ivory on obsidian).
  - Light surfaces: text ratio ≥ 6.5:1 (near-black on linen cream).
  - Accent badges: contrast ratio ≥ 4.5:1.
- **Keyboard Navigation:** All cards, buttons, and links retain visible focus rings (`:focus-visible`). Pinned sections do not trap focus or break tab traversal.
- **Prefers-Reduced-Motion:** Instantly unbinds all scrub timelines, pins, and transforms; displays rich static editorial layouts.

### 7.2 Performance Engineering
- **Asset Payload Optimization:** All images converted to WebP with responsive `srcset` and explicit `width`/`height` attributes to prevent Cumulative Layout Shift (CLS).
- **GPU Acceleration:** Animations restricted to `transform` (`translate3d`, `scale`, `rotate`) and `opacity`.
- **Memory Footprint:** Unloaded off-screen video frames; video pauses when out of viewport using IntersectionObserver.
- **Zero Framework Bloat:** 0kb new npm dependencies; purely Razor, CSS variables, and GSAP.

---

## 8. Exact Files That Will Be Modified

No backend files, models, controllers, or database schemas will be touched. All changes are strictly confined to the landing page frontend presentation:

1. `JamesThew/Views/Home/Index.cshtml` (Full 10-section editorial Razor markup)
2. `JamesThew/wwwroot/css/landing.css` (Editorial styles, tokens, grid layouts, horizontal track, responsive rules)
3. `JamesThew/wwwroot/js/landing-scroll.js` (GSAP master timelines, horizontal gallery scrub, card choreography)
4. `tests/JamesThew.Tests/CinematicHeroBrowserQATests.cs` (Updated automated browser Playwright QA verifying all 10 sections and viewports)
5. `JamesThew/wwwroot/assets/landing/editorial/` (Directory for new optimized generated assets)

---

## 9. Implementation Phases (Post-Approval)

- **Phase 1: Asset Preparation & Optimization**
  - Generate the 4 supplemental editorial assets (`story-copper-pan-reduction`, `story-herb-garden-harvest`, `recipe-lobster-thermidor`, `cta-sourdough-olive-oil`).
  - Optimize to WebP under `JamesThew/wwwroot/assets/landing/editorial/`.
- **Phase 2: Razor View & CSS Foundation (Sections 01–05)**
  - Implement Sections 01 through 05 in `Index.cshtml` and `landing.css`.
  - Wire Hero, Transformation, Story, Collage, and Dark Signature.
- **Phase 3: Extended Choreography (Sections 06–10)**
  - Implement Horizontal Signature Collection (Section 06), Masterclasses (Section 07), Contests (Section 08), Editorial CTA (Section 09), and Footer (Section 10).
  - Wire GSAP ScrollTrigger horizontal translation and deck fan-out in `landing-scroll.js`.
- **Phase 4: Responsive, Reduced-Motion & Automated QA**
  - Verify and tune at 1440×900, 1280×720, 768×1024, 430×932, and 390×844.
  - Run `dotnet build --no-incremental`, `dotnet test`, and full Playwright browser audit.

---
*End of Motion Blueprint. Awaiting user review and authorization before proceeding to implementation.*
