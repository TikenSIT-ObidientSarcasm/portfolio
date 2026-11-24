// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// Steampunk x Cyberpunk theme effects.
// Everything here is cosmetic: the site works the same with this file removed.

(function () {
    'use strict';

    var reduceMotion = window.matchMedia &&
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    document.addEventListener('DOMContentLoaded', function () {
        highlightActiveNav();
        setupHeaderShadow();

        if (reduceMotion) return;

        setupGlitchTitles();
        decodeLabels();
        setupReveal();
    });

    // Marks the nav link for the current page as active
    function highlightActiveNav() {
        var current = normalize(window.location.pathname);

        // The home page "/" is really /Project
        if (current === '') current = '/project';

        document.querySelectorAll('.portfolio-nav .nav-link[href]')
            .forEach(function (link) {
                var target = normalize(new URL(link.href, window.location.origin).pathname);

                if (current === target || current.indexOf(target + '/') === 0) {
                    link.classList.add('active');
                }
            });
    }

    // "/Project/Index/" -> "/project"
    function normalize(path) {
        return path.toLowerCase()
            .replace(/\/index\/?$/, '')
            .replace(/\/$/, '');
    }

    // Adds a shadow under the header once the page is scrolled
    function setupHeaderShadow() {
        var header = document.querySelector('.site-header');
        if (!header) return;

        function update() {
            header.classList.toggle('is-scrolled', window.scrollY > 10);
        }

        update();
        window.addEventListener('scroll', update, { passive: true });
    }

    // Gives big titles a quick glitch flicker (the CSS does the drawing)
    function setupGlitchTitles() {
        document.querySelectorAll('.hero h1, .details-header h1')
            .forEach(function (title) {
                title.setAttribute('data-text', title.textContent.trim());
                title.classList.add('glitch');
            });
    }

    // Small labels "decode" from random symbols into their real text
    function decodeLabels() {
        var glyphs = '█▓▒░<>/\\[]{}=+*#%&';

        document.querySelectorAll('.eyebrow, .section-label')
            .forEach(function (el) {
                var original = el.textContent.trim();
                if (!original || original.length > 60) return;

                var frames = 18;
                var frame = 0;

                var timer = setInterval(function () {
                    frame++;

                    var revealed = Math.floor(original.length * frame / frames);
                    var out = '';

                    for (var i = 0; i < original.length; i++) {
                        var ch = original.charAt(i);

                        out += (i < revealed || /\s/.test(ch))
                            ? ch
                            : glyphs.charAt(Math.floor(Math.random() * glyphs.length));
                    }

                    el.textContent = out;

                    if (frame >= frames) {
                        clearInterval(timer);
                        el.textContent = original;
                    }
                }, 40);
            });
    }

    // Cards slide in one after another as they scroll into view
    function setupReveal() {
        if (!('IntersectionObserver' in window)) return;

        var targets = document.querySelectorAll(
            '.project-card, .course-card, .image-card, .empty-projects');

        if (!targets.length) return;

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;

                var el = entry.target;
                el.classList.add('in-view');
                observer.unobserve(el);

                // Once the entrance is done, remove the helper classes so
                // normal hover effects respond instantly again.
                setTimeout(function () {
                    el.classList.remove('reveal', 'in-view');
                    el.style.transitionDelay = '';
                }, 1300);
            });
        }, { threshold: 0.08 });

        targets.forEach(function (el, index) {
            el.classList.add('reveal');
            el.style.transitionDelay = (Math.min(index, 8) * 70) + 'ms';
            observer.observe(el);
        });
    }
})();