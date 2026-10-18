# 完整皮肤素材生成记录

模式：内置 imagegen；透明背景；未使用 CLI/API Key。参考图分别为原始 `assets/leo.png`、`assets/vertical-drag.png`、`src/Assets/edge-motion-v1.png`。每套保留相同动作顺序和角色身份。

## 主图集、上下拖动与吸附

每个队伍、每种图集分别调用一次，提示词模板如下：

```
Edit target: supplied sprite atlas. Precise clothing-only replacement. {description} Preserve EVERY original sprite pixel position, pose, face, wool, ears, horns, silhouette, linework, outline, shading, cell boundaries and transparent alpha. Do not redesign, add accessories, remove frames or change expressions. Replace red/blue Barcelona clothing on ALL characters with specified team kit; preserve original cloth shading. Output exact original canvas {layout}. Existing empty cells stay empty. No labels, no background, no new logos. This is a production animation atlas: exact layout and consistency across every frame are critical. Transparent background.
```

description：

- city：`Manchester City: sky blue jersey with tonal sky blue vertical stripes, white shorts, navy boots.`
- miami：`Inter Miami: pink jersey with tonal pink vertical stripes, black shorts, black boots.`
- argentina：`Argentina: sky blue and white vertically striped jersey, black shorts, black boots.`
- spain：`Spain: red jersey with subtle darker red vertical stripes and gold collar, navy shorts, navy boots.`

layout：主图集 `1536x2288, 8 columns and 11 rows; each cell 192x208`；上下拖动 `768x416, 4 columns and 2 rows; each cell 192x208`；吸附 `1536x1024, 6 columns and 4 rows`。生成器返回的主图集分辨率可能不同，渲染按其实际尺寸归一读取，动作格顺序不变。

## 起步图集换装

参考图：`src/Assets/edge-bridge-v1.png`。每队单独编辑，提示词：

```
Clothing-only edit of this exact sprite sheet. All32 poses in FOUR rows, EIGHT columns. Replace Barcelona red/blue kit with {clothes}. Preserve every pose, position, cream wool, ears, face, horns, linework and scale. Keep boots navy. No scarves/accessories. Exact canvas1536x1024 and grid unchanged, actual transparent background, no shadows outside character.
```

clothes：city `sky blue jersey, white shorts`；miami `pink jersey, black shorts`；argentina `sky blue and white striped jersey, black shorts`；spain `red jersey, gold collar, navy shorts`。

生成后的 PNG 原样保存于本目录。运行时用透明度连通区域读取起步与吸附图，避免生成素材轻微跨格造成碎片。
