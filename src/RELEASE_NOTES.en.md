# Change log

[简体中文](RELEASE_NOTES.md) | **English**

## v1.06.0 — 2026-11-07 · File quick actions and optional integrations

This feature release consolidates changes since v1.05.0 while retaining the 1.x compatibility range. The complete Windows x64 ZIP bundles the desktop runtime and includes a SHA-256 checksum.

### Find and process files

- Icon-based application/file search switching, direct paths and VS Code new-window opening. Search modes have matching dimensions; size-slider repaint was corrected.
- Right-click real local apps to reveal their executable. Shortcuts resolve their target instead of revealing the Start Menu shortcut folder.
- Drop on Leo's head, face or body, then choose: dropping never immediately runs a file. Up to three relevant primary actions; other openers, copy/reveal, notes and tasks remain secondary.
- One Markdown file can be handed to a separately configured Mojian with the new import interface and a provisioned Python environment. Import is confirmed only by a matching receipt. Check preview, template and output location and confirm generation in Mojian yourself. No automatic generation/export, overwrite or dependency installation; no Markdown batch handoff yet.
- VS Code always opens a new window. PDFs can use configured SumatraPDF. Capability rules reuse application modules, extensions, executable, arguments, working directory and explicit administrator settings. Paths stay local. Type matching is not a compatibility guarantee; submitting a launch request does not prove opening succeeded.

### Integrate existing products when needed

- AI news and paper/project briefs with field selection, optional Chinese title translation and reading aloud. Network sources may lag or omit content.
- Footer search targets Edge, Gemini, DeepSeek or ChatGPT. Web AI filling/sending requires manually loading the bundled browser extension and signing in to the selected website, without a model API key. Site changes, login and security challenges may require manual intervention.
- The former proxy shortcut opens separately installed ClashDock. This package does not bundle or assume it is installed. Missing configuration/dependencies yield a brief message; right-click to configure its path.
- Missing optional apps, Mojian scripts/programs or modules remain isolated. Targeted checks cover configuration read failures, launch failures, timeouts, cancellation and continued operation after failures.

### Installation, privacy and verification

Extract everything, run Install-Leo.cmd and select the existing dedicated Leo folder when updating. Manifest checks, backup and rollback are retained. Personal settings are preserved. No third-party client, private path, API key, chat/clipboard data or voice model is distributed. Chinese and English guides remain separate; start with [Quick start](QUICK_START.en.md).

Evidence includes 55 open-capability checks, 15 ClashDock checks, 51 prior drop/entry checks and Mojian interface/isolated real-GUI import checks. Release verification additionally checks the manifest, clean installation, bundled-runtime startup and update rollback. Targeted verification is not a guarantee for every OS or long-running workload.


## Quick-start documentation

Added a [quick start](QUICK_START.en.md) with three-minute setup, an entry lookup table, expandable task instructions and troubleshooting. The home page prioritizes download/use; developer references are separate. Chinese and English remain independent. Documentation/future packaging inputs only; the v1.05.0 binary is unchanged.

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
