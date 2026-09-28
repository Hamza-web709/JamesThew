using JamesThew.Models;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Data;

public static class ContentSeeder
{
    public static async Task SeedContentAsync(ApplicationDbContext db)
    {
        await SeedFaqItemsAsync(db);
        await SeedEditorialContentAsync(db);
        await SeedContestsAsync(db);
    }

    private static async Task SeedFaqItemsAsync(ApplicationDbContext db)
    {
        var faqs = new (string Key, string Question, string Answer, int SortOrder)[]
        {
            (
                "faq-membership",
                "How do I become a registered member of JamesThew.com?",
                "To become a member, click 'Register' in the navigation bar to create your account with your name, email, and password. Once registered, you can choose a demo subscription plan ($10/month or $100/year). In this academic demonstration environment, membership requests are submitted to the pending approval queue and manually approved by the administrator. Once approved, you gain full access to all exclusive masterclass recipes and cooking tips.",
                1
            ),
            (
                "faq-charges",
                "What are the subscription charges and membership plans?",
                "JamesThew.com offers two demo subscription tiers: a Monthly Plan at $10/month and an Annual Plan at $100/year. Please note that this is a simulated academic demonstration project—no real credit cards, payment gateways, or monetary transactions are processed. All payment receipts and plan activations are strictly labelled for academic demonstration.",
                2
            ),
            (
                "faq-recipes-access",
                "How can I access recipes and cooking tips, and are there any viewing fees?",
                "All Free recipes and basic cooking tips published by James Thew are completely open and accessible to the public without requiring any registration or login. However, exclusive masterclass recipes and advanced culinary techniques marked 'Members-Only' require an active, approved membership. Unauthenticated visitors can view public summaries and culinary overviews of premium items, but secret ingredients and step-by-step procedures remain securely locked.",
                3
            ),
            (
                "faq-contest-unregistered",
                "Can unregistered visitors participate in recipe and tip contests?",
                "No. Under our updated demo security policy, unregistered visitors and guests are welcome to browse active contests, review competition rules, and view published winners and announcements. However, submitting contest entries requires a verified member account to ensure fair competition, valid authorship, and contest integrity.",
                4
            ),
            (
                "faq-submit-content",
                "How can members submit or upload their own recipes and cooking tips?",
                "Authenticated members can submit recipes (including ingredient lists and ordered preparation steps) and cooking tips through their member dashboard. Submitted community contributions enter a 'Pending' moderation queue. To protect community standards, customer contributions are only published to the public catalog (as Free content) once reviewed and approved by the site administrator.",
                5
            ),
            (
                "faq-feedback",
                "How can I post feedback about a recipe or the website?",
                "Feedback is reserved for registered members to prevent automated spam and maintain constructive culinary discourse. Logged-in members can submit specific comments directly on accessible recipe detail pages or send general feedback through the Site Feedback page. Unauthenticated visitors will see a sign-in prompt.",
                6
            ),
            (
                "faq-winners",
                "How are contest winners selected and announced?",
                "Once a contest submission deadline concludes, the administrator reviews all eligible member submissions based on creativity, culinary technique, and adherence to contest rules. The chosen winner is announced atomically on the public Announcements page and on the respective contest result page, showcasing the winning member's display name and winning entry.",
                7
            )
        };

        foreach (var item in faqs)
        {
            var existing = await db.FaqItems.FirstOrDefaultAsync(x => x.QuestionKey == item.Key);
            if (existing is null)
            {
                db.FaqItems.Add(new FaqItem
                {
                    QuestionKey = item.Key,
                    Question = item.Question,
                    Answer = item.Answer,
                    SortOrder = item.SortOrder,
                    IsPublished = true
                });
            }
            else
            {
                existing.Question = item.Question;
                existing.Answer = item.Answer;
                existing.SortOrder = item.SortOrder;
                existing.IsPublished = true;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedEditorialContentAsync(ApplicationDbContext db)
    {
        // Free Recipe 1: Classic Roast Herb Chicken
        await SeedRecipeAsync(db, new RecipeSeedDto
        {
            Title = "James's Classic Roast Herb Chicken",
            Slug = "jamess-classic-roast-herb-chicken",
            Summary = "[DEMO CONTENT] Golden-crisp whole roast chicken basted in garlic-herb butter, roasted alongside caramelized baby potatoes, carrots, and sweet shallots.",
            Visibility = ContentVisibility.Free,
            ImageUrl = "/images/recipes/classic-roast-chicken.jpg",
            Servings = 4,
            PrepMinutes = 20,
            CookMinutes = 75,
            Ingredients =
            [
                ("Whole free-range chicken", "1", "approx 1.8 kg"),
                ("Unsalted butter, softened", "60", "g"),
                ("Fresh rosemary, finely chopped", "4", "sprigs"),
                ("Fresh thyme, leaves picked", "6", "sprigs"),
                ("Garlic, crushed", "5", "cloves"),
                ("Fresh lemon, halved", "1", "whole"),
                ("Baby Dutch yellow potatoes, halved", "500", "g"),
                ("Carrots, cut into batons", "3", "medium"),
                ("Cold-pressed extra virgin olive oil", "2", "tbsp"),
                ("Flaky Maldon sea salt", "1", "tsp"),
                ("Cracked black peppercorns", "1", "tsp")
            ],
            Steps =
            [
                "Preheat oven to 200°C (400°F). Pat the chicken completely dry inside and out using paper towels for maximum skin crispness.",
                "In a small bowl, blend softened butter with chopped rosemary, thyme, 2 cloves crushed garlic, sea salt, and black pepper.",
                "Gently loosen the skin over the chicken breasts and thighs, spreading half the herb butter directly under the skin, and massage the remainder over the exterior.",
                "Stuff the cavity with the halved lemon, remaining garlic cloves, and a few whole herb sprigs. Truss the legs snugly with kitchen twine.",
                "Toss baby potatoes and carrots with olive oil, salt, and pepper in a large heavy cast-iron roasting pan. Place chicken breast-side up in the center.",
                "Roast for 70-75 minutes until the internal thigh temperature reaches 75°C (165°F) and juices run clear. Rest loosely tented with foil for 15 minutes before carving."
            ]
        });

        // Free Recipe 2: Artisanal Crusty Sourdough Boule
        await SeedRecipeAsync(db, new RecipeSeedDto
        {
            Title = "Artisanal Crusty Sourdough Boule",
            Slug = "artisanal-crusty-sourdough-boule",
            Summary = "[DEMO CONTENT] Naturally leavened rustic sourdough bread with a crackling blistered crust, airy tender crumb, and balanced lactic acidity.",
            Visibility = ContentVisibility.Free,
            ImageUrl = "/images/recipes/sourdough-boule.jpg",
            Servings = 8,
            PrepMinutes = 45,
            CookMinutes = 40,
            Ingredients =
            [
                ("Unbleached strong bread flour (12.5% protein)", "450", "g"),
                ("Stone-ground whole wheat flour", "50", "g"),
                ("Filtered water (approx 26°C / 78°F)", "350", "ml"),
                ("Active sourdough starter (100% hydration)", "100", "g"),
                ("Fine mineral sea salt", "10", "g")
            ],
            Steps =
            [
                "Autolyse: In a large glass bowl, combine the bread flour, whole wheat flour, and 330ml of the water until no dry flour remains. Cover and let rest for 45 minutes.",
                "Inoculation: Add the 100g bubbly active starter and remaining 20ml water mixed with salt. Dimple and pinch into dough until thoroughly incorporated.",
                "Bulk Fermentation: Perform 4 sets of stretch-and-folds spaced 30 minutes apart. Allow dough to ferment at room temperature until increased in volume by 50% with rounded edges.",
                "Shaping: Tip dough onto a lightly dusted work surface, pre-shape into a loose round, and rest 20 minutes. Perform final bench stitch and roll into a taut boule.",
                "Cold Retard: Place boule smooth side down into a rice-flour-dusted banneton basket. Seal in a plastic bag and refrigerate at 4°C for 12-16 hours.",
                "Baking: Preheat Dutch oven to 250°C (485°F) for 45 minutes. Turn cold loaf onto parchment, score with a razor lame at a 30° angle, and bake covered for 20 minutes. Remove lid, reduce to 220°C (425°F), and bake 20 more minutes until mahogany. Cool completely before slicing."
            ]
        });

        // Free Recipe 3: Mediterranean Seafood Saffron Risotto
        await SeedRecipeAsync(db, new RecipeSeedDto
        {
            Title = "Mediterranean Seafood Saffron Risotto",
            Slug = "mediterranean-seafood-saffron-risotto",
            Summary = "[DEMO CONTENT] Creamy Carnaroli rice slowly simmered in saffron-infused shellfish broth, topped with seared king prawns, tender calamari, and fresh flat-leaf parsley.",
            Visibility = ContentVisibility.Free,
            ImageUrl = "/images/recipes/seafood-saffron-risotto.jpg",
            Servings = 4,
            PrepMinutes = 25,
            CookMinutes = 30,
            Ingredients =
            [
                ("Superfine Carnaroli rice", "320", "g"),
                ("Wild tiger prawns, peeled and deveined", "250", "g"),
                ("Fresh calamari, sliced into rings", "150", "g"),
                ("Rich seafood or fish stock", "1", "liter"),
                ("Spanish saffron threads (steeped in 50ml warm stock)", "1", "pinch"),
                ("Shallot, minced fine", "1", "medium"),
                ("Dry crisp white wine", "100", "ml"),
                ("Chilled unsalted butter, cubed", "35", "g"),
                ("Extra virgin olive oil", "2", "tbsp"),
                ("Fresh flat-leaf Italian parsley, chopped", "2", "tbsp"),
                ("Sea salt and white pepper", null, "to taste")
            ],
            Steps =
            [
                "In a wide skillet, heat 1 tbsp olive oil over medium-high. Sear prawns and calamari for 90 seconds until opaque and lightly caramelized. Remove to a warm plate.",
                "In a heavy-bottomed risotto pan, sweat minced shallots in remaining olive oil over low heat until translucent without browning.",
                "Add Carnaroli rice and toast for 2-3 minutes until grains are hot and edges become pearlescent.",
                "Deglaze with white wine, stirring constantly until alcohol evaporates and liquid is almost fully absorbed.",
                "Begin adding simmering seafood stock one ladle at a time, stirring gently and waiting until each ladle is absorbed before adding the next. At the 12-minute mark, stir in the saffron infusion.",
                "After 17-18 minutes when rice is al dente, remove pan from heat. Fold in the cooked seafood, cold butter cubes, and chopped parsley (mantecatura) for a glossy, wavy finish. Serve immediately."
            ]
        });

        // Paid Recipe 1 (Members-Only): James's Masterclass Beef Wellington
        await SeedRecipeAsync(db, new RecipeSeedDto
        {
            Title = "James's Masterclass Beef Wellington",
            Slug = "jamess-masterclass-beef-wellington",
            Summary = "[DEMO CONTENT] Exclusive Members-Only Masterclass: Center-cut prime beef tenderloin seared and enrobed in black truffle mushroom duxelles, aged prosciutto di Parma, and crisp golden puff pastry.",
            Visibility = ContentVisibility.MembersOnly,
            ImageUrl = "/images/recipes/beef-wellington.jpg",
            Servings = 6,
            PrepMinutes = 60,
            CookMinutes = 40,
            Ingredients =
            [
                ("Center-cut prime beef fillet (Chateaubriand)", "800", "g"),
                ("Mixed cremini and porcini mushrooms", "500", "g"),
                ("Black truffle paste or oil", "2", "tbsp"),
                ("Prosciutto di Parma slices", "8", "thin slices"),
                ("All-butter puff pastry sheet", "500", "g"),
                ("Large egg yolks (beaten with milk for glaze)", "2", "large"),
                ("English mustard", "2", "tbsp"),
                ("Clarified butter or olive oil", "2", "tbsp"),
                ("Coarse sea salt and Tellicherry black pepper", null, "to taste")
            ],
            Steps =
            [
                "Season beef fillet generously with sea salt and black pepper. In a smoking hot cast-iron skillet, sear the fillet in clarified butter for 60 seconds per side until evenly browned. Transfer to a rack and brush immediately while warm with English mustard. Chill in refrigerator for 20 minutes.",
                "In a food processor, pulse mushrooms until finely minced. Cook in a dry pan over medium heat with thyme and truffle paste for 15-20 minutes until all mushroom moisture has completely evaporated (dry duxelles paste). Cool completely.",
                "Lay a double sheet of clingfilm on the worktop. Arrange prosciutto slices overlapping in a shingle pattern. Spread the cooled mushroom duxelles evenly over the prosciutto layer.",
                "Place chilled beef fillet in the center. Using the clingfilm, roll prosciutto tightly around the beef into a tight cylinder. Twist the clingfilm ends firmly to set the shape. Chill for at least 30 minutes to firm up.",
                "Roll puff pastry into a rectangle 4mm thick. Unwrap beef cylinder and place on pastry. Roll pastry around beef, trimming excess, and seal seams with egg wash. Wrap tightly in clingfilm and chill 20 minutes.",
                "Score top pastry lightly with a diamond lattice pattern using a paring knife. Brush thoroughly with egg yolk wash and sprinkle with flaky sea salt.",
                "Bake at 200°C (400°F) for 30-35 minutes until the pastry is deep golden brown and a probe thermometer inserted into the center reads 48°C (for rare) or 52°C (for medium-rare). Rest on a board for 12-15 minutes before slicing with a serrated knife."
            ]
        });

        // Free Tip 1: Knife Skills
        await SeedTipAsync(db, new TipSeedDto
        {
            Title = "Mastering Chef's Knife Grip and Precision Cuts",
            Slug = "mastering-chefs-knife-grip-and-precision-cuts",
            Summary = "[DEMO CONTENT] Master the professional pinch grip and guiding claw technique to improve kitchen speed, knife control, and chopping safety.",
            Visibility = ContentVisibility.Free,
            ImageUrl = "/images/tips/knife-skills-cut.jpg",
            Body = "Holding a chef's knife properly is the single most transformative skill in any culinary journey. Many home cooks make the mistake of gripping the handle with all five fingers, or placing their index finger flat along the spine of the blade. This causes blade wobble, wrist fatigue, and dangerous slips.\n\nThe Professional Pinch Grip: Choke up on the blade. Grip the heel of the blade directly between your thumb and the side of your index finger, curling your remaining three fingers comfortably around the handle. This shifts the center of balance directly into your hand, giving you surgical control over every cut.\n\nThe Guiding 'Claw': Never leave your non-knife hand flat or fingers splayed. Curl your non-knife fingertips inward like a bear claw, resting your flat fingernails firmly on the vegetable with thumb tucked safely behind. Rest the flat side of the chef's knife against your middle knuckle as a guide.\n\nBoard Stability: Always place a slightly damp paper towel or silicone mat directly beneath your cutting board. A sliding cutting board is the number one cause of kitchen injuries.\n\nPrecision Cuts:\n- Julienne: 1/8 inch x 1/8 inch x 2 inches matchsticks.\n- Brunoise: Tiny, uniform 1/8 inch cubes derived from julienne.\n- Chiffonade: Tightly rolling leafy herbs (basil, sage) and slicing thinly into delicate ribbons."
        });

        // Free Tip 2: Pan Searing & Maillard Reaction
        await SeedTipAsync(db, new TipSeedDto
        {
            Title = "The Science of Pan Searing and the Maillard Reaction",
            Slug = "the-science-of-pan-searing-and-the-maillard-reaction",
            Summary = "[DEMO CONTENT] How to achieve a deep, caramelized restaurant crust by mastering surface moisture, pan heat capacity, and the Maillard reaction.",
            Visibility = ContentVisibility.Free,
            ImageUrl = "/images/recipes/classic-roast-chicken.jpg",
            Body = "That savory, deep-brown, umami-rich crust on a pan-seared steak or chicken thigh is not burning—it is the Maillard reaction, a complex chemical reaction between amino acids and reducing sugars that occurs between 140°C and 165°C (285°F to 330°F).\n\nRule 1: Dry Surfaces are Mandatory\nWater cannot exceed 100°C (212°F) under normal atmospheric pressure. If your protein has moisture on its exterior, all pan heat is wasted boiling water into steam instead of creating caramelization. Always pat meat dry with paper towels, or better yet, salt it and rest uncovered in the refrigerator overnight (dry brining) to dehydrate the surface.\n\nRule 2: Choose Heavy Thermal Mass Pans\nThin aluminum non-stick pans lose temperature the second cold meat hits the surface. Use heavy cast iron, carbon steel, or multi-ply stainless steel. These pans store substantial thermal energy and maintain high searing temperatures.\n\nRule 3: Use High Smoke Point Oils\nAvoid extra virgin olive oil or whole butter for high-heat searing—their solids burn at 175°C. Instead, use avocado oil, clarified butter (ghee), grapeseed, or beef tallow, which stay stable past 230°C (450°F).\n\nRule 4: Do Not Overcrowd the Pan\nLeave at least 1-2 inches of space between items. Crowding traps escaping moisture, converting your sear into a simmer."
        });

        // Paid Tip 1 (Members-Only): French Sauce Emulsions
        await SeedTipAsync(db, new TipSeedDto
        {
            Title = "Masterclass French Sauce Emulsions & Pan Deglazing",
            Slug = "masterclass-french-sauce-emulsions-and-pan-deglazing",
            Summary = "[DEMO CONTENT] Exclusive Members-Only Masterclass: Professional techniques for deglazing caramelized fond, emulsifying chilled butter, and stabilizing velvet pan sauces.",
            Visibility = ContentVisibility.MembersOnly,
            ImageUrl = "/images/tips/pan-sauce-emulsion.jpg",
            Body = "A great pan sauce is the hallmark of professional classical gastronomy. In this masterclass guide, we explore the molecular mechanics of transforming the browned bits stuck to your pan (the French 'fond') into a silk-smooth emulsion.\n\nStep 1: Fond Management\nAfter removing your seared protein to rest, pour off excess burnt fat from the skillet, leaving approximately one tablespoon of clean rendered fat. Sauté minced shallots and thyme over medium-low heat for 60 seconds until fragrant.\n\nStep 2: Acid Deglazing\nDeglaze the smoking pan with 100ml of dry wine (red for beef/lamb, dry white for poultry/seafood) or dry French vermouth. Use a wooden spatula or flat whisk to scrape every speck of caramelized fond from the bottom. Reduce the liquid by 75% until it forms a syrupy consistency.\n\nStep 3: Stock Reduction\nPour in 200ml of high-gelatin homemade veal or roasted chicken stock. Reduce over medium heat until the liquid coats the back of a spoon (nappe consistency).\n\nStep 4: Monter au Beurre (The Emulsion Secret)\nTake the pan completely off the heat. Vigorously swirl in 30g to 40g of refrigerator-cold, cubed unsalted butter, two cubes at a time. The cold temperature and rapid agitation disperse microscopic fat droplets into the warm reduction, creating an opaque, glossy, velvet emulsion. Never boil the sauce after mounting butter, or the emulsion will split.\n\nTroubleshooting Broken Sauces: If your sauce breaks and separates into an oily slick, whisk in one teaspoon of cold water or heavy cream off the heat to re-establish the emulsion boundary."
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedRecipeAsync(ApplicationDbContext db, RecipeSeedDto dto)
    {
        var existing = await db.ContentItems
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Ingredients)
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Steps)
            .FirstOrDefaultAsync(x => x.Slug == dto.Slug);

        if (existing is null)
        {
            var item = new ContentItem
            {
                Title = dto.Title,
                Slug = dto.Slug,
                Summary = dto.Summary,
                Kind = ContentKind.Recipe,
                Origin = ContentOrigin.Editorial,
                Visibility = dto.Visibility,
                PublicationStatus = PublicationStatus.Published,
                AuthorDisplayName = "Chef James Thew",
                ImageUrl = dto.ImageUrl,
                CreatedAtUtc = DateTime.UtcNow
            };

            var recipe = new Recipe
            {
                ContentItem = item,
                Servings = dto.Servings,
                PrepMinutes = dto.PrepMinutes,
                CookMinutes = dto.CookMinutes
            };

            int pos = 1;
            foreach (var ing in dto.Ingredients)
            {
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Position = pos++,
                    Name = ing.Name,
                    QuantityText = ing.Quantity,
                    Unit = ing.Unit
                });
            }

            pos = 1;
            foreach (var step in dto.Steps)
            {
                recipe.Steps.Add(new RecipeStep
                {
                    Position = pos++,
                    Instruction = step
                });
            }

            item.Recipe = recipe;
            db.ContentItems.Add(item);
        }
        else
        {
            existing.Title = dto.Title;
            existing.Summary = dto.Summary;
            existing.Visibility = dto.Visibility;
            existing.ImageUrl = dto.ImageUrl;
            if (existing.Recipe is not null)
            {
                existing.Recipe.Servings = dto.Servings;
                existing.Recipe.PrepMinutes = dto.PrepMinutes;
                existing.Recipe.CookMinutes = dto.CookMinutes;
            }
        }
    }

    private static async Task SeedTipAsync(ApplicationDbContext db, TipSeedDto dto)
    {
        var existing = await db.ContentItems
            .Include(x => x.Tip)
            .FirstOrDefaultAsync(x => x.Slug == dto.Slug);

        if (existing is null)
        {
            var item = new ContentItem
            {
                Title = dto.Title,
                Slug = dto.Slug,
                Summary = dto.Summary,
                Kind = ContentKind.Tip,
                Origin = ContentOrigin.Editorial,
                Visibility = dto.Visibility,
                PublicationStatus = PublicationStatus.Published,
                AuthorDisplayName = "Chef James Thew",
                ImageUrl = dto.ImageUrl,
                CreatedAtUtc = DateTime.UtcNow
            };

            var tip = new Tip
            {
                ContentItem = item,
                Body = dto.Body
            };

            item.Tip = tip;
            db.ContentItems.Add(item);
        }
        else
        {
            existing.Title = dto.Title;
            existing.Summary = dto.Summary;
            existing.Visibility = dto.Visibility;
            existing.ImageUrl = dto.ImageUrl;
            if (existing.Tip is not null)
            {
                existing.Tip.Body = dto.Body;
            }
        }
    }

    private static async Task SeedContestsAsync(ApplicationDbContext db)
    {
        var contests = new ContestSeedDto[]
        {
            new()
            {
                Title = "Autumn Heritage Stew Showdown",
                Slug = "autumn-heritage-stew-showdown",
                Summary = "Showcase your best slow-cooked, rich autumnal stew recipe using seasonal root vegetables and hearty cuts.",
                DescriptionAndRules = "Chef James Thew invites all registered members to submit their signature slow-cooked stew recipes. Whether beef bourguignon, lamb tagine, or hearty vegan bean stew, dishes must be thoroughly tested for home kitchen execution.\n\nRules & Criteria:\n1. Recipe must serve 4-6 portions.\n2. Total prep and cooking instructions must be clearly itemized.\n3. Evaluation will be based on depth of flavor, balance of aromatics, and clarity of kitchen instructions.\n4. Only authenticated members may submit entries during the open window.",
                Type = ContestType.Recipe,
                Status = ContestStatus.Published,
                PrizeDescription = "Chef James Thew Feature Article & Masterclass Culinary Trophy",
                ImageUrl = "/images/recipes/classic-roast-chicken.jpg",
                OpensAtUtc = DateTime.UtcNow.AddDays(-7),
                ClosesAtUtc = DateTime.UtcNow.AddDays(14)
            },
            new()
            {
                Title = "Zero-Waste Kitchen Knife & Prep Wisdom",
                Slug = "zero-waste-kitchen-knife-and-prep-wisdom",
                Summary = "Share your top professional knife maintenance, trimming, or scrap-saving technique to reduce kitchen waste.",
                DescriptionAndRules = "Efficiency and knife proficiency define the foundation of culinary artistry. Share a concise, actionable kitchen tip on blade sharpening, butchery scrap stock usage, or vegetable trimmings preservation.\n\nRules & Criteria:\n1. Tips must be practical, safe, and verifiable.\n2. Must focus on knife handling, blade honing, or culinary waste prevention.\n3. Concise submissions under 500 words are preferred.",
                Type = ContestType.Tip,
                Status = ContestStatus.Published,
                PrizeDescription = "Featured Community Spotlight & Professional Chef Apron",
                ImageUrl = "",
                OpensAtUtc = DateTime.UtcNow.AddDays(3),
                ClosesAtUtc = DateTime.UtcNow.AddDays(24)
            },
            new()
            {
                Title = "Summer Artisanal Seafood Showcase",
                Slug = "summer-artisanal-seafood-showcase",
                Summary = "Celebrating sustainable coastal cuisine, light citrus marinades, and pan-seared seafood perfection.",
                DescriptionAndRules = "This seasonal competition celebrated the freshest marine bounty with emphasis on delicate heat control and acidic balance.\n\nContest is concluded. Submissions are closed and winner announcements have been finalized.",
                Type = ContestType.Recipe,
                Status = ContestStatus.Published,
                PrizeDescription = "Gold Culinary Distinction & Published Recipe Spotlight",
                ImageUrl = "/images/recipes/seafood-saffron-risotto.jpg",
                OpensAtUtc = DateTime.UtcNow.AddDays(-45),
                ClosesAtUtc = DateTime.UtcNow.AddDays(-15)
            },
            new()
            {
                Title = "Winter Pastry Secrets [Draft Challenge]",
                Slug = "winter-pastry-secrets-draft-challenge",
                Summary = "Upcoming internal editorial challenge testing laminated dough techniques and flakiness secrets.",
                DescriptionAndRules = "Draft competition specifications under internal editorial review by Chef James Thew. Not yet available for public entry.",
                Type = ContestType.Tip,
                Status = ContestStatus.Draft,
                PrizeDescription = "Baking Master Distinction",
                ImageUrl = "",
                OpensAtUtc = DateTime.UtcNow.AddDays(30),
                ClosesAtUtc = DateTime.UtcNow.AddDays(60)
            }
        };

        foreach (var dto in contests)
        {
            var existing = await db.Contests.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Slug == dto.Slug);
            if (existing is null)
            {
                db.Contests.Add(new Contest
                {
                    Title = dto.Title,
                    Slug = dto.Slug,
                    Summary = dto.Summary,
                    DescriptionAndRules = dto.DescriptionAndRules,
                    Type = dto.Type,
                    Status = dto.Status,
                    PrizeDescription = dto.PrizeDescription,
                    ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl,
                    OpensAtUtc = dto.OpensAtUtc,
                    ClosesAtUtc = dto.ClosesAtUtc,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.Title = dto.Title;
                existing.Summary = dto.Summary;
                existing.DescriptionAndRules = dto.DescriptionAndRules;
                existing.Type = dto.Type;
                existing.Status = dto.Status;
                existing.PrizeDescription = dto.PrizeDescription;
                existing.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl;
                existing.OpensAtUtc = dto.OpensAtUtc;
                existing.ClosesAtUtc = dto.ClosesAtUtc;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
    }

    private sealed class RecipeSeedDto
    {
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public ContentVisibility Visibility { get; init; }
        public string ImageUrl { get; init; } = string.Empty;
        public int Servings { get; init; }
        public int PrepMinutes { get; init; }
        public int CookMinutes { get; init; }
        public (string Name, string? Quantity, string? Unit)[] Ingredients { get; init; } = [];
        public string[] Steps { get; init; } = [];
    }

    private sealed class TipSeedDto
    {
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public ContentVisibility Visibility { get; init; }
        public string ImageUrl { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
    }

    private sealed class ContestSeedDto
    {
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string DescriptionAndRules { get; init; } = string.Empty;
        public ContestType Type { get; init; }
        public ContestStatus Status { get; init; }
        public string? PrizeDescription { get; init; }
        public string? ImageUrl { get; init; }
        public DateTime OpensAtUtc { get; init; }
        public DateTime ClosesAtUtc { get; init; }
    }
}
