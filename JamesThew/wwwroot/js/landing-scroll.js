/**
 * JamesThew.com — Haute Cuisine & Editorial Scroll Choreography
 * Section-by-Section GSAP Master Timelines & Responsive MatchMedia Architecture
 * Follows the approved Reference Video Motion Blueprint.
 */

document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    // 1. Dependency Guard
    if (typeof window.gsap === 'undefined' || typeof window.ScrollTrigger === 'undefined') {
        console.warn('JamesThew: GSAP or ScrollTrigger not loaded.');
        return;
    }

    var gsap = window.gsap;
    var ScrollTrigger = window.ScrollTrigger;
    gsap.registerPlugin(ScrollTrigger);

    // 2. Full-bleed and dynamic header offset handling
    var mainEl = document.querySelector('.jt-main-content');
    if (mainEl) {
        mainEl.classList.add('jt-main-fullbleed');
    }

    var headerEl = document.getElementById('jtHeader') || document.querySelector('.jt-header');
    function updateHeaderOffset() {
        // Floating glass capsule overlays page content — no layout row reserved
        document.documentElement.style.setProperty('--jt-header-h', '0px');
        return 0;
    }
    updateHeaderOffset();
    window.addEventListener('resize', updateHeaderOffset, { passive: true });

    // Adaptive Header Theme Observer: smoothly toggles [data-theme] on floating capsule
    if (headerEl) {
        var themedSections = document.querySelectorAll('[data-header-theme]');
        themedSections.forEach(function (sec) {
            var theme = sec.getAttribute('data-header-theme');
            ScrollTrigger.create({
                trigger: sec,
                start: 'top 50px',
                end: 'bottom 50px',
                onEnter: function () { headerEl.setAttribute('data-theme', theme); },
                onEnterBack: function () { headerEl.setAttribute('data-theme', theme); }
            });
        });
    }

    // 3. Video Background Initialization & Safety Timeout
    var videoEl = document.getElementById('heroCookingVideo');
    var videoInitialized = false;

    function initAllChoreography() {
        if (videoInitialized) return;
        videoInitialized = true;
        buildChoreography(videoEl);
    }

    if (videoEl) {
        videoEl.pause();
        if (videoEl.readyState >= 1 && videoEl.duration > 0) {
            initAllChoreography();
        } else {
            videoEl.addEventListener('loadedmetadata', initAllChoreography, { once: true });
            setTimeout(initAllChoreography, 1000); // 1.0s safety fallback
        }
    } else {
        initAllChoreography();
    }

    // 4. Choreography Builder with Responsive MatchMedia
    function buildChoreography(video) {
        var mm = gsap.matchMedia();

        // ==================================================================
        // DESKTOP BREAKPOINT (>= 992px)
        // ==================================================================
        mm.add('(min-width: 992px) and (prefers-reduced-motion: no-preference)', function () {

            // --------------------------------------------------------------
            // SECTION 01: HERO CHOREOGRAPHY & VIDEO SCRUB
            // Continuous motion: typography moves gradually; at least one visual
            // anchor remains visibly dominant across progress 0.00 -> 1.00.
            // --------------------------------------------------------------
            var heroSection = document.getElementById('hero-story');
            if (heroSection) {
                var heroTl = gsap.timeline({
                    scrollTrigger: {
                        trigger: heroSection,
                        start: 'top top',
                        end: 'bottom bottom',
                        scrub: 0.8,
                        pin: true,
                        invalidateOnRefresh: true
                    },
                    defaults: { ease: 'none' }
                });

                // Video Scrub Synchronization (scrubs active prep/cooking footage)
                if (video) {
                    var rawDuration = (video.duration && !isNaN(video.duration) && video.duration > 0) ? video.duration : 10.0;
                    var videoProxy = { time: 0 };
                    heroTl.to(videoProxy, {
                        time: rawDuration * 0.45,
                        duration: 1.0,
                        onUpdate: function () {
                            if (video.readyState >= 1 && isFinite(videoProxy.time)) {
                                try {
                                    var target = Math.max(0, Math.min(videoProxy.time, rawDuration - 0.05));
                                    if (Math.abs(video.currentTime - target) > 0.04) {
                                        video.currentTime = target;
                                    }
                                } catch (e) {}
                            }
                        }
                    }, 0);
                }

                // Hanging Aromatics Ascend Smoothly (Remain visible across scroll)
                heroTl.to('#floatTomato', {
                    y: -120,
                    scale: 0.88,
                    rotation: -6,
                    opacity: 0.35,
                    duration: 0.85
                }, 0.10);

                heroTl.to('#floatBasil', {
                    y: -140,
                    scale: 0.90,
                    rotation: 8,
                    opacity: 0.35,
                    duration: 0.85
                }, 0.10);

                // Distributed Asymmetric Text: stays clearly visible (opacity >= 0.25) until handoff
                // Never fade completely to 0 at 0.60 leaving an empty black screen!
                heroTl.to('#heroTopLeft', {
                    x: -45,
                    y: -20,
                    opacity: 0.25,
                    duration: 0.70
                }, 0.25);

                heroTl.to('#heroMidRight', {
                    x: 45,
                    opacity: 0.25,
                    duration: 0.70
                }, 0.25);

                heroTl.to('#heroBottomLeft', {
                    y: 20,
                    opacity: 0,
                    duration: 0.35
                }, 0.15);

                heroTl.to('#heroBottomRight', {
                    y: 20,
                    opacity: 0.25,
                    duration: 0.60
                }, 0.30);
            }

            // --------------------------------------------------------------
            // SECTION 02: INGREDIENT-TO-DISH TRANSFORMATION
            // Choreography: 0-25% Converge -> 25-45% Compress & Thermal Core ->
            // 45-58% Searing Burst -> 58-75% Plate Emerges -> 70-100% Dish Resolves & Handoff
            // --------------------------------------------------------------
            var transformSection = document.getElementById('section-transformation');
            if (transformSection) {
                var transTl = gsap.timeline({
                    scrollTrigger: {
                        trigger: transformSection,
                        start: 'top top',
                        end: 'bottom bottom',
                        scrub: 0.8,
                        pin: true,
                        invalidateOnRefresh: true
                    },
                    defaults: { ease: 'power1.inOut' }
                });

                // Video continuation scrub if available
                if (video) {
                    var rawDur = (video.duration && !isNaN(video.duration)) ? video.duration : 10.0;
                    var transProxy = { time: rawDur * 0.45 };
                    transTl.to(transProxy, {
                        time: rawDur * 0.95,
                        duration: 1.0,
                        ease: 'none',
                        onUpdate: function () {
                            if (video.readyState >= 1 && isFinite(transProxy.time)) {
                                try {
                                    video.currentTime = Math.max(0, Math.min(transProxy.time, rawDur - 0.05));
                                } catch (e) {}
                            }
                        }
                    }, 0);
                }

                // Initial setup for focal transformation elements
                gsap.set('#plateWrapper', { scale: 0.65, opacity: 0, filter: 'blur(8px)' });
                gsap.set('#transformPlatedDish', { clipPath: 'circle(12% at 50% 50%)', scale: 0.88, opacity: 0, filter: 'brightness(1.4) blur(4px)' });
                gsap.set('#transformDishTag', { y: 25, opacity: 0 });
                gsap.set('#thermalCore', { scale: 0.3, opacity: 0 });
                gsap.set('#kineticBurst', { scale: 0.4, opacity: 0 });

                // 0–25%: Flanking copy and aromatics are visibly present upon section arrival (no blank black entrance);
                // Aromatics converge from surrounding space toward the focal center
                transTl.fromTo('#transformLeftText', { x: -30, opacity: 0.85 }, { x: 0, opacity: 1, duration: 0.25 }, 0.0);
                transTl.fromTo('#transformRightText', { x: 30, opacity: 0.85 }, { x: 0, opacity: 1, duration: 0.25 }, 0.0);

                transTl.fromTo('#convergeGarlic',
                    { x: 0, y: 0, scale: 1.15, opacity: 0.90 },
                    { x: 190, y: 160, scale: 1.0, opacity: 1, duration: 0.25 },
                    0.0
                );
                transTl.fromTo('#convergeChili',
                    { x: 0, y: 0, scale: 1.15, opacity: 0.90 },
                    { x: -190, y: 150, scale: 1.0, opacity: 1, duration: 0.25 },
                    0.0
                );
                transTl.fromTo('#convergeLemon',
                    { x: 0, y: 0, scale: 1.15, opacity: 0.90 },
                    { x: 150, y: -170, scale: 1.0, opacity: 1, duration: 0.25 },
                    0.0
                );

                // 25–45%: Cluster compresses tightly and slightly pulses; thermal core radiates heat
                transTl.to('#convergeGarlic', { x: 205, y: 175, scale: 0.86, filter: 'blur(1px)', duration: 0.20 }, 0.25);
                transTl.to('#convergeChili', { x: -205, y: 165, scale: 0.86, filter: 'blur(1px)', duration: 0.20 }, 0.25);
                transTl.to('#convergeLemon', { x: 165, y: -185, scale: 0.86, filter: 'blur(1px)', duration: 0.20 }, 0.25);
                transTl.to('.jt-converging-aromatics', { scale: 1.12, rotation: 8, transformOrigin: 'center center', duration: 0.20 }, 0.25);

                transTl.fromTo('#thermalCore',
                    { scale: 0.35, opacity: 0 },
                    { scale: 1.15, opacity: 0.85, duration: 0.20 },
                    0.25
                );

                // 45–58%: Brief cinematic heat/sizzle burst covers transition point; ingredients dissolve
                transTl.to('#thermalCore', { scale: 1.55, opacity: 1, filter: 'blur(32px)', duration: 0.13 }, 0.45);
                transTl.fromTo('#kineticBurst',
                    { scale: 0.5, opacity: 0 },
                    { scale: 1.25, opacity: 1, duration: 0.10 },
                    0.45
                );
                transTl.to(['#convergeGarlic', '#convergeChili', '#convergeLemon'],
                    { scale: 0.5, opacity: 0, filter: 'blur(8px)', duration: 0.10 },
                    0.46
                );
                transTl.to('.jt-converging-aromatics', { opacity: 0, duration: 0.08 }, 0.48);
                transTl.to('#kineticBurst', { scale: 1.45, opacity: 0.25, duration: 0.08 }, 0.55);

                // 58–75%: Plate emerges from behind the thermal burst from the SAME center
                transTl.to('#plateWrapper',
                    { scale: 1.0, opacity: 1, filter: 'blur(0px)', duration: 0.20 },
                    0.55
                );
                transTl.to('#thermalCore', { scale: 2.1, opacity: 0, duration: 0.16 }, 0.58);
                transTl.to('#kineticBurst', { opacity: 0, duration: 0.10 }, 0.58);

                // 70–88%: Plated Crispy Skin Herb Chicken crystallizes onto plate via reveal mask
                transTl.to('#transformPlatedDish',
                    { clipPath: 'circle(75% at 50% 50%)', scale: 1.0, opacity: 1, filter: 'brightness(1.0) blur(0px)', duration: 0.20 },
                    0.68
                );
                transTl.to('#transformDishTag', { y: 0, opacity: 1, duration: 0.14 }, 0.78);
                transTl.to(['#transformLeftText', '#transformRightText'], { opacity: 0.35, duration: 0.15 }, 0.80);

                // 88–100%: CONTINUOUS FLOW 02 -> 03 HANDOFF
                // Plate remains prominently visible (opacity >= 0.85) as light French linen canvas rises underneath
                transTl.to('#plateWrapper', { y: 30, scale: 0.96, opacity: 0.90, duration: 0.12 }, 0.88);
                transTl.to('#transformDishTag', { y: 10, opacity: 0.60, duration: 0.10 }, 0.90);
            }

            // --------------------------------------------------------------
            // SECTION 03: THE STORY OF FOOD (Light Editorial Parallax & Smooth Entrance)
            // --------------------------------------------------------------
            var storyCanvas = document.getElementById('section-story');
            if (storyCanvas) {
                // Section 02 -> 03 seamless upward reveal
                gsap.fromTo('.jt-story-container',
                    { y: 50, opacity: 0.9 },
                    {
                        y: 0,
                        opacity: 1,
                        ease: 'none',
                        scrollTrigger: {
                            trigger: storyCanvas,
                            start: 'top 95%',
                            end: 'top 60%',
                            scrub: 0.6
                        }
                    }
                );

                var storyCards = storyCanvas.querySelectorAll('.jt-story-card');
                if (storyCards.length >= 4) {
                    gsap.to(storyCards[0], {
                        y: 40,
                        ease: 'none',
                        scrollTrigger: { trigger: storyCanvas, start: 'top bottom', end: 'bottom top', scrub: 0.6 }
                    });
                    gsap.to(storyCards[1], {
                        y: -30,
                        ease: 'none',
                        scrollTrigger: { trigger: storyCanvas, start: 'top bottom', end: 'bottom top', scrub: 0.6 }
                    });
                    gsap.to(storyCards[2], {
                        y: 25,
                        ease: 'none',
                        scrollTrigger: { trigger: storyCanvas, start: 'top bottom', end: 'bottom top', scrub: 0.6 }
                    });
                    gsap.to(storyCards[3], {
                        y: -40,
                        ease: 'none',
                        scrollTrigger: { trigger: storyCanvas, start: 'top bottom', end: 'bottom top', scrub: 0.6 }
                    });
                }
            }

            // --------------------------------------------------------------
            // SECTION 04: RECIPE EDITORIAL COLLAGE & FOCAL ZOOM
            // Pinned stage: outer cards disperse radially, dark curtain establishes obsidian world,
            // central Wellington card expands to true viewport bounds (100vw x 100vh illusion)
            // Shortened pin (+=110%) ensures continuous visual momentum into Section 05.
            // --------------------------------------------------------------
            var collageSection = document.getElementById('section-collage');
            var focalCard = document.getElementById('mosaicCardFocal');
            if (collageSection && focalCard) {
                var collageTl = gsap.timeline({
                    scrollTrigger: {
                        trigger: collageSection,
                        start: 'top top',
                        end: '+=110%',
                        pin: true,
                        scrub: 1.0,
                        invalidateOnRefresh: true
                    },
                    defaults: { ease: 'power1.inOut' }
                });

                // 0.00 – 0.15: Stage at rest for initial viewing

                // 0.15 – 0.50: Outer cards disperse radially and scale down
                collageTl.to('#mosaicCardA', { x: -280, y: -160, scale: 0.72, opacity: 0, duration: 0.35 }, 0.15);
                collageTl.to('#mosaicCardB', { y: -240, scale: 0.72, opacity: 0, duration: 0.35 }, 0.15);
                collageTl.to('#mosaicCardC', { x: 280, y: -160, scale: 0.72, opacity: 0, duration: 0.35 }, 0.15);
                collageTl.to('#mosaicCardE', { x: -280, y: 220, scale: 0.72, opacity: 0, duration: 0.35 }, 0.18);
                collageTl.to('#mosaicCardF', { x: 280, y: 220, scale: 0.72, opacity: 0, duration: 0.35 }, 0.18);

                // Headline & surrounding copy fade backward
                collageTl.to('.jt-collage-header', { y: -60, opacity: 0, duration: 0.30 }, 0.15);

                // Focal Card text overlay dissolves, leaving pure culinary imagery
                collageTl.to('.jt-focal-content', { opacity: 0, y: 20, duration: 0.20 }, 0.15);

                // 0.20 – 0.90: Central Beef Wellington card expands toward full viewport bounds
                collageTl.to(focalCard, {
                    scale: 2.85,
                    borderRadius: 0,
                    borderWidth: 0,
                    borderColor: 'transparent',
                    boxShadow: '0 0 120px rgba(0,0,0,0.95)',
                    duration: 0.70
                }, 0.20);

                // 0.30 – 0.70: Dark curtain crossfades background to obsidian (#090b0e)
                collageTl.to('#collageDarkCurtain', {
                    opacity: 1,
                    duration: 0.40
                }, 0.30);

                // Adaptive header theme transition midway through dark curtain
                collageTl.to({}, {
                    duration: 0.01,
                    onStart: function () { if (headerEl) headerEl.setAttribute('data-theme', 'dark'); },
                    onReverseComplete: function () { if (headerEl) headerEl.setAttribute('data-theme', 'light'); }
                }, 0.50);

                // 0.85 – 1.00: Card reaches full viewport takeover, seamlessly establishing
                // dark state matching Section 05's center plate with zero visible gap.
            }

            // --------------------------------------------------------------
            // SECTION 05: DARK SIGNATURE DISH FEATURE (CONTINUOUS FLOW 05 -> 06 HANDOFF)
            // Starts seamlessly in obsidian darkness from Section 04's focal takeover;
            // Dish stays visibly dominant throughout (opacity >= 0.75) — NO BLANK SCREEN STATE.
            // --------------------------------------------------------------
            var signatureSection = document.getElementById('section-signature');
            if (signatureSection) {
                var sigTl = gsap.timeline({
                    scrollTrigger: {
                        trigger: signatureSection,
                        start: 'top top',
                        end: 'bottom bottom',
                        scrub: 0.8,
                        pin: true,
                        invalidateOnRefresh: true
                    },
                    defaults: { ease: 'power1.inOut' }
                });

                // 0.00 – 0.40: Dish settles into focus, halo glows, 3D text layers separate behind plate
                sigTl.fromTo('#sigDishCenter', { scale: 1.05 }, { scale: 1.0, duration: 0.35 }, 0.0);
                sigTl.fromTo('#sigHalo', { scale: 0.85, opacity: 0.4 }, { scale: 1.25, opacity: 1.0, duration: 0.35 }, 0.0);
                sigTl.fromTo('#sigTextTop', { y: 50 }, { y: -25, duration: 0.35 }, 0.0);
                sigTl.fromTo('#sigTextBottom', { y: -40 }, { y: 30, duration: 0.35 }, 0.0);

                // Specifications Reveal
                sigTl.fromTo('#sigSpecs', { y: 35, opacity: 0 }, { y: 0, opacity: 1, duration: 0.25 }, 0.15);

                // 0.75 – 1.00: CONTINUOUS FLOW 05 -> 06 HANDOFF
                // Signature dish shifts smoothly leftward, but REMAINS VISIBLE (opacity >= 0.75)
                // as the horizontal rail of Section 06 arrives. No blank black void!
                sigTl.to('#sigDishCenter', { x: -140, scale: 0.88, opacity: 0.75, duration: 0.25 }, 0.75);
                sigTl.to('#sigSpecs', { y: 20, opacity: 0.50, duration: 0.20 }, 0.75);
                sigTl.to(['#sigTextTop', '#sigTextBottom'], { opacity: 0.25, duration: 0.20 }, 0.75);
                sigTl.to('#sigHalo', { opacity: 0.40, duration: 0.25 }, 0.75);
            }

            // --------------------------------------------------------------
            // SECTION 06: HORIZONTAL SIGNATURE RECIPE COLLECTION RAIL
            // (Vertical scroll translates horizontal card rail)
            // --------------------------------------------------------------
            var horizontalSection = document.getElementById('section-horizontal');
            var horizontalTrack = document.getElementById('horizontalTrack');
            if (horizontalSection && horizontalTrack) {
                function getScrollDistance() {
                    var maxScroll = horizontalTrack.scrollWidth - window.innerWidth + 120;
                    return maxScroll > 0 ? -maxScroll : 0;
                }

                gsap.to(horizontalTrack, {
                    x: getScrollDistance,
                    ease: 'none',
                    scrollTrigger: {
                        trigger: horizontalSection,
                        start: 'top top',
                        end: function () {
                            var distance = Math.abs(getScrollDistance());
                            return '+=' + (distance > 0 ? distance : 0);
                        },
                        pin: true,
                        pinSpacing: true,
                        scrub: 0.8,
                        invalidateOnRefresh: true
                    }
                });
            }

            // --------------------------------------------------------------
            // SECTION 07: CULINARY TECHNIQUE / MASTERCLASS (3D Fan-In)
            // --------------------------------------------------------------
            var techSection = document.getElementById('section-masterclass');
            if (techSection) {
                var techTl = gsap.timeline({
                    scrollTrigger: {
                        trigger: techSection,
                        start: 'top 70%',
                        end: 'center center',
                        scrub: 0.8
                    },
                    defaults: { ease: 'power2.out' }
                });

                techTl.fromTo('#techCard1', { x: -80, opacity: 0, rotationY: 10 }, { x: 0, opacity: 1, rotationY: 0, duration: 0.8 }, 0);
                techTl.fromTo('#techCard2', { y: 60, opacity: 0 }, { y: 0, opacity: 1, duration: 0.8 }, 0.1);
                techTl.fromTo('#techCard3', { x: 80, opacity: 0, rotationY: -10 }, { x: 0, opacity: 1, rotationY: 0, duration: 0.8 }, 0.2);
            }

            // --------------------------------------------------------------
            // SECTION 08: CONTESTS & EXPERIENCES (3D Deck Fan-Out & Handoff 08 -> 09)
            // --------------------------------------------------------------
            var contestsSection = document.getElementById('section-contests');
            if (contestsSection) {
                var contestCards = contestsSection.querySelectorAll('.jt-contest-card');
                if (contestCards.length >= 3) {
                    var contestsTl = gsap.timeline({
                        scrollTrigger: {
                            trigger: contestsSection,
                            start: 'top 75%',
                            end: 'bottom 80%',
                            scrub: 0.8
                        },
                        defaults: { ease: 'power2.out' }
                    });

                    // 0.00 – 0.50: Fan-out deck
                    contestsTl.fromTo(contestCards[0], { rotation: -6, x: -30, opacity: 0.7 }, { rotation: 0, x: 0, opacity: 1, duration: 0.5 }, 0.0);
                    contestsTl.fromTo(contestCards[1], { y: 40, opacity: 0.7 }, { y: 0, opacity: 1, duration: 0.5 }, 0.1);
                    contestsTl.fromTo(contestCards[2], { rotation: 6, x: 30, opacity: 0.7 }, { rotation: 0, x: 0, opacity: 1, duration: 0.5 }, 0.2);

                    // 0.70 – 1.00: CONTINUOUS FLOW 08 -> 09 HANDOFF
                    // Contest cards stay visible (opacity >= 0.65) as ivory CTA canvas rises underneath
                    contestsTl.to(contestCards, {
                        scale: 0.94,
                        y: -30,
                        opacity: 0.65,
                        stagger: 0.05,
                        duration: 0.30
                    }, 0.70);
                }
            }

            // --------------------------------------------------------------
            // SECTION 09: FINAL EDITORIAL CTA (CONTINUOUS FLOW 08 -> 09 REVEAL)
            // --------------------------------------------------------------
            var ctaSection = document.getElementById('section-cta');
            var ctaVisual = document.getElementById('ctaVisualBlock');
            if (ctaSection) {
                // Ivory CTA canvas wipes/reveals upward overlapping Section 08 recession
                gsap.fromTo('.jt-cta-container',
                    { y: 60, opacity: 0.85 },
                    {
                        y: 0,
                        opacity: 1,
                        ease: 'none',
                        scrollTrigger: {
                            trigger: ctaSection,
                            start: 'top 95%',
                            end: 'top 50%',
                            scrub: 0.6
                        }
                    }
                );

                if (ctaVisual) {
                    gsap.fromTo(ctaVisual,
                        { x: 50, y: 30, opacity: 0.8 },
                        {
                            x: 0,
                            y: -20,
                            opacity: 1,
                            ease: 'none',
                            scrollTrigger: {
                                trigger: ctaSection,
                                start: 'top bottom',
                                end: 'bottom top',
                                scrub: 0.6
                            }
                        }
                    );
                }
            }

        }); // END DESKTOP

        // ==================================================================
        // TABLET & MOBILE BREAKPOINTS ( < 992px )
        // ==================================================================
        mm.add('(max-width: 991px)', function () {
            // Mobile and tablet safety: ensure full visibility without horizontal overflow or pin trapping
            gsap.set([
                '#heroTopLeft', '#heroMidRight', '#heroBottomLeft', '#heroBottomRight',
                '#floatTomato', '#floatBasil', '#transformLeftText', '#transformRightText',
                '#plateWrapper', '#transformPlatedDish', '#transformDishTag',
                '#sigTextTop', '#sigTextBottom', '#sigDishCenter', '#sigSpecs',
                '#horizontalTrack', '#techCard1', '#techCard2', '#techCard3',
                '#ctaVisualBlock', '#convergeGarlic', '#convergeChili', '#convergeLemon',
                '.jt-converging-aromatics', '#thermalCore', '#kineticBurst',
                '#collageDarkCurtain', '#mosaicCardFocal', '#mosaicCardA', '#mosaicCardB',
                '#mosaicCardC', '#mosaicCardE', '#mosaicCardF', '.jt-focal-content',
                '.jt-collage-header', '#sigHalo'
            ], {
                clearProps: 'transform,clipPath,filter'
            });
            // Ensure plates and dishes are visible on mobile
            gsap.set(['#plateWrapper', '#transformPlatedDish', '#transformDishTag', '#mosaicCardFocal', '#sigDishCenter', '#sigSpecs'], {
                opacity: 1
            });
        });

        // ==================================================================
        // PREFERS-REDUCED-MOTION (WCAG 2.2 AA)
        // ==================================================================
        mm.add('(prefers-reduced-motion: reduce)', function () {
            // Instantly clear any transforms, masks, filters and ensure full accessible visibility
            gsap.set([
                '#heroTopLeft', '#heroMidRight', '#heroBottomLeft', '#heroBottomRight',
                '#floatTomato', '#floatBasil', '#transformLeftText', '#transformRightText',
                '#plateWrapper', '#transformPlatedDish', '#transformDishTag',
                '#sigTextTop', '#sigTextBottom', '#sigDishCenter', '#sigSpecs',
                '#horizontalTrack', '#techCard1', '#techCard2', '#techCard3',
                '#ctaVisualBlock', '#convergeGarlic', '#convergeChili', '#convergeLemon',
                '.jt-converging-aromatics', '#thermalCore', '#kineticBurst',
                '#collageDarkCurtain', '#mosaicCardFocal', '#mosaicCardA', '#mosaicCardB',
                '#mosaicCardC', '#mosaicCardE', '#mosaicCardF', '.jt-focal-content',
                '.jt-collage-header', '#sigHalo'
            ], {
                clearProps: 'all'
            });
            gsap.set(['#plateWrapper', '#transformPlatedDish', '#transformDishTag', '#mosaicCardFocal', '#sigDishCenter', '#sigSpecs'], {
                opacity: 1
            });
        });

    } // END buildChoreography

});
