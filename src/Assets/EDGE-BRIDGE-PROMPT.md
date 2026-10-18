# 四向起步素材

内置 imagegen，透明背景；参考图一为原始 `assets/leo.png`，参考图二为现有 `src/Assets/edge-motion-v1.png`。结果保存为同目录 `edge-bridge-v1.png`。

```
Generate a transparent production sprite sheet with FOUR rows, EIGHT columns, canvas1536x1024. Reference1 is Leo's original base animation atlas; reference2 is existing edge tucking animation. Create ONLY the missing early inbetween movement from reference1 row0 first standing idle toward reference2 first-column pose in each corresponding row. Keep same small cream lamb, face, ears, horns, red blue striped Barcelona kit, navy shorts/boots, exact original 2D linework. Row1 turns gradually toward viewer right and raises far hand beside head; row2 mirrored toward viewer left; row3 gradually bends forward onto hands; row4 gradually lowers from standing toward gentle squat. Each row starts with EXACT original standing neutral pose (arms down), then 6 genuinely incremental early inbetween poses, last frame matches first pose of corresponding row of reference2. Do not continue beyond that first pose, do not hide body. All8 frames each row show full body and ears, same consistent scale, each centered in its own192x256 cell with bottom baseline at240, plenty of transparent separation. Changes between neighboring cells very small and smooth. All32 characters distinct, clean, not overlapping. No backdrop/checkerboard/labels/text. Actual transparent alpha.
```

运行时在每行生成帧之前额外使用原始站立帧，按完整角色连通区域读取，避免格线切断脚和耳朵。
