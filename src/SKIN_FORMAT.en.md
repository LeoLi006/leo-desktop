# Skin and jersey format — English

[简体中文](SKIN_FORMAT.md) | **English**

Applies to main 1.04.0, retaining old format compatibility. `LeoJersey/1` is a front-facing flat fabric texture, not a whole character or animation sheet.

## Jersey requirements

- PNG, exactly1024×1024 pixels, maximum10MB. Import decodes/re-encodes the image and strips original metadata.
- Fill the whole square with opaque fabric color; transparent pixels are composited over white.
- Use front-view left/right and forward-facing text; do not pre-mirror. Place collar at top center, shoulders at the sides, chest details in roughly the upper two-thirds.
- Projection samples approximately x3.5%–96.5%, y0%–68%. Extend plain fabric below this region.
- No character, face, hands, hanger, grass or photographic background. Import validates file specifications, not automatic cutout or perspective correction.
- Existing character shape, fur, face and animation remain; only clothing regions receive the texture. Custom shorts default dark. Tiny lettering remains limited by actual companion size.

Export the jersey template in Settings and replace its sample artwork. Create an independent theme or begin from a team preset, then edit colors. Local custom skins use `skins/<generated-id>/skin.json` and `jersey.png`; imported copies do not overwrite built-ins. Keep original PNGs: ordinary configuration backup does not include jersey image files.

## Independent themes and packages

A CustomSkin can use BaseTheme=`independent`. Palette contains Primary/Secondary/Accent as #RRGGBB and a single-line Motto up to 80 characters. DefaultOptions supports accent override, brightness70–130, radius0–24 and opacity70–100. Old team-based skins remain supported.

A `.leoskin` file is a ZIP up to 20MB, containing exactly:

| File | Requirements |
| --- | --- |
| `skin.json` | Format=`LeoSkin/1`, Name1–24 characters, Palette, Options and optional OriginalKit/HasBadge. |
| `jersey.png` | Required1024×1024 PNG up to 10MB; signature/dimensions checked before re-encoding. |
| `badge.png` | Optional planner badge, consistent with HasBadge=true. |

No other entries, subdirectories, duplicates or links are allowed. OriginalKit=true preserves original Barcelona clothing when sharing the default design; ordinary custom jerseys use false. Packages omit local IDs/paths; import always creates a new identity rather than replacing an existing one.

## Badge and planner quotes

Badge import accepts images16–2048 pixels per dimension, maximum4MB, re-encoded into transparent256×256 PNG with aspect-preserving padding. Local BadgeFile names are generated `badge-<32hex>.png` within the skin directory, never a source path. Copy/export includes the custom badge; built-in badges remain unchanged.

PlannerQuotes belongs to Preferences, not the skin package: absent/null selects defaults, an empty array disables quotes, one quote is static, maximum200 lines of 120 characters each. Quotes travel with private configuration migration, not public skin sharing. User-provided names, mottos and images still require privacy/rights review before publication.
