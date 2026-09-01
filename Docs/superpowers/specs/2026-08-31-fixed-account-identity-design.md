# 固定账号与统一 ID 设计

## 目标

移除 Lobby 的临时 `nextUserId`。所有玩家身份使用固定的 `ulong accountId`；房间与战斗实例也使用 `ulong`。测试客户端从各自配置提供账号身份，未来 Steam 验票后以同一入口注入 SteamID。

## ID 约定

| 名称 | 类型 | 所有者 | 作用 |
| --- | --- | --- | --- |
| `accountId` | `ulong` | 登录/认证层 | 稳定玩家身份；测试期为配置值，正式期为验票得到的 SteamID。 |
| `roomId` | `ulong` | Lobby | 大厅房间实例。 |
| `battleId` | `ulong` | Battle | 战斗实例。 |
| `Connect` | `Connect` 对象 | 网络层 | 当前 TCP 连接；断线和重连时可替换，绝不能当作身份。 |

`accountId == 0` 是无效值。所有发送给客户端的房主与成员身份字段使用 `ulong`。当前 TCP 消息使用 Newtonsoft JSON，`ulong` 与 `Dictionary<ulong, T>` 可正常往返；如果以后消息直接被 JavaScript 读取，SteamID 必须在该边界编码成字符串。

## 身份进入 Lobby

`ServerLobbyManager.OnAccept` 只接受未经身份确认的连接并注册 `C2L_LoginLobby` 回调；不得创建 Player 或发送大厅快照。`OnDisconnected` 必须允许未登录连接直接断开。

```csharp
private void OnClientLogin(IMessage message, Connect connect)
private bool CreatePlayer(Connect connect, ulong accountId, string username)
```

`OnClientLogin` 是唯一的网络入口：它从登录消息取得经当前认证方式确认的身份，再调用私有 `CreatePlayer`。`CreatePlayer` 验证 ID 非零、连接未登录且账号未在线，随后创建 `Player`、建立连接/账号双向索引、注册大厅操作协议，并向该连接发送初始 `L2C_RefreshRoomList`。

当前测试使用 `C2L_LoginLobby { ulong accountId, string username }`：客户端在 `ClientService.OnConnectedSuccess` 中发送它。正式 Steam 接入时保留该回调位置，但消息改为携带 Steam 登录票据；`OnClientLogin` 验票得到 `ulong steamId` 后调用同一个 `CreatePlayer(connect, steamId, username)`。Lobby 房间逻辑不读取或验证 Steam 票据以外的身份数据。

同一 `accountId` 的第二条在线连接当前拒绝登录；本次不实现 Lobby 连接替换与重连保留房间状态。

## ID 创建与数据迁移

`ServerUtility` 持有私有的房间序列，并公开：

```csharp
public static ulong CreateRoomId()
```

实现使用内部 `long` 计数器配合 `Interlocked.Increment`，将正值转换为 `ulong`。Lobby 只能调用该方法，不能保存 `nextRoomId`；后续替换为数据库或雪花 ID 时只改 Utility 的实现。

以下模型字段统一迁移：

- `Player.userId` 改为 `Player.accountId : ulong`。
- `Room.roomId`、`Room.ownerAccountId` 和 `Room.players : Dictionary<ulong, Player>`。
- `RoomData`、`SimpleRoomData`、`StartRoomData`、加入房间请求以及所有 Lobby/Battle 传输对象中出现的 `roomId`、`battleId`、房主 ID、玩家字典键。
- `ServerLobbyManager.playerList : Dictionary<ulong, Player>` 与 `connectPlayerDic : Dictionary<Connect, ulong>`；局部变量使用 `accountId`，不再使用含糊的 `playerId`。

本次不修改尚未完成的 Battle 开局、重载与客户端切场业务；`L2B_StartRoom` 继续传递迁移后的 `RoomData`，从而已具备 `ulong` 房间与玩家数据。

## 测试配置与客户端流程

新增 `LobbyAccountConfig`，放在现有 ConfigEditor 扫描的 `CrystalMagic.Game.Config` 命名空间：

```csharp
[Serializable]
[GameConfig]
[EditorLabel("Lobby Account Config")]
public class LobbyAccountConfig
{
    [EditorLabel("Account Id")]
    public ulong accountId = 1;

    [EditorLabel("Username")]
    public string username = "TestPlayer";
}
```

ConfigEditor 保存它到 `Assets/Res/Config/LobbyAccountConfig.json`。`ConfigEditorWindow` 增加 `ulong` 编辑控件：使用文本输入和 `ulong.TryParse`，拒绝负数、溢出或无效文本，避免将 SteamID 限制在 `long` 范围内。

每个测试进程使用自己的 `LobbyAccountConfig` 值，例如 `1` 与 `2`。`ClientLobbyManager` 从 `ConfigComponent.Instance.Get<LobbyAccountConfig>()` 读取身份，订阅 `OnConnectedSuccess`，并在该事件中发送 `C2L_LoginLobby`；它不会轮询 `Connect.State`。

`Assets/Res/Config` 是同一工程副本共享的资源：并行运行两个 Editor 实例时它们会读到同一个账号配置。要让并行进程使用不同 ID，需要使用两个独立工作目录/构建目录并分别保存配置；本次不添加命令行或持久化文件覆盖机制。

服务端接受登录后才发送初始房间列表。客户端收到该快照后现有 `onRoomRefresh` 流程保持不变；创建、加入、离开和准备不接受未登录连接的请求。

## 验证

1. 两个不同配置的客户端登录，服务端 Player、Room 和 RoomData 均使用各自的 `ulong accountId`。
2. 创建、加入、准备、离开、房主转移和断线移房间仍正确更新 `Dictionary<ulong, Player>` 与快照。
3. `accountId == 0`、重复连接身份和未登录客户端的大厅请求均不改变 Lobby 状态。
4. 编译 `Crystal-Magic.sln`，并确认所有协议 opcode 唯一。
