# Verification and performance — English

[简体中文](PERFORMANCE.md) | **English**

The measurements below are historical evidence from the1.03.0 optimization on2026-10-19, not a benchmark of every current build. Both sides used the same .NET9.0.6 isolated host, fake data, disabled app indexing/network work, warmup and six-second samples. The release runtime differed. CPU is process CPU time; allocations are cumulative managed bytes, not resident memory. First-show time is not login/cold startup. The docking stress loop deliberately advanced work roughly every 5ms and is not representative idle load.

| Six-second scenario | Allocations before→after | CPU time before→after |
| --- | --- | --- |
| Normal idle | 9.65MB→0.055MB | 203ms→219ms |
| Static docking stress | 99.95MB→1.44MB | 1438ms→281ms |
| Fullscreen hidden | 0.026MB→0.034MB | 16ms→47ms |
| Planner/log windows open | 9.88MB→0.050MB | 203ms→203ms |

The credible gains are lower repeated allocations and elimination of duplicate static-docking submissions, not a universal CPU/window-speed claim. Idle remained about 42–43 frames/6s. Stress duplicate submissions fell430→0. One idle endpoint private-memory sample was53.0→41.5MB; GDI40→42 reflects retained reusable drawing resources. First show354→316ms is one sample; note opening46→60ms did not establish a speedup. Small hidden CPU values were noisy.

An isolated180.12-second/812-round soak had stable GDI74 and USER24 handles after warmup, managed heap roughly1.32–1.41MB and private memory 56.9–59.0MB. Its287 directly related checks covered persistence/recovery, lifecycle, interaction, docking, profiles, execution/worker cancellation, queues, planner and rendering. Two soak bounds also passed. These are historical scoped results, not a newly rerun full suite or proof against long-term leaks.

## Implementation principles

Attach theme/scrollbar hooks on demand rather than traversing entire control trees at every idle event. Reuse bitmap/native DIB/DC/pixel buffers, clear transparent pixels each frame, rebuild on size changes and release on exit. Stop animation while fully hidden; retain interactive transitions. Own/cancel background work and discard stale generations without closing external user applications.

Core settings/API/tasks/notes use unique same-directory temporary files, full writes/flush and atomic replacement with prior complete backups. Recovery preserves corrupt originals. Failed saves do not publish an uncommitted snapshot; a failed view callback does not undo an already committed write. This is not a multi-file transaction or a guarantee against hardware failure/all backups being corrupt.

## Later stability and language evidence

A later approximately five-minute isolated stability check did not resolve a private-memory high-water observation. It is neither proof of leakage nor proof of absence. The agreed next step is everyday observation, not repeated tuning without evidence. No multi-day real client/network qualification is claimed.

At42fea41, related checks passed: language28, chat resize9, conversation41, desktop tools58, entry display109, profiles36, themes65(total346), build0 warnings/errors. These are different scoped runs and must not be added to historical counts as a new full-suite number. Chat checks simulate resizing/reopening/native-child repaint without idle redraw loops; real driver/DPI combinations still need feedback.

## Reproduction and reporting

Use `src/Test-Robustness.ps1`; `-Measure` adds sampling and `-Soak` adds sustained isolated operations. Inspect relevant test scripts before invoking them. Examples for newer work include `--ui-language-test` and `--chat-resize-test`; report paths must be absolute and point inside ignored .build. Do not read real personal data, call paid model APIs or start real clients for default fixture runs.

Run only checks directly affected by a change. Record scenario, commit, runtime, duration, warmup and metrics, and distinguish simulation from real use. Original raw reports stay local/ignored; do not upload them as personal diagnostics. This documentation-only consolidation does not rerun runtime tests or install a new build.
