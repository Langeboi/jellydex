# Kommer Snart

Kommer Snart adds a release-calendar tab to Jellyfin Web for movies and series requested through Seerr. Movies use the earliest TMDB digital-release date (release type 4) for the configured country; series use the next announced episode.

The interface follows Jellyfin's active CSS variables and has explicit support for the ElegantFin dark-grey palette, motion settings, and hover-glare variables.

## Requirements

- Jellyfin 10.10.x or 10.11.x
- A reachable Seerr instance and API key
- [File Transformation](https://www.iamparadox.dev/jellyfin/plugins/manifest.json), which inserts the tab into Jellyfin Web

## Install a test build

1. Install File Transformation from its plugin catalog and restart Jellyfin.
2. Download the ZIP matching your Jellyfin version from this repository's releases.
3. Extract `Jellyfin.Plugin.KommerSnart.dll` into a `Kommer Snart` directory below Jellyfin's plugin directory.
4. Restart Jellyfin.
5. Open **Dashboard → Plugins → Kommer Snart**, enter the Seerr URL and API key, select a two-letter region, and test the connection.

The Seerr API key stays server-side. Calendar endpoints require an authenticated Jellyfin session, and configuration/test endpoints require administrator elevation.

## Build and test

The two supported targets are built independently:

```bash
dotnet test --configuration Release -p:JellyfinVersion=10.10.7
dotnet test --configuration Release -p:JellyfinVersion=10.11.0

./scripts/build-release.sh 0.1.0.0 10.10.7 10.10
./scripts/build-release.sh 0.1.0.0 10.11.0 10.11
```

Release archives are written to `artifacts/`. GitHub Actions publishes both variants as a prerelease and updates `manifest.json` for use as a Jellyfin plugin catalog.

## Date behavior

- Movies show only TMDB digital dates for the configured region. Cinema dates are intentionally excluded.
- TMDB does not reliably distinguish subscription streaming from digital rental or purchase.
- Series show `nextEpisodeToAir` when Seerr provides it.
- Items with no announced date remain visible under **Dato ikke annonsert**.
