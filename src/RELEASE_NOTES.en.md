# Change log

[简体中文](RELEASE_NOTES.md) | **English**

## Documentation navigation update

Public guides and the GitHub home page now have separate Chinese and English editions, with language links at the top. This changes documentation and future packaging inputs only; it does not change application behavior or replace the published v1.05.0 binary.

## v1.05.0 — 2026-10-22

This Windows x64 release consolidates main changes since v1.00.0 and includes a SHA-256 checksum.

### Installation and compatibility

Extract everything and run Install-Leo.cmd. For updates select the existing installation. The installer verifies the manifest, backs up and supports rollback. The bundled Windows desktop runtime needs no development environment; personal settings, keys, speech models and third-party clients are excluded.

### Recent changes

- `48df4ea`: Optional read-only computer status and existing mail-client launch, off by default; Huorong launch only.
- `874b78f`: Filter/deduplicate/chunk relevant match metadata to avoid oversized actions; chat reflow,18 icons and status-window improvements.
- `6e4bca5`: Coalesced layout/native-child repaint after chat resize/restore without idle polling.
- `42fea41`: Chinese–English UI using local resources, settings, dynamic bindings and user-content protection.
- Consolidated bilingual guides are included; source-interface links target this release tag.

### Earlier main foundations

Unified execution/contracts/entry contributions, scoped host services and owned background work; migrated core capabilities; bounded model plans and routine recovery; extension compatibility, rollback, status and sharing checks. See [architecture](Architecture/README.en.md) and [roadmap](ROADMAP.en.md) for limits.

Routine success uses quiet logs; errors/reminders retain necessary feedback. Earlier changes fixed search/context-settings interactions and added custom badges/planner quotes. Git preserves per-batch history; old check counts are not accumulated into a current full-suite claim.

### Release verification

Package hashes, privacy patterns and documentation links were checked. Isolated validation covered clean installation, bundled-runtime startup, rollback preserving settings and the first-run UI. No unrelated full-suite rerun was performed. Build succeeded, but network restrictions produced NU1900 for online NuGet vulnerability metadata; no completed online dependency vulnerability audit is claimed.

## Existing releasev1.00.0

[Release page](https://github.com/LeoLi006/leo-desktop/releases/tag/v1.00.0)

That release introduced unified entry routing, app modules/worker extensions, shortcut ordering/overflow, path completion, media controls, independent themes/skin sharing, drop actions and notes. Use its tagged source and binary as the authority; later main changes are not all included.

For manual downloads choose Leo-v1.00.0-win-x64.zip; the equivalent v1.0.0-named asset supports older updaters. Historical screenshots use demo data, not current fixtures.
