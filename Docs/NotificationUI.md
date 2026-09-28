# 通用提示 UI

入口为 `Assets/Res/UI/NotificationUI.prefab`，随地牢/战斗和城镇状态打开、释放。使用现有 UIComponent 的 MVC 生命周期和 PoolComponent，不需要在场景额外挂管理器；提示不拦鼠标，也不触发角色输入锁或拦截 Escape。

## 已配置的三种提示

| Key | 子 prefab | 行为 |
| --- | --- | --- |
| pickup | NotificationPickupItem | 屏幕中心，1.1 秒上移 72 UI 单位并渐隐，最多同时 8 条 |
| threat | NotificationThreatItem | 中心上方 150 UI 单位；淡入 0.15 秒，进度变化 0.55 秒，停留 1.2 秒，原地淡出 0.4 秒 |
| revenge | NotificationRevengeItem | 中心上方 230 UI 单位；原地淡入 0.15 秒，显示“复仇小队已出动”1.8 秒，原地淡出 0.4 秒 |

位置使用 Canvas 的参考分辨率坐标。以上都是初始配置，可直接调整，不写死在业务逻辑里。

## 修改样式和动画

打开对应子 prefab，在根节点组件的 `Motion` 中配置：

- `Position`、`StackSpacing`：位置、同类多条消息的间距。
- `EnterSeconds / HoldSeconds / ExitSeconds`：进入、停留、退出时间。
- `EnterOffset / HoldOffset / ExitOffset`：进入时偏移、停留阶段累计位移、退出阶段追加位移；全部为零就是原地显示。
- `EnterScale / ExitScale`：开头、结束时缩放。
- `FadeDuringHold`：停留/移动过程中同步渐隐，拾取使用此项。
- 三条曲线控制各阶段的位移/缩放；进入、退出同时控制透明度。动画使用未缩放时间。

修改 `Label` 的字体、颜色、大小即修改文字样式。进度子 prefab 额外提供 `GrowthSeconds / GrowthCurve`。`Bar/Fill` 使用普通 Image，通过横向锚点宽度显示进度，不要求提供 Sprite；更换图片时保持左侧锚点为零、Size Delta 为零。

主 prefab 的 `Templates` 决定 Key、子 prefab 引用、通道、文案本地化 Key、队列策略、数量上限和进度最大值。`Stack` 同时叠加，`Merge` 合并同一组，`Queue` 按顺序显示。Merge/Queue 每个通道同时显示一条；`MaxVisible` 用于 Stack，`MaxQueued` 限制待播放数。超出上限淘汰最早一条。

新增类型可复制文本/进度子 prefab 并在 Templates 新增 Key；新的显示方式可继承 `NotificationItemView`，通过 `SetContent / TickContent / ContentDuration` 扩展。数据走 Controller → Model → View，不从 View 读取游戏状态。

## 状态脚本触发

新增 `Notify UI` 动作节点（类型 `NotifyUI`）：

- `Notification Key` 对应 Templates 的 Key。
- `From / To` 为数值 getter/表达式，威胁度表示变化前、后的数值。
- `Group / Cycle` 为整数轮次（数值表达式会四舍五入）。同 Key、通道、楼层、轮次才合并。

当前兴趣点、野外小队全灭和巡逻回归都执行：保存旧威胁度 → 原本的增加威胁度 → NotifyUI。通知立即捕获数值，再进入表现队列，不会晚一帧重新读取已经清零的威胁度。

威胁度以 `dungeon.revenge.wave` 分组。同一轮连续增长从屏幕当前进度继续动画；下一轮排队，上一轮先显示至 100%。显示夹在 0–100%，不改变原来的威胁度计算、清零或预算逻辑。没有任何增长时不会主动显示进度条。

复仇小队使用 `SpawnUnit` 的 `Spawned Notification Key = revenge`，在实际生成至少一个成员后只通知一次；名单为空或全部生成失败时不通知。不要把它改成 SpawnUnit.Out 后的普通 NotifyUI——Out 只表示生成命令已排队。

服务器把通知快照写入现有表现事件通道，客户端接收展示；单机直接发布 `NotificationSignalEvent`。拾取沿用原有 PickupFeedbackEvent。进入新楼层、关闭/释放面板时会清空旧提示并归还池对象。

## 检查

Unity EditMode 中运行 `NotificationTests` 和 `DungeonThreatTests`。前者覆盖队列、跨轮次、场景清理、prefab 引用和生成通知门槛；后者包含实际状态脚本旧值/新值快照检查。

PlayMode 需人工确认：拾取漂移、连续威胁增长、到 100% 后下一轮动画、复仇文字原地淡出、切层清理，以及提示期间移动/攻击和鼠标操作不受影响。联机需用主客端各验证一次。
