/**
 * JamesThew.com — Unified Inner-Page Scripts & Card-Deck Motion System
 * Implements the card-animation.mp4 sequential stacked-card interaction for Recipes and Tips.
 */

document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    // 1. Check GSAP availability
    var hasGsap = typeof window.gsap !== 'undefined' && typeof window.ScrollTrigger !== 'undefined';
    if (!hasGsap) {
        return;
    }

    var gsap = window.gsap;
    var ScrollTrigger = window.ScrollTrigger;
    gsap.registerPlugin(ScrollTrigger);

    var mm = gsap.matchMedia();

    // =========================================================================
    // 2. RECIPE & COOKING TIPS STACKED CARD DECK (Desktop >= 992px)
    // Mirrors cards-animation.mp4: Current card front/center, upcoming cards stacked
    // behind to the right with ambient glow, vertical scroll advances cards sequentially.
    // =========================================================================
    mm.add('(min-width: 992px) and (prefers-reduced-motion: no-preference)', function () {
        var deckContainers = document.querySelectorAll('.jt-deck-showcase-section');

        deckContainers.forEach(function (deckSection) {
            var cards = deckSection.querySelectorAll('.jt-deck-card');
            var glowEl = deckSection.querySelector('.jt-deck-ambient-glow');
            var counterEl = deckSection.querySelector('.jt-counter-index');
            var barFill = deckSection.querySelector('.jt-counter-bar-fill');
            var totalCards = cards.length;

            if (totalCards <= 1) {
                // If only 1 card or empty, keep it statically centered
                if (cards.length === 1) {
                    gsap.set(cards[0], { x: 0, y: 0, scale: 1, opacity: 1, zIndex: 10 });
                }
                return;
            }

            // Initial 3D Stacking Placement
            cards.forEach(function (card, idx) {
                if (idx === 0) {
                    gsap.set(card, { x: 0, y: 0, scale: 1.0, opacity: 1.0, zIndex: 10, rotation: 0 });
                } else if (idx === 1) {
                    gsap.set(card, { x: 45, y: -18, scale: 0.93, opacity: 0.65, zIndex: 9, rotation: 1.5 });
                } else if (idx === 2) {
                    gsap.set(card, { x: 90, y: -36, scale: 0.86, opacity: 0.35, zIndex: 8, rotation: 3 });
                } else {
                    gsap.set(card, { x: 135, y: -54, scale: 0.78, opacity: 0, zIndex: 7, rotation: 4.5 });
                }
            });

            // Ambient Glow Palette for sequential card color shift
            var glowPalettes = [
                'radial-gradient(circle, rgba(243, 198, 105, 0.40) 0%, rgba(217, 119, 54, 0.18) 50%, transparent 72%)', // Amber
                'radial-gradient(circle, rgba(194, 65, 46, 0.38) 0%, rgba(217, 119, 54, 0.18) 50%, transparent 72%)',  // Sear Ember
                'radial-gradient(circle, rgba(46, 125, 50, 0.35) 0%, rgba(243, 198, 105, 0.15) 50%, transparent 72%)',  // Fresh Herb
                'radial-gradient(circle, rgba(200, 152, 71, 0.42) 0%, rgba(139, 69, 19, 0.20) 50%, transparent 72%)',  // Roast Saffron
                'radial-gradient(circle, rgba(54, 119, 217, 0.38) 0%, rgba(18, 22, 28, 0.25) 50%, transparent 72%)'    // Oceanic Seafood
            ];

            // Build sequential pinned timeline
            var scrollDistancePerCard = 320; // 320px scroll per card transition
            var totalScrollDistance = (totalCards - 1) * scrollDistancePerCard;

            var deckTl = gsap.timeline({
                scrollTrigger: {
                    trigger: deckSection,
                    start: 'top top+=90',
                    end: '+=' + totalScrollDistance,
                    pin: true,
                    scrub: 0.8,
                    invalidateOnRefresh: true,
                    onUpdate: function (self) {
                        var progress = self.progress;
                        var currentIdx = Math.min(totalCards - 1, Math.floor(progress * totalCards));
                        if (counterEl) {
                            counterEl.textContent = String(currentIdx + 1).padStart(2, '0');
                        }
                        if (barFill) {
                            barFill.style.width = (progress * 100) + '%';
                        }
                    }
                },
                defaults: { ease: 'power1.inOut' }
            });

            // Add transitions for card 0 -> 1 -> 2 -> ... -> N-1
            var segmentDuration = 1.0;
            for (var i = 0; i < totalCards - 1; i++) {
                var currentCard = cards[i];
                var nextCard = cards[i + 1];
                var thirdCard = i + 2 < totalCards ? cards[i + 2] : null;
                var fourthCard = i + 3 < totalCards ? cards[i + 3] : null;
                var startTime = i * segmentDuration;

                // 1. Current card exits left and slightly recedes (mirrors reference)
                deckTl.to(currentCard, {
                    x: -160,
                    scale: 0.84,
                    rotation: -5,
                    opacity: 0,
                    duration: segmentDuration
                }, startTime);

                // 2. Next card moves from background stack to front & center
                deckTl.to(nextCard, {
                    x: 0,
                    y: 0,
                    scale: 1.0,
                    rotation: 0,
                    opacity: 1.0,
                    zIndex: 10 + i + 1,
                    duration: segmentDuration
                }, startTime);

                // 3. Third card advances into the secondary slot
                if (thirdCard) {
                    deckTl.to(thirdCard, {
                        x: 45,
                        y: -18,
                        scale: 0.93,
                        rotation: 1.5,
                        opacity: 0.65,
                        zIndex: 9 + i,
                        duration: segmentDuration
                    }, startTime);
                }

                // 4. Fourth card becomes partially visible in tertiary slot
                if (fourthCard) {
                    deckTl.to(fourthCard, {
                        x: 90,
                        y: -36,
                        scale: 0.86,
                        rotation: 3,
                        opacity: 0.35,
                        zIndex: 8 + i,
                        duration: segmentDuration
                    }, startTime);
                }

                // 5. Update ambient glow color
                if (glowEl) {
                    var paletteIdx = (i + 1) % glowPalettes.length;
                    deckTl.to(glowEl, {
                        background: glowPalettes[paletteIdx],
                        duration: segmentDuration * 0.6
                    }, startTime + 0.2);
                }
            }
        });
    });

    // =========================================================================
    // 3. MOBILE & TABLET (< 992px): Reset all transforms
    // =========================================================================
    mm.add('(max-width: 991px)', function () {
        gsap.set('.jt-deck-card', {
            clearProps: 'transform,opacity,zIndex'
        });
    });

    // =========================================================================
    // 4. GENERAL SUBTLE SCROLL REVEALS FOR INNER PAGES
    // =========================================================================
    var pageHero = document.querySelector('.jt-page-hero');
    if (pageHero) {
        gsap.from(pageHero, {
            y: 25,
            opacity: 0,
            duration: 0.7,
            ease: 'power2.out'
        });
    }

    var contentCards = document.querySelectorAll('.jt-content-card, .jt-pricing-card');
    if (contentCards.length > 0) {
        gsap.from(contentCards, {
            y: 30,
            opacity: 0,
            duration: 0.6,
            stagger: 0.08,
            ease: 'power2.out',
            scrollTrigger: {
                trigger: contentCards[0],
                start: 'top 88%'
            }
        });
    }
});

