# 皮肤与动画维护 / Skin and animation maintenance

五套内置皮肤为巴萨默认、阿根廷、西班牙、迈阿密和曼城；自定义皮肤通过球衣纹样与主题创建，不依赖比赛日自动换装。
The five built-ins are Barcelona(default), Argentina, Spain, Inter Miami and Manchester City. Custom skins use jersey textures and themes, not automatic match-day switching.

## 素材 / Assets

| 路径 / Path | 用途 / Purpose |
| --- | --- |
| assets/leo.png | 主要待机、移动、互动、思考 / Main idle, movement, interaction and thinking |
| assets/vertical-drag.png | 上下拖动 / Vertical dragging |
| src/Assets/edge-bridge-v1.png | 四向过渡起步姿态 / Four-direction docking bridge poses |
| src/Assets/edge-motion-v1.png | 收起、头部隐藏与展开 / Docking, head-only and reveal |
| src/Assets/MatchDay/Kits/*-2024.png | 额外球队球衣纹样 / Additional team jersey textures |

表中路径从仓库根目录解释，开发输出目录可能使用assets映射。默认巴萨保留原图，额外皮肤投影同一角色的衣服区域。[规范](SKIN_FORMAT.md)与[素材说明](Assets/MatchDay/Kits/README.md)列出图片要求。
Paths are repository-relative; build output uses asset mapping. Barcelona retains original artwork; other skins project textures onto the same clothing regions. See [format](SKIN_FORMAT.md) and [asset notes](Assets/MatchDay/Kits/README.md).

## 状态与绘制要求 / State and rendering requirements

- 等比绘制，保留耳朵和羊角，头部宽度与站立姿态对齐。Preserve aspect ratio, ears/horns and head scale relative to standing poses.
- 右侧由左侧角色对称，视线朝内；球衣后投影以避免文字镜像。Mirror the character for the right edge with inward gaze; project clothing afterward so lettering stays forward.
- 收起隐藏功能键；展开恢复；直接拖出必须清理吸附状态并接管动画。Hide buttons while docked, restore on reveal, and clear docking state when dragging out.
- Windows逐像素Alpha、预乘提交、仅去除Alpha<8极淡噪点；位图/DC/缓冲由宿主管理释放。Use per-pixel premultiplied alpha, remove only very faint alpha<8 noise, and release host-owned bitmaps/DCs/buffers.
- 球衣先按原动作格贴合，再缩放一次；小字体受实际显示尺寸限制。Project at original cell resolution then scale once; tiny lettering remains display-size limited.
- 静止吸附避免相同帧重复提交；尺寸变化重建缓冲，关闭释放。Avoid duplicate static dock submissions; rebuild buffers on size change and release on exit.

只在修改动画后运行定向入口`--dock-animation-test <绝对报告路径>`，检查四向、衔接、拖动接管和多屏工作区，报告写.build。短测不替代不同DPI/显示器实际观察。
After animation changes, use `--dock-animation-test <absolute-report-path>` for four edges, transitions, drag takeover and monitor work areas; write reports to.build. Short tests do not replace real DPI/monitor observations.
