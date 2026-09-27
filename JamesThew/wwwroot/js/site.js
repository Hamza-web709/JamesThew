// JamesThew.com - Progressive Client Scripts
document.addEventListener('DOMContentLoaded', function () {
    // Accessible Mobile Navigation Toggle
    var navToggle = document.getElementById('navbarToggleBtn');
    var navCollapse = document.getElementById('navbarSupportedContent');

    if (navToggle && navCollapse) {
        navToggle.addEventListener('click', function () {
            var isExpanded = navToggle.getAttribute('aria-expanded') === 'true';
            navToggle.setAttribute('aria-expanded', !isExpanded);
            navCollapse.classList.toggle('show');
        });

        // Close on Escape key press
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && navCollapse.classList.contains('show')) {
                navCollapse.classList.remove('show');
                navToggle.setAttribute('aria-expanded', 'false');
                navToggle.focus();
            }
        });
    }

    // Auto-open FAQ details when linked via URL hash (e.g. #faq-membership)
    if (window.location.hash) {
        var targetElement = document.querySelector(window.location.hash);
        if (targetElement && targetElement.tagName === 'DETAILS') {
            targetElement.open = true;
            targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }
});
