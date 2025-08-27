
# ChemLocalLink
[![GitHub all releases](https://img.shields.io/github/downloads/Chemotion/ChemLocalLink/total)](https://github.com/Chemotion/ChemLocalLink/releases/latest)
[![License](https://img.shields.io/badge/License-MIT%202.0-blue.svg)](https://opensource.org/licenses/MIT)
[![Maintenance](https://img.shields.io/badge/Maintained%3F-yes-blue.svg)](https://github.com/Chemotion/ChemLocalLink/graphs/commit-activity)
[![GitHub issues](https://img.shields.io/github/issues/Chemotion/ChemLocalLink.svg)](https://github.com/Chemotion/ChemLocalLink/issues)
[![Continuous Integration](https://github.com/Chemotion/ChemLocalLink/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Chemotion/ChemLocalLink/actions/workflows/dotnet.yml)
[![Latest Release](https://github.com/Chemotion/ChemLocalLink/actions/workflows/dotnet_innosetup.yml/badge.svg)](https://github.com/Chemotion/ChemLocalLink/actions/workflows/dotnet_innosetup.yml)

ChemLocalLink is a cross-platform application designed to manage and process Chemotion Files.

## Key Features
- **URL Handling**: Handles chemotion-specific URLs to react with Chemotion.
- **Structured Download Storage**: Persistent per‑origin folder layout: `<DownloadRoot>/<origin-host>/<optional/deep/link/path>/file.ext`.
- **Download & Edit Tracking**: Checksums detect local edits; edited items flagged automatically (periodic scan).
- **Folder Scan / Link External Files**: Scan action links any new, untracked files placed manually inside the download tree.
- **Upload Workflow (Keep or Delete)**: Upload edited files then either mark them as kept (retain on disk) or delete them (with recursive cleanup of now-empty folders).
- **Session Export / Import**: Portable `.chemlocallink` archive bundles transfer to another machine.
- **Desktop Notifications**: Progress + status notifications (download / upload / errors).
- **History & State Persistence**: `downloads.json` stored under application data, re‑hydrated on startup.

## Directory & Data Layout
Application data base path: (OS ApplicationData)/`ChemLocalLink`

Files created:
- `downloads.json` – persisted list of tracked downloads (including created / duplicated / scanned files).
- `config.json` – app config (currently only the resolved or overridden `DownloadDirectory`).
- `theme.json` (handled internally through `JsonDataService`).

Default persistent download directory:
- Windows: `%USERPROFILE%/Documents/ChemLocalLink`
- macOS: `~/Documents/ChemLocalLink`
- Linux: `~/Documents/ChemLocalLink` if it exists, else `~/ChemLocalLink`

Structure after processing a deep link (example):
```
ChemLocalLink/
  example.chemotion.net/
    project/123/reactions/
      reaction_456.json
```

## Session Export / Import
Archive extension: `.chemlocallink`

Contents:
```
manifest.json         // schemaVersion, appVersion, exportedAtUtc, fileCount
downloads.json        // portable metadata (relative file names)
files/                // actual files, preserving subfolder structure
```
Import rules:
- Skips entries whose checksum already exists.
- Recreates needed subdirectories relative to current download root.
- Adds non‑duplicate files to the top of history and rebuilds groups.

## Typical Workflow
1. User clicks `chemotion://...` link (or pastes URL) in the app.
2. App resolves token + deep link path, downloads file to structured directory.
3. User edits file externally (double‑click to open).
4. App detects modification (checksum delta → IsEdited = true).
5. User uploads (single or bulk)
6. Optionally export session for transfer or backup.

## Contributing
1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit: `git commit -m "feat: add your feature"`
4. Push: `git push origin feature/your-feature`
5. Open a Pull Request

Please open issues for bugs, feature requests, or clarifications.

## License
MIT License – see `LICENSE` for full text.

## Contact
- Mostafa Mekky – [mekky@kit.edu](mailto:mekky@kit.edu)
- Issues: https://github.com/Chemotion/ChemLocalLink/issues

## Related
- Chemotion: https://chemotion.net/
