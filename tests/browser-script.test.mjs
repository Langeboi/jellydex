import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

async function loadPlugin() {
    const scriptUrl = new URL(
        '../src/Jellyfin.Plugin.KommerSnart/Web/kommer-snart.js',
        import.meta.url
    );
    const script = await readFile(scriptUrl, 'utf8');
    const context = {
        console,
        document: {
            readyState: 'loading',
            addEventListener() {},
            querySelector() { return null; }
        },
        MutationObserver: class {
            observe() {}
        },
        window: {
            addEventListener() {},
            location: { hash: '#/not-home' },
            setTimeout
        }
    };

    vm.runInNewContext(script, context);
    return context.window.kommerSnartPlugin;
}

test('normalizes Jellyfin PascalCase calendar responses', async () => {
    const plugin = await loadPlugin();
    const normalized = plugin.normalizeResponse({
        Region: 'DK',
        RequestCount: 1,
        Warnings: ['warning'],
        Items: [{
            TmdbId: 42,
            MediaType: 'movie',
            Title: 'Testfilm',
            PosterPath: '/poster.jpg',
            ReleaseDate: '2026-08-01',
            RequestStatus: 5,
            MediaStatus: 2
        }]
    });

    assert.equal(normalized.region, 'DK');
    assert.equal(normalized.requestCount, 1);
    assert.equal(normalized.warnings[0], 'warning');
    assert.equal(normalized.items.length, 1);
    assert.equal(normalized.items[0].tmdbId, 42);
    assert.equal(normalized.items[0].mediaType, 'movie');
    assert.equal(normalized.items[0].title, 'Testfilm');
    assert.equal(normalized.items[0].releaseDate, '2026-08-01');
    assert.equal(normalized.items[0].requestStatus, 5);
});
