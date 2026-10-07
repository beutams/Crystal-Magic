# Crystal Magic UI：材质版六音试听 v2

这版使用 Kenney 的 CC0 音效作为底子，剪辑、变速、均衡、叠层并统一电平。不是纯原创录音，也没有用 AI 音频模型生成。只有“魔法嵌入”的短气流层为程序合成；不含钟声音阶、提示旋律或长混响。

素材来源：

- RPG Audio：https://kenney.nl/assets/rpg-audio
- Impact Sounds：https://kenney.nl/assets/impact-sounds
- 两个包随附的原始 CC0 声明保存在 ZIP 和 sources 目录。每个成品的具体源文件、裁切区间、变速和叠层参数见 manifest.json。
- Interface Sounds 仅下载评估，未用于这六个成品。

## 试听与项目位置

- WAV：48 kHz、16-bit PCM、单声道；保留电平余量，非循环。
- 项目文件：`Assets/Res/Audio/SFX/UI_MaterialV2/`。
- 旧版 24 个 WAV 完整保留，生成时会检查旧版文件哈希；没有改 UI 播放事件。
- `ui-v2-preview.wav`：01 至 06，间隔 0.6 秒。
- `ui-v1-v2-comparison.wav`：按同一顺序，每组先旧版、再新版。仅对照音轨将旧版峰值匹配到新版，避免音量差掩盖音色差；这不是响度 LUFS 匹配，主文件没有改变。
- 请在低音量下先试听，再结合翻页/装备动画判断是否合适；文件检查不能替代主观听感验收。

## 复现

项目中的 `Tools/Audio/prepare_ui_material_sources.py` 从已下载的 Kenney 官方 ZIP 中选取音源；`decode_ui_material_sources.cjs` 使用独立无界面浏览器解码成 48 kHz 单声道 float32；`build_ui_material_v2.py` 生成本版。

ZIP 内保留实际使用的源音、许可证、处理清单和脚本作参考。脚本按本项目目录布局运行，并依赖原 v1 用于生成对照音轨；不能把该 ZIP 视为独立一键运行的工程。默认拒绝覆盖已有 v2 WAV，确认需要替换后才传 `--force`。