// =========================================================================
// PHASE 7C — PREMIUM CARD INTERACTION POLISH
// Subtle, cinematic pointer-driven interactions for content & utility cards.
// =========================================================================
(function () {
    'use strict';

    function initCardInteractions() {
        var contentCards = document.querySelectorAll('.jt-card-interactive-content');
        var utilityCards = document.querySelectorAll('.jt-card-interactive-utility');
        var flipCards = document.querySelectorAll('.jt-card-flip-wrap');

        var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
        var isPointerCoarse = window.matchMedia('(hover: none), (pointer: coarse)');

        function setupPointerTracking(card, isContentCard) {
            var rafId = null;
            var maxTilt = 5.0; // 5 degrees max tilt
            var maxLift = -8.0; // -8px max lift

            function onPointerMove(e) {
                if (prefersReducedMotion.matches || isPointerCoarse.matches) {
                    return;
                }

                if (rafId) {
                    cancelAnimationFrame(rafId);
                }

                rafId = requestAnimationFrame(function () {
                    var rect = card.getBoundingClientRect();
                    var x = e.clientX - rect.left;
                    var y = e.clientY - rect.top;

                    card.style.setProperty('--mouse-x', x.toFixed(1) + 'px');
                    card.style.setProperty('--mouse-y', y.toFixed(1) + 'px');

                    if (isContentCard) {
                        var nx = (x / rect.width) * 2 - 1; // -1 to +1
                        var ny = (y / rect.height) * 2 - 1; // -1 to +1

                        var tiltX = (-ny * maxTilt).toFixed(2);
                        var tiltY = (nx * maxTilt).toFixed(2);

                        card.style.setProperty('--tilt-x', tiltX + 'deg');
                        card.style.setProperty('--tilt-y', tiltY + 'deg');
                        card.style.setProperty('--lift-y', maxLift + 'px');
                    }
                });
            }

            function onPointerLeave() {
                if (rafId) {
                    cancelAnimationFrame(rafId);
                }

                card.style.setProperty('--tilt-x', '0deg');
                card.style.setProperty('--tilt-y', '0deg');
                card.style.setProperty('--lift-y', '0px');
            }

            card.addEventListener('pointermove', onPointerMove, { passive: true });
            card.addEventListener('pointerleave', onPointerLeave, { passive: true });
        }

        contentCards.forEach(function (card) {
            setupPointerTracking(card, true);
        });

        utilityCards.forEach(function (card) {
            setupPointerTracking(card, false);
        });

        // Flip Card Toggle Handlers
        flipCards.forEach(function (card) {
            var toggleBtns = card.querySelectorAll('.jt-flip-toggle-btn');
            var closeBtns = card.querySelectorAll('.jt-flip-close-btn');

            toggleBtns.forEach(function (btn) {
                btn.addEventListener('click', function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    card.classList.toggle('is-flipped');
                });
            });

            closeBtns.forEach(function (btn) {
                btn.addEventListener('click', function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    card.classList.remove('is-flipped');
                    // Return focus to toggle button
                    var toggle = card.querySelector('.jt-flip-toggle-btn');
                    if (toggle) {
                        toggle.focus();
                    }
                });
            });

            // Smooth reverse on pointer leave
            card.addEventListener('pointerleave', function () {
                if (card.classList.contains('is-flipped')) {
                    card.classList.remove('is-flipped');
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCardInteractions);
    } else {
        initCardInteractions();
    }
})();

// =========================================================================
// PHASE 8A — EMAIL OTP + DEMO PAYMENT CHECKOUT INPUTS
// =========================================================================
(function () {
    'use strict';

    function digitsOnly(value) {
        return (value || '').replace(/\D/g, '');
    }

    function initOtpForms() {
        document.querySelectorAll('.jt-otp-form').forEach(function (form) {
            var boxes = Array.prototype.slice.call(form.querySelectorAll('.jt-otp-box'));
            var hidden = form.querySelector('.jt-otp-hidden');
            var length = Number(form.getAttribute('data-otp-length') || boxes.length || 0);
            if (!boxes.length || !hidden || !length) {
                return;
            }

            function syncHidden() {
                hidden.value = boxes.map(function (box) { return box.value; }).join('').slice(0, length);
            }

            function fillFromText(text, startIndex) {
                var chars = digitsOnly(text).slice(0, length - startIndex).split('');
                chars.forEach(function (char, offset) {
                    boxes[startIndex + offset].value = char;
                });
                syncHidden();
                var focusIndex = Math.min(startIndex + chars.length, boxes.length - 1);
                boxes[focusIndex].focus();
            }

            if (hidden.value) {
                fillFromText(hidden.value, 0);
            }

            boxes.forEach(function (box, index) {
                box.addEventListener('input', function () {
                    var value = digitsOnly(box.value);
                    if (value.length > 1) {
                        fillFromText(value, index);
                        return;
                    }
                    box.value = value;
                    syncHidden();
                    if (value && index < boxes.length - 1) {
                        boxes[index + 1].focus();
                    }
                });

                box.addEventListener('keydown', function (event) {
                    if (event.key === 'Backspace' && !box.value && index > 0) {
                        boxes[index - 1].focus();
                        boxes[index - 1].value = '';
                        syncHidden();
                    }
                    if (event.key === 'ArrowLeft' && index > 0) {
                        event.preventDefault();
                        boxes[index - 1].focus();
                    }
                    if (event.key === 'ArrowRight' && index < boxes.length - 1) {
                        event.preventDefault();
                        boxes[index + 1].focus();
                    }
                });

                box.addEventListener('paste', function (event) {
                    event.preventDefault();
                    fillFromText(event.clipboardData.getData('text'), index);
                });
            });
        });
    }

    function initResendTimers() {
        document.querySelectorAll('.jt-resend-form').forEach(function (form) {
            var button = form.querySelector('.jt-resend-button');
            var label = form.querySelector('.jt-resend-countdown');
            var remaining = Number(form.getAttribute('data-cooldown') || 0);
            if (!button || !label || remaining <= 0) {
                return;
            }

            function tick() {
                if (remaining <= 0) {
                    button.disabled = false;
                    label.textContent = '';
                    return;
                }
                button.disabled = true;
                label.textContent = '(' + remaining + 's)';
                remaining -= 1;
                window.setTimeout(tick, 1000);
            }

            tick();
        });
    }

    function detectCardNetwork(digits) {
        if (/^4\d{12,18}$/.test(digits)) {
            return 'Visa';
        }
        var prefix2 = Number(digits.slice(0, 2));
        var prefix4 = Number(digits.slice(0, 4));
        if (digits.length >= 2 && prefix2 >= 51 && prefix2 <= 55) {
            return 'Mastercard';
        }
        if (digits.length >= 4 && prefix4 >= 2221 && prefix4 <= 2720) {
            return 'Mastercard';
        }
        return 'Unknown Card';
    }

    function initDemoCardForm() {
        var cardInput = document.querySelector('.jt-card-number-input');
        var expiryInput = document.querySelector('.jt-expiry-input');
        var cvvInput = document.querySelector('.jt-cvv-input');
        var networkLabel = document.querySelector('.jt-card-network span');

        if (cardInput) {
            cardInput.addEventListener('input', function () {
                var digits = digitsOnly(cardInput.value).slice(0, 19);
                cardInput.value = digits.replace(/(\d{4})(?=\d)/g, '$1 ').trim();
                if (networkLabel) {
                    networkLabel.textContent = detectCardNetwork(digits);
                }
            });
        }

        if (expiryInput) {
            expiryInput.addEventListener('input', function () {
                var digits = digitsOnly(expiryInput.value).slice(0, 4);
                expiryInput.value = digits.length > 2 ? digits.slice(0, 2) + '/' + digits.slice(2) : digits;
            });
        }

        if (cvvInput) {
            cvvInput.addEventListener('input', function () {
                cvvInput.value = digitsOnly(cvvInput.value).slice(0, 3);
            });
        }
    }

    function initPhase8AInputs() {
        initOtpForms();
        initResendTimers();
        initDemoCardForm();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initPhase8AInputs);
    } else {
        initPhase8AInputs();
    }
})();
