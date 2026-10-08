# 火球 V2：释放 + 命中爆炸

这次提供两个独立的一次性 WAV，以及一个仅用于试听的组合文件。
声音设计参考火系技能音效包的“发射 / 命中分离”方向，不是对其试听录音的复制。
没有下载、采样或使用 Cyberwave Orchestra 的商业音频。
网页试听本身未在当前工具中完成听辨；本次依据其公开分类和此前确认的声音结构制作。

- 释放：快速喷发 → 火焰向前翻滚 → 少量余焰颗粒。不是命中爆炸。
- 命中：瞬间爆开 → 低频冲击 → 燃烧尾音，不使用金属碎片或枪炮机械声。
- 组合试听：释放在 0 秒，命中在 1.1 秒。这个间隔只是试听示意，游戏中应由实际命中触发。

## 文件

- `fireball_v2_release.wav`：独立释放声，0.76 秒。
- `fireball_v2_impact.wav`：独立命中爆炸，0.98 秒。
- `fireball-v2-sequence.wav`：两者组合，2.08 秒；不应把此试听串放进技能 Effect。

48 kHz、16-bit PCM、单声道。程序处理包括裁切、非线性时间映射、滤波、瞬态整形、音量包络、分层和短淡出。
这是素材剪辑与程序合成，不是文字转音效模型生成。

## 后续配置位置（本次未改动技能）

- 创建火球弹体的释放节点：`Fireball_PrototypeV2/fireball_v2_release.wav`。
- 实际命中引起爆炸的节点：`Fireball_PrototypeV2/fireball_v2_impact.wav`，在爆炸位置播放一次。
- 命中音效应位于范围内逐目标伤害链之外，不能击中几个敌人就重复几次。
- 超距离消失不能无条件播放命中爆炸；不要同时在碰撞和销毁链重复播放。
- 通用起手使用已有独立素材，不包含在这两个文件内。

所有旧 UI、Combat V1 WAV 和技能数据保持不变。来源、处理参数和校验值见 `manifest.json`。

## 使用的源素材

1. **Fireball — Julien Matthey**（diligentcircle 上传，CC0）
   https://opengameart.org/content/fireball-1
   原文件：105016__julien-matthey__jm-fx-fireball-01.wav
   处理：截取、下混单声道、重采样、时间映射、滤波、包络及分层。
2. **Fire Crackling — AntumDeluge**（CC0）
   https://opengameart.org/content/fire-crackling
   原文件：fire-1.wav
   处理：截取、温和压缩突出火焰颗粒、衰减包络与滤波。
3. **Catching fire — themightyglider / qubodup**（CC0）
   https://opengameart.org/content/catching-fire
   原文件：flame_0.ogg，沿用第一轮已解码源素材，仅作很轻的点火层。

页面许可核对日期：2026-10-06。CC0：https://creativecommons.org/publicdomain/zero/1.0/
压力、喷流及冲击噪声层由脚本确定性合成，不含语音、旋律或商业试听采样。
