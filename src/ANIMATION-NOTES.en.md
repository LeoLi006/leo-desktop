# Skin and animation maintenance

[简体中文](ANIMATION-NOTES.md) | **English**

The five built-ins are Barcelona(default), Argentina, Spain, Inter Miami and Manchester City. Custom skins use jersey textures and themes, not automatic match-day switching.

## Assets

| Path | Purpose |
| --- | --- |
| assets/leo.png | Main idle, movement, interaction and thinking |
| assets/vertical-drag.png | Vertical dragging |
| src/Assets/edge-bridge-v1.png | Four-direction docking bridge poses |
| src/Assets/edge-motion-v1.png | Docking, head-only and reveal |
| src/Assets/MatchDay/Kits/*-2024.png | Additional team jersey textures |

Paths are repository-relative; build output uses asset mapping. Barcelona retains original artwork; other skins project textures onto the same clothing regions. See [format](SKIN_FORMAT.en.md) and [asset notes](Assets/MatchDay/Kits/README.en.md).

## State and rendering requirements

- Preserve aspect ratio, ears/horns and head scale relative to standing poses.
- Mirror the character for the right edge with inward gaze; project clothing afterward so lettering stays forward.
- Hide buttons while docked, restore on reveal, and clear docking state when dragging out.
- Use per-pixel premultiplied alpha, remove only very faint alpha<8 noise, and release host-owned bitmaps/DCs/buffers.
- Project at original cell resolution then scale once; tiny lettering remains display-size limited.
- Avoid duplicate static dock submissions; rebuild buffers on size change and release on exit.

After animation changes, use `--dock-animation-test <absolute-report-path>` for four edges, transitions, drag takeover and monitor work areas; write reports to.build. Short tests do not replace real DPI/monitor observations.
