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

    var adminToggle = document.getElementById('adminMenuToggle');
    var adminSidebar = document.getElementById('adminSidebar');

    if (adminToggle && adminSidebar) {
        adminToggle.addEventListener('click', function () {
            var isExpanded = adminToggle.getAttribute('aria-expanded') === 'true';
            adminToggle.setAttribute('aria-expanded', (!isExpanded).toString());
            adminSidebar.classList.toggle('show', !isExpanded);
            document.body.classList.toggle('jt-admin-nav-open', !isExpanded);
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && adminSidebar.classList.contains('show')) {
                adminSidebar.classList.remove('show');
                adminToggle.setAttribute('aria-expanded', 'false');
                document.body.classList.remove('jt-admin-nav-open');
                adminToggle.focus();
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
