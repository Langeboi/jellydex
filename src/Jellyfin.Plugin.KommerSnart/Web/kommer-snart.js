(function () {
    'use strict';

    if (window.kommerSnartPlugin) {
        return;
    }

    var state = {
        initialized: false,
        loading: false,
        loaded: false,
        filter: 'all',
        data: null
    };

    function isHomePage() {
        var hash = window.location.hash || '';
        return hash === '' || hash === '#/home' || hash === '#/home.html'
            || hash.indexOf('#/home?') === 0 || hash.indexOf('#/home.html?') === 0;
    }

    function createTabButton() {
        var slider = document.querySelector('.emby-tabs-slider');
        if (!slider) {
            return;
        }

        ensureTabContent();

        if (document.querySelector('#kommerSnartTabButton')) {
            reconcileCustomTabs(slider);
            return;
        }

        var favoriteButton = slider.querySelector('[data-index="1"]');
        var button = document.createElement('button');
        button.type = 'button';
        button.setAttribute('is', 'empty-button');
        button.setAttribute('data-index', '2');
        button.id = 'kommerSnartTabButton';
        button.className = 'emby-tab-button emby-button';

        var foreground = document.createElement('div');
        foreground.className = 'emby-button-foreground';
        foreground.textContent = 'Kommer Snart';
        button.appendChild(foreground);
        button.addEventListener('click', function () {
            window.setTimeout(loadCalendar, 50);
        });

        if (favoriteButton && favoriteButton.nextSibling) {
            slider.insertBefore(button, favoriteButton.nextSibling);
        } else {
            slider.appendChild(button);
        }

        reconcileCustomTabs(slider);
    }

    function ensureTabContent() {
        var existing = document.querySelector('#kommerSnartTab');
        if (existing) {
            if (!existing.querySelector('#kommerSnartRoot')) {
                existing.appendChild(createCalendarRoot());
            }
            return existing;
        }

        var favorites = document.querySelector('#favoritesTab');
        if (!favorites || !favorites.parentNode) {
            return null;
        }

        var content = document.createElement('div');
        content.className = 'tabContent pageTabContent';
        content.id = 'kommerSnartTab';
        content.setAttribute('data-index', '2');
        content.appendChild(createCalendarRoot());
        favorites.parentNode.insertBefore(content, favorites.nextSibling);
        return content;
    }

    function createCalendarRoot() {
        var root = document.createElement('div');
        root.id = 'kommerSnartRoot';
        root.className = 'sections kommerSnartRoot';
        var loading = document.createElement('div');
        loading.className = 'kommerSnartLoading';
        loading.textContent = 'Henter kommende udgivelser…';
        root.appendChild(loading);
        return root;
    }

    // Custom Tabs also starts at index 2. Reserve that slot for Kommer Snart and
    // move entries such as "Tilføj Film/Serie" one place to the right.
    function reconcileCustomTabs(slider) {
        var ownButton = document.querySelector('#kommerSnartTabButton');
        var ownContent = ensureTabContent();
        var favorites = document.querySelector('#favoritesTab');
        if (ownButton) {
            ownButton.setAttribute('data-index', '2');
        }
        if (ownContent) {
            ownContent.setAttribute('data-index', '2');
            if (favorites && favorites.parentNode && favorites.nextSibling !== ownContent) {
                favorites.parentNode.insertBefore(ownContent, favorites.nextSibling);
            }
        }

        Array.prototype.forEach.call(
            slider.querySelectorAll('[id^="customTabButton_"]'),
            function (button, index) {
                button.setAttribute('data-index', String(index + 3));
            }
        );
        Array.prototype.forEach.call(
            document.querySelectorAll('.pageTabContent[id^="customTab_"]'),
            function (content, index) {
                content.setAttribute('data-index', String(index + 3));
            }
        );
    }

    function initialize() {
        if (!isHomePage()) {
            return;
        }

        createTabButton();
        state.initialized = Boolean(document.querySelector('#kommerSnartTabButton'));
    }

    function loadCalendar() {
        var root = document.querySelector('#kommerSnartRoot');
        if (!root) {
            ensureTabContent();
            root = document.querySelector('#kommerSnartRoot');
        }
        if (!root || state.loading) {
            return;
        }

        if (state.loaded && state.data) {
            render(root);
            return;
        }

        state.loading = true;
        renderLoading(root);
        ApiClient.fetch({
            url: ApiClient.getUrl('KommerSnart/Calendar'),
            type: 'GET',
            dataType: 'json',
            headers: { accept: 'application/json' }
        }).then(function (data) {
            state.data = data;
            state.loaded = true;
            render(root);
        }).catch(function (error) {
            console.error('Kommer Snart could not load the calendar', error);
            renderError(root);
        }).finally(function () {
            state.loading = false;
        });
    }

    function renderLoading(root) {
        root.replaceChildren();
        var loading = document.createElement('div');
        loading.className = 'kommerSnartLoading';
        loading.textContent = 'Henter kommende udgivelser…';
        root.appendChild(loading);
    }

    function renderError(root) {
        root.replaceChildren();
        var panel = document.createElement('div');
        panel.className = 'kommerSnartEmpty';
        var title = document.createElement('h2');
        title.textContent = 'Kalenderen kunne ikke indlæses';
        var text = document.createElement('p');
        text.textContent = 'Kontrollér Seerr-indstillingerne i Jellyfin-administrationen.';
        panel.append(title, text);
        root.appendChild(panel);
    }

    function render(root) {
        root.replaceChildren();
        var items = (state.data && state.data.items ? state.data.items : []).filter(function (item) {
            return state.filter === 'all' || item.mediaType === state.filter;
        });

        root.appendChild(buildHero(items));
        root.appendChild(buildFilters());

        if (!items.length) {
            var empty = document.createElement('div');
            empty.className = 'kommerSnartEmpty';
            empty.textContent = 'Der er ingen anmodede udgivelser i denne visning.';
            root.appendChild(empty);
            return;
        }

        var groups = groupItems(items);
        groups.forEach(function (group) {
            var section = document.createElement('section');
            section.className = 'kommerSnartSection';
            var heading = document.createElement('h2');
            heading.className = 'kommerSnartSectionTitle';
            heading.textContent = group.label;
            var grid = document.createElement('div');
            grid.className = 'kommerSnartGrid';
            group.items.forEach(function (item) {
                grid.appendChild(buildCard(item));
            });
            section.append(heading, grid);
            root.appendChild(section);
        });
    }

    function buildHero(items) {
        var hero = document.createElement('header');
        hero.className = 'kommerSnartHero';
        var copy = document.createElement('div');
        var eyebrow = document.createElement('div');
        eyebrow.className = 'kommerSnartEyebrow';
        eyebrow.textContent = 'SEERR-KALENDER';
        var title = document.createElement('h1');
        title.textContent = 'Kommer Snart';
        var subtitle = document.createElement('p');
        subtitle.textContent = items.length + ' kommende og planlagte udgivelser i ' + (state.data.region || 'den valgte region');
        copy.append(eyebrow, title, subtitle);
        hero.appendChild(copy);
        return hero;
    }

    function buildFilters() {
        var filters = document.createElement('div');
        filters.className = 'kommerSnartFilters';
        [
            ['all', 'Alle'],
            ['movie', 'Film'],
            ['tv', 'Serier']
        ].forEach(function (entry) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'kommerSnartFilter' + (state.filter === entry[0] ? ' isActive' : '');
            button.textContent = entry[1];
            button.addEventListener('click', function () {
                state.filter = entry[0];
                render(document.querySelector('#kommerSnartRoot'));
            });
            filters.appendChild(button);
        });
        return filters;
    }

    function groupItems(items) {
        var groups = [];
        var byKey = new Map();
        items.forEach(function (item) {
            var key = item.releaseDate ? item.releaseDate.slice(0, 7) : 'unknown';
            if (!byKey.has(key)) {
                var label = key === 'unknown'
                    ? 'Dato ikke annonceret'
                    : new Intl.DateTimeFormat('da-DK', { month: 'long', year: 'numeric', timeZone: 'UTC' })
                        .format(new Date(key + '-01T00:00:00Z'));
                var group = { key: key, label: capitalize(label), items: [] };
                byKey.set(key, group);
                groups.push(group);
            }
            byKey.get(key).items.push(item);
        });
        return groups;
    }

    function buildCard(item) {
        var card = document.createElement('article');
        card.className = 'kommerSnartCard';

        var art = document.createElement('div');
        art.className = 'kommerSnartPoster';
        if (item.posterPath) {
            var image = document.createElement('img');
            image.loading = 'lazy';
            image.alt = '';
            image.src = 'https://image.tmdb.org/t/p/w342' + item.posterPath;
            art.appendChild(image);
        } else {
            var fallback = document.createElement('span');
            fallback.textContent = item.mediaType === 'movie' ? 'FILM' : 'SERIE';
            art.appendChild(fallback);
        }

        var dateBadge = document.createElement('div');
        dateBadge.className = 'kommerSnartDateBadge';
        dateBadge.textContent = item.releaseDate ? formatShortDate(item.releaseDate) : 'Ukendt dato';
        art.appendChild(dateBadge);

        var body = document.createElement('div');
        body.className = 'kommerSnartCardBody';
        var meta = document.createElement('div');
        meta.className = 'kommerSnartMeta';
        meta.textContent = item.mediaType === 'movie' ? 'DIGITAL UDGIVELSE' : 'NÆSTE EPISODE';
        var title = document.createElement('h3');
        title.textContent = item.title;
        var detail = document.createElement('p');
        detail.className = 'kommerSnartDetail';
        if (item.mediaType === 'tv' && item.seasonNumber && item.episodeNumber) {
            detail.textContent = 'S' + pad(item.seasonNumber) + 'E' + pad(item.episodeNumber)
                + (item.episodeTitle ? ' · ' + item.episodeTitle : '');
        } else if (item.releaseDate) {
            detail.textContent = formatLongDate(item.releaseDate);
        } else {
            detail.textContent = item.mediaType === 'movie'
                ? 'Den digitale dato er ikke annonceret for regionen'
                : 'Næste episode er ikke annonceret';
        }
        body.append(meta, title, detail);
        card.append(art, body);
        return card;
    }

    function formatShortDate(date) {
        return new Intl.DateTimeFormat('da-DK', {
            day: 'numeric',
            month: 'short',
            timeZone: 'UTC'
        }).format(new Date(date + 'T00:00:00Z'));
    }

    function formatLongDate(date) {
        return capitalize(new Intl.DateTimeFormat('da-DK', {
            weekday: 'long',
            day: 'numeric',
            month: 'long',
            year: 'numeric',
            timeZone: 'UTC'
        }).format(new Date(date + 'T00:00:00Z')));
    }

    function capitalize(value) {
        return value ? value.charAt(0).toUpperCase() + value.slice(1) : value;
    }

    function pad(value) {
        return String(value).padStart(2, '0');
    }

    function navigationChanged() {
        window.setTimeout(initialize, 500);
    }

    var observer = new MutationObserver(function () {
        if (isHomePage()) {
            initialize();
        }
    });

    function start() {
        observer.observe(document.body, { childList: true, subtree: true });
        initialize();
    }

    window.addEventListener('hashchange', navigationChanged);
    window.addEventListener('popstate', navigationChanged);
    document.addEventListener('viewshow', navigationChanged);
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) {
            navigationChanged();
        }
    });

    window.kommerSnartPlugin = {
        state: state,
        initialize: initialize,
        reload: function () {
            state.loaded = false;
            state.data = null;
            loadCalendar();
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
}());
