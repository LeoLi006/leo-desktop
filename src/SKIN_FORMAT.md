# LeoJersey/1 球衣纹样规格

适用 v1.04.0（兼容旧格式）。此格式是正面球衣的平铺纹样，不是整张角色图或完整动作图集。

- PNG，严格1024×1024像素，最多10MB；图片解码后重新编码保存，不保留原文件元数据。
- 建议使用不透明背景，铺满球衣面料底色；透明部分导入时按白色合成。
- 左右方向与正面观看一致，胸前文字正向；无需预先镜像。
- 领口在顶部中央，肩部可放在两侧；队徽、赞助等胸前细节放在上方约2/3区域。
- 当前贴图读取横向约3.5%～96.5%、纵向约0～68%的区域；下方可延续面料底色。
- 不要包含人物、脸、手、衣架、草地或摄影背景。导入器只校验文件规格，不进行自动抠图或透视矫正。
- 原角色脸部、毛发、轮廓和动作保持原样；只将纹样贴合衣服区域。自定义皮肤短裤默认深色。

使用设置中的“导出球衣模板”制作自己的图片。模板内的示例文字和标识应覆盖为自己的设计。可新建独立主题，也可从现有球队开始，之后在主题编辑器调整配色。

本地存储：`skins/<随机id>/skin.json` 与 `jersey.png`，不覆盖内置素材。源图片保留；删除自定义皮肤只移除导入副本。配置备份不包含球衣图片，迁移时请保留自己的原始PNG。


## v0.53.0：独立主题与分享包

独立皮肤不再必须继承球队。`CustomSkin`的`BaseTheme`可为`independent`，同时具有`Palette`：`Primary`、`Secondary`、`Accent`为`#RRGGBB`颜色，`Motto`为最多80字符的单行标语。`DefaultOptions`保存强调色覆盖、明暗（70–130）、圆角（0–24）、不透明度（70–100）。旧球队皮肤与旧自定义皮肤仍可使用。

`.leoskin`是ZIP容器，允许两个必需根文件和一个可选标识文件：

- `skin.json`：`Format="LeoSkin/1"`、`Name`（1–24字符）、`Palette`、`Options`、可选`OriginalKit`。
- `badge.png`：可选清单标识，须与 `HasBadge` 声明一致，详见下节。
- `jersey.png`：1024×1024 PNG，最多10MB；读取前校验PNG签名与尺寸，导入时重新编码并去除原图片元数据。

`OriginalKit=true`表示使用原始巴萨动画服装，供默认皮肤往返分享时保留原始效果。普通自建球衣使用false。

皮肤包最多20MB，不接受其它条目、链接、重复名称或子目录；包内不保存本机ID和文件路径。导入使用新GUID，始终新增皮肤，不按原ID覆盖。自己的PNG应使用正面平铺纹样，不把人物、球衣展示照片或背景放进贴图。

## v1.04.0：清单队徽与国旗

皮肤包可选增加根文件 `badge.png`，同时 `skin.json` 中 `HasBadge=true`；没有标识的旧包仍只有两个根文件。图片导入先检查4MB文件上限及16～2048像素的宽高，重新编码为透明256×256 PNG，原比例留白显示。导出不携带本机文件路径。

自定义皮肤本地清单增加可选 `BadgeFile`，只接受程序生成的 `badge-<32位十六进制>.png` 名称；在皮肤目录内保存。上传源文件保留原样，复制皮肤和分享包会带上标识。内置皮肤标识不覆盖。

清单滚动话语属于 Preferences.PlannerQuotes：缺省或null使用原有默认句子，空数组关闭，单句静态显示，最多200句、每句最多120字。话语包含在用户配置迁移中，不包含在皮肤分享包中。


---

# Skin and jersey format — English

Applies to main1.04.0, retaining old format compatibility. `LeoJersey/1` is a front-facing flat fabric texture, not a whole character or animation sheet.

## Jersey requirements

- PNG, exactly1024×1024 pixels, maximum10MB. Import decodes/re-encodes the image and strips original metadata.
- Fill the whole square with opaque fabric color; transparent pixels are composited over white.
- Use front-view left/right and forward-facing text; do not pre-mirror. Place collar at top center, shoulders at the sides, chest details in roughly the upper two-thirds.
- Projection samples approximately x3.5%–96.5%, y0%–68%. Extend plain fabric below this region.
- No character, face, hands, hanger, grass or photographic background. Import validates file specifications, not automatic cutout or perspective correction.
- Existing character shape, fur, face and animation remain; only clothing regions receive the texture. Custom shorts default dark. Tiny lettering remains limited by actual companion size.

Export the jersey template in Settings and replace its sample artwork. Create an independent theme or begin from a team preset, then edit colors. Local custom skins use `skins/<generated-id>/skin.json` and `jersey.png`; imported copies do not overwrite built-ins. Keep original PNGs: ordinary configuration backup does not include jersey image files.

## Independent themes and packages

A CustomSkin can use BaseTheme=`independent`. Palette contains Primary/Secondary/Accent as #RRGGBB and a single-line Motto up to80 characters. DefaultOptions supports accent override, brightness70–130, radius0–24 and opacity70–100. Old team-based skins remain supported.

A `.leoskin` file is a ZIP up to20MB, containing exactly:

| File | Requirements |
| --- | --- |
| `skin.json` | Format=`LeoSkin/1`, Name1–24 characters, Palette, Options and optional OriginalKit/HasBadge. |
| `jersey.png` | Required1024×1024 PNG up to10MB; signature/dimensions checked before re-encoding. |
| `badge.png` | Optional planner badge, consistent with HasBadge=true. |

No other entries, subdirectories, duplicates or links are allowed. OriginalKit=true preserves original Barcelona clothing when sharing the default design; ordinary custom jerseys use false. Packages omit local IDs/paths; import always creates a new identity rather than replacing an existing one.

## Badge and planner quotes

Badge import accepts images16–2048 pixels per dimension, maximum4MB, re-encoded into transparent256×256 PNG with aspect-preserving padding. Local BadgeFile names are generated `badge-<32hex>.png` within the skin directory, never a source path. Copy/export includes the custom badge; built-in badges remain unchanged.

PlannerQuotes belongs to Preferences, not the skin package: absent/null selects defaults, an empty array disables quotes, one quote is static, maximum200 lines of120 characters each. Quotes travel with private configuration migration, not public skin sharing. User-provided names, mottos and images still require privacy/rights review before publication.
