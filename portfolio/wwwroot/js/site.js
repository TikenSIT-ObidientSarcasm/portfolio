// Steampunk x Cyberpunk theme effects.
// Everything here is cosmetic: the site works the same with this file removed.

(function () {
    'use strict';

    var reduceMotion = window.matchMedia &&
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    document.addEventListener('DOMContentLoaded', function () {
        highlightActiveNav();
        setupHeaderShadow();
        setupGalleryHud();

        // Cards are hidden right away (so they don't flash behind the
        // loading screen) and slide in once the loading screen is gone.
        var startReveal = reduceMotion ? null : prepareReveal();

        runBootScreen(function () {
            if (reduceMotion) return;

            setupGlitchTitles();
            decodeLabels();
            if (startReveal) startReveal();
            countUpStats();
        });
    });

    // ---------------------------------------------------------
    // Loading screen: shown once per visit (per browser tab)
    // ---------------------------------------------------------
    function runBootScreen(done) {
        var boot = document.querySelector('.boot-screen');

        if (!boot || reduceMotion ||
            document.documentElement.classList.contains('no-boot')) {
            if (boot) boot.remove();
            done();
            return;
        }

        var started = Date.now();
        var minimum = 1700; // milliseconds the screen stays up

        function finish() {
            var wait = Math.max(0, minimum - (Date.now() - started));

            setTimeout(function () {
                boot.classList.add('is-done');

                try { sessionStorage.setItem('booted', '1'); } catch (e) { }

                setTimeout(function () { boot.remove(); }, 700);
                done();
            }, wait);
        }

        if (document.readyState === 'complete') {
            finish();
        } else {
            window.addEventListener('load', finish);
        }
    }

    // ---------------------------------------------------------
    // Navigation
    // ---------------------------------------------------------

    // Marks the nav link for the current page as active
    function highlightActiveNav() {
        var current = normalize(window.location.pathname);

        document.querySelectorAll('.portfolio-nav .nav-link[href]')
            .forEach(function (link) {
                var target = normalize(new URL(link.href, window.location.origin).pathname);

                if (current === target || current.indexOf(target + '/') === 0) {
                    link.classList.add('active');
                }
            });
    }

    // "/Project/Index/" -> "/project"   and   "/" -> "/home"
    function normalize(path) {
        var clean = path.toLowerCase()
            .replace(/\/index\/?$/, '')
            .replace(/\/$/, '');

        // The site root is the Home page
        return clean === '' ? '/home' : clean;
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

    // ---------------------------------------------------------
    // Project popup: "IMG 01 / 03" counter on the slideshow
    // ---------------------------------------------------------
    function setupGalleryHud() {
        document.querySelectorAll('.project-modal-gallery')
            .forEach(function (gallery) {
                var items = gallery.querySelectorAll('.carousel-item');
                var total = items.length || 1;

                var badge = document.createElement('div');
                badge.className = 'hud-counter';
                gallery.appendChild(badge);

                function pad(n) { return (n < 10 ? '0' : '') + n; }

                function update() {
                    var index = 0;

                    items.forEach(function (item, i) {
                        if (item.classList.contains('active')) index = i;
                    });

                    badge.textContent = 'IMG ' + pad(index + 1) + ' / ' + pad(total);
                }

                update();

                // Bootstrap fires this event after each slide change
                gallery.addEventListener('slid.bs.carousel', update);
            });
    }

    // ---------------------------------------------------------
    // Effects (skipped when the visitor prefers reduced motion)
    // ---------------------------------------------------------

    // Home page numbers count up from 0
    function countUpStats() {
        document.querySelectorAll('.stat-number[data-count]')
            .forEach(function (el) {
                var target = parseInt(el.getAttribute('data-count'), 10);
                if (isNaN(target) || target <= 0) return;

                var duration = 900;
                var begin = null;

                el.textContent = '0';

                function tick(now) {
                    if (begin === null) begin = now;

                    var progress = Math.min((now - begin) / duration, 1);
                    var eased = 1 - Math.pow(1 - progress, 3);

                    el.textContent = Math.round(target * eased);

                    if (progress < 1) requestAnimationFrame(tick);
                }

                requestAnimationFrame(tick);
            });
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

    // Cards slide in one after another as they scroll into view.
    // Returns a function that starts the animation.
    function prepareReveal() {
        var targets = document.querySelectorAll(
            '.project-card, .course-card, .image-card, .empty-projects, .stat-panel');

        if (!targets.length || !('IntersectionObserver' in window)) return null;

        var started = false;

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
            el.style.transitionDelay = (Math.min(index, 8) * 60) + 'ms';
        });

        function start() {
            if (started) return;
            started = true;
            targets.forEach(function (el) { observer.observe(el); });
        }

        // Safety net so cards can never stay hidden
        setTimeout(start, 5000);

        return start;
    }
})();