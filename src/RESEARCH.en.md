# Related work and reuse principles

[简体中文](RESEARCH.md) | **English**

This consolidates the initial2026-10-19 research, not a newly verified dependency selection or benchmark. Listing a candidate does not mean it is integrated.

| Area | Candidates | Current approach |
| --- | --- | --- |
| Search | Flow Launcher、Everything | Study provider/ranking ideas, retain Leo's host/search |
| Extensions | PowerToys Command Palette、MCP C# SDK | Study discovery/process boundaries, no compatibility claim |
| Reliable routines | Polly、Workflow Core、Elsa | Keep the small sequential executor rather than a full engine for simple routines |
| Model adaptation | Microsoft.Extensions.AI | Prioritize existing relay compatibility and controlled invocation |
| Install/update | Velopack | Evaluate migration/recovery separately, no automatic replacement |
| Companion interaction | VPet | Study state/interrupt handling, retain Leo assets/host |

Start with a concrete gap, then check current official code, licenses, platform/support and migration cost, then build an isolated prototype. Popularity and project marketing are not Leo performance evidence. Reuse must respect licensing and dependency boundaries.

The research informed unified execution, scoped services, lifecycle and controlled extensions. Actual delivery/limits are documented in [architecture](Architecture/README.en.md) and [roadmap](ROADMAP.en.md). Reassess candidates for a real need, rather than refactor just to add frameworks.
