# Changelog

## v1.3.0 — First public release

The first public release retains the existing application's 1.3.0 version.

### Added

- Native Windows desktop interface with dark surfaces, purple accents, and a custom MultiMP3 logo.
- Secondary “A ChudGPT App” identity, metadata-derived version labels, About page and verified ecosystem link, with charcoal/lime tokens and compact-window spacing.
- Batch queuing of supported YouTube videos, YouTube Music albums, and public playlists.
- Album and playlist folders, automatic track-list expansion, and drag-to-reorder queue items.
- MP3 output at 128, 192, 256, or 320 kbps, plus high-quality variable bitrate.
- Background downloads with configurable concurrency, retries, pause, and cancellation.
- Per-item progress, independent failure handling, retry actions, and expandable error logs.
- Persistent settings, queue, albums, completed-file history, and library backup recovery.
- Duplicate prompts, Windows-safe filenames, and unique filenames that preserve existing files.
- Optional title, artist, album metadata, and cover art for converted audio.
- Amazon Music mode for importing already-downloaded purchased MP3s without modifying originals.
- Source-mode filtering and exact-copy local imports organized by embedded album tags.
- MIT licensing, public documentation, sample screenshots, issue templates, and release packaging.

### Release notes

- Windows x64 portable build includes .NET; media tools install separately through `Setup-Engine.cmd`.
- Amazon streaming links, DRM-protected media, authenticated/private media, radio mixes, and live broadcasts are unsupported.
- The application is not code-signed. Internet-dependent extraction may require upstream tool updates.
