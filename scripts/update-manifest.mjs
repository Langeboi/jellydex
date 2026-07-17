import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import process from 'node:process';

const [version, repository] = process.argv.slice(2);
if (!version || !repository || !repository.includes('/')) {
    throw new Error('Usage: node scripts/update-manifest.mjs <version> <owner/repository>');
}

const pluginGuid = '7f32c2c7-7d82-4f15-ae2b-a77a3d4e92c1';
const dependencyGuid = '5e87cc92-571a-4d8d-8d98-d2d4147f9f90';
const timestamp = new Date().toISOString();
const variants = [
    { suffix: '10.10', targetAbi: '10.10.7.0' },
    { suffix: '10.11', targetAbi: '10.11.0.0' }
];

async function checksum(path) {
    const file = await readFile(path);
    return createHash('md5').update(file).digest('hex').toUpperCase();
}

let manifest;
try {
    manifest = JSON.parse(await readFile('manifest.json', 'utf8'));
} catch {
    manifest = [];
}

let plugin = manifest.find((entry) => entry.guid === pluginGuid);
if (!plugin) {
    plugin = { guid: pluginGuid, versions: [] };
    manifest.push(plugin);
}

Object.assign(plugin, {
    name: 'Kommer Snart',
    description: 'Regional digital-release calendar and next-episode schedule for Seerr requests inside Jellyfin Web.',
    overview: 'Seerr release calendar for Jellyfin',
    owner: repository.split('/')[0],
    category: 'General',
    imageUrl: 'https://raw.githubusercontent.com/' + repository + '/main/logo.svg'
});

plugin.versions = (plugin.versions || []).filter((entry) => entry.version !== version);
for (const variant of variants) {
    const filename = 'kommer-snart-' + version + '-jellyfin-' + variant.suffix + '.zip';
    plugin.versions.push({
        version,
        changelog: 'Automatisk testutgivelse fra GitHub Actions.',
        targetAbi: variant.targetAbi,
        sourceUrl: 'https://github.com/' + repository + '/releases/download/v' + version + '/' + filename,
        checksum: await checksum('artifacts/' + filename),
        timestamp,
        dependencies: [dependencyGuid]
    });
}

function compareVersion(left, right) {
    const a = left.version.split('.').map(Number);
    const b = right.version.split('.').map(Number);
    for (let index = 0; index < Math.max(a.length, b.length); index++) {
        const difference = (b[index] || 0) - (a[index] || 0);
        if (difference) return difference;
    }
    return right.targetAbi.localeCompare(left.targetAbi);
}

plugin.versions.sort(compareVersion);
await writeFile('manifest.json', JSON.stringify(manifest, null, 2) + '\n');
