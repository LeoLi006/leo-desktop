# Roadmap

[简体中文](ROADMAP.md) | **English**

Updated 2026-10-21, based on main 42fea41. Goal: a stable, efficient, configurable desktop action hub. Feature count does not replace quality.

This page consolidates historical batches by current outcome. A pilot is not full delivery. Previous tasks remain in Git history. Agree on scope before implementation; documentation changes do not automatically publish binaries.

## Delivered scope

- [x] Unified action registration/adaptation/results/cancellation and old-ID compatibility; major existing capabilities migrated.
- [x] Scoped services, contracts, search/menu/button contributions and disable/remove lifecycle for ordinary internal modules.
- [x] Integrated configuration/dependencies, presentation/feedback, theme subscriptions and owned periodic tasks.
- [x] Snapshot/draft/commit checks and match→task/note→task handoff verification.
- [x] Existing app/LeoModule/1 package compatibility, recovery, status and sharing-boundary checks.
- [x] Targeted short stability checks with explicit limits, not extreme optimization.
- [x] Optional read-only computer status, existing mail-client entry, icon library, chat resize repainting and Chinese–English UI.
- [x] Consolidated bilingual public documentation aligned with main.

## Ongoing observation

- [ ] Observe windows/background activity/memory/handles over real multi-day use; private-memory high-water is not proven leakage or fully explained.
- [ ] Chat resize behavior across real DPI/monitor/driver combinations and streaming content.
- [ ] Changing data sources, real client readiness and login startup behavior.
- [ ] Feedback on long English labels, unknown extensions and user-content boundaries.

Use reproducible issues as evidence. Do not broaden tests or split interfaces without a concrete need. Add focused checks for confirmed defects; keep real keys and personal data out of fixtures.

## Remaining goals

| Trigger | Work | Acceptance |
| --- | --- | --- |
| Concrete defect | Fix entry, persistence, disable/exit and late-result failures | Reproduce, minimal fix, relevant checks pass |
| Real new-module need | Validate current contract/service reuse | Reuse generic entry paths or document necessary composition |
| External SDK decision | Version negotiation, permissions/trust and independent delivery | Specify compatibility, permissions, isolation and recovery; not delivered today |
| Existing routines insufficient | Extend bounded routines for a concrete case | Explicit evidence, cancel/retry and unknown-state handling |
| Explicit release decision | Clean package, upgrade recovery, version/screenshots | No personal data, verifiable payload, matching docs/binaries |

No online marketplace, arbitrary-code hot loading, full sandbox, fully declarative UI, general parallel compensation or global transaction is promised. These require separate decisions rather than minor cleanup.

## Working rules

Define/approve each batch, implement, validate direct effects, update development notes and commit locally. Update the local program only for runtime changes; GitHub upload, packaging and releases require their own explicit scope.
