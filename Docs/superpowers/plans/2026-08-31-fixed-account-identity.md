# 固定账号与统一 ID Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用 ConfigEditor 中的固定 `ulong accountId` 登录 Lobby，移除临时玩家自增 ID，并将房间、战斗与玩家身份 ID 统一为 `ulong`。

**Architecture:** `C2L_LoginLobby` 是客户端直连 Lobby 的唯一身份入口；服务端回调调用私有 `CreatePlayer` 完成登录后的通用建档和协议注册。`LobbyAccountConfig` 是测试期身份来源，正式 Steam 接入只替换该回调中取得身份的方式，不改变房间逻辑。`ServerUtility.CreateRoomId` 封装房间 ID 的本地序列生成。

**Tech Stack:** Unity 6000.1、C#、Newtonsoft JSON TCP 协议、Unity `JsonUtility` 配置、ConfigEditor。

**Spec:** `Docs/superpowers/specs/2026-08-31-fixed-account-identity-design.md`

## Global Constraints

- 玩家身份、`roomId`、`battleId` 和对外房主 ID 均使用 `ulong`；`accountId == 0` 无效。
- `Connect` 只表示当前物理连接，不能作为玩家身份或实例 ID。
- 不修改未完成的 Battle 开局、重载和切场业务。
- 测试身份只从 `LobbyAccountConfig` 读取；未来 Steam 验票仅替换登录回调的身份来源。
- 当前工作树含用户未提交改动；本计划不执行 git add、commit、reset 或 checkout。

---

### Task 1: 配置编辑器与测试账号配置

**Files:**
- Create: `Assets/Scripts/Game/Config/LobbyAccountConfig.cs`
- Create: `Assets/Res/Config/LobbyAccountConfig.json`
- Modify: `Assets/Scripts/Core/Config/Editor/ConfigEditorWindow.cs:143-203`

**Interfaces:**
- Produces: `CrystalMagic.Game.Config.LobbyAccountConfig` with `public ulong accountId` and `public string username`.
- Produces: ConfigEditor `ulong` field editing through a text field validated with `ulong.TryParse`.
- Consumes: `ConfigComponent.Instance.Get<LobbyAccountConfig>()` in Task 3.

- [ ] **Step 1: Add the configuration type and default JSON**

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

Create `LobbyAccountConfig.json` with `accountId: 1` and `username: "TestPlayer"`.

- [ ] **Step 2: Add a safe ConfigEditor ulong input**

```csharp
if (type == typeof(ulong))
{
    string current = ((ulong)(value ?? 0UL)).ToString();
    string input = EditorGUILayout.TextField(label, current);
    if (input == current)
        return value ?? 0UL;

    if (ulong.TryParse(input, out ulong result))
    {
        changed = true;
        return result;
    }

    return value ?? 0UL;
}
```

Keep invalid and negative text from changing the stored value.

- [ ] **Step 3: Verify ConfigEditor discovery and persistence**

Open `Tools/Config/Config Editor`, refresh types, load `Lobby Account Config`, enter `2`, save, and confirm that `Assets/Res/Config/LobbyAccountConfig.json` contains the exact decimal ID and username. Restore the default test account value after the check.

### Task 2: 统一 Lobby 模型与协议的 ID 类型

**Files:**
- Modify: `Assets/Scripts/Web/Core/Player.cs`
- Modify: `Assets/Scripts/Web/Core/ServerUtility.cs`
- Modify: `Assets/Scripts/Web/Lobby/Room.cs`
- Modify: `Assets/Scripts/Web/Lobby/RoomData.cs`
- Modify: `Assets/Scripts/Web/Lobby/StartRoomData.cs`
- Modify: `Assets/Scripts/Web/Lobby/ServerLobbyManager.cs`
- Modify: `Assets/Scripts/Web/Lobby/Protocol/C2L_JoinRoom.cs`
- Modify: `Assets/Scripts/Web/Lobby/Protocol/L2B_StartRoom.cs`
- Modify: every Lobby/Battle protocol that declares `roomId`, `battleId`, owner ID, or player dictionary keys.

**Interfaces:**
- Produces: `ServerUtility.CreateRoomId() : ulong`.
- Produces: `Player.accountId : ulong`, `Room.roomId : ulong`, `Room.ownerAccountId : ulong`, and `Dictionary<ulong, Player> Room.players`.
- Consumes: `ulong accountId` from Task 3.

- [ ] **Step 1: Replace the model fields and all snapshot keys**

```csharp
public class Player
{
    public ulong accountId;
    public string username;
    public ulong roomId;
    public bool ready;
    public bool start;
    public Connect connect;
}

public class Room
{
    public ulong roomId;
    public ulong ownerAccountId;
    public Dictionary<ulong, Player> players = new();
}
```

Use `0UL` as the no-room sentinel after the migration. Update `RoomData`, `SimpleRoomData`, `StartRoomData`, request/response types, and Battle transfer data to use the same types and names.

Update type-dependent `ServerLobbyManager` declarations and lookups in the same task so that `Player.accountId`, `Room.players`, and `roomList` remain coherent. Keep its temporary accept-time player construction only until Task 3 replaces it with the login callback.

- [ ] **Step 2: Move room ID generation behind ServerUtility**

```csharp
private static long nextRoomId;

public static ulong CreateRoomId()
{
    return (ulong)Interlocked.Increment(ref nextRoomId);
}
```

Remove `nextRoomId` and `GetNewRoomId` from `ServerLobbyManager`; create rooms only through `ServerUtility.CreateRoomId()`.

- [ ] **Step 3: Search for stale identity declarations**

Run:

```powershell
rg -n --glob '*.cs' 'nextUserId|userId|nextRoomId|GetNewRoomId|Dictionary<long, Player>|Dictionary<long, string>|Dictionary<long, bool>|ownerId|long roomId|long battleId' Assets/Scripts/Web
```

Expected: no stale player or instance ID declarations remain; unrelated `long` timer and socket values may remain.

### Task 3: 登录回调、客户端配置登录与安全清理

**Files:**
- Create: `Assets/Scripts/Web/Lobby/Protocol/C2L_LoginLobby.cs`
- Modify: `Assets/Scripts/Web/Lobby/Protocol/C2L_JoinRoom.cs`
- Modify: `Assets/Scripts/Web/Lobby/ServerLobbyManager.cs`
- Modify: `Assets/Scripts/Web/Lobby/ClientLobbyManager.cs`

**Interfaces:**
- Produces: `C2L_LoginLobby { ulong accountId; string username; }` for testing only.
- Produces: `ServerLobbyManager.OnClientLogin(IMessage, Connect)` and private `CreatePlayer(Connect, ulong, string)`.
- Consumes: `LobbyAccountConfig` from Task 1.

- [ ] **Step 1: Restore the test login protocol with a unique opcode**

```csharp
[Message(Opcode = 10)]
[Serializable]
public class C2L_LoginLobby : IMessage
{
    public ulong accountId;
    public string username;
}
```

Set `C2L_JoinRoom` to Opcode `13`: the stable Lobby request range is Login `10`, Leave `11`, Create `12`, Join `13`, Ready `14`, Start `15`. This also resolves the existing Create/Join Opcode `12` collision.

- [ ] **Step 2: Delay Player creation until the login callback**

```csharp
private bool CreatePlayer(Connect connect, ulong accountId, string username)
{
    if (accountId == 0
        || connectPlayerDic.ContainsKey(connect)
        || playerList.ContainsKey(accountId))
        return false;

    Player player = new Player
    {
        accountId = accountId,
        username = username,
        roomId = 0,
        connect = connect,
    };
    connectPlayerDic.Add(connect, accountId);
    playerList.Add(accountId, player);
    RegisterLobbyCallbacks(connect);
    connect.Send(new L2C_RefreshRoomList
    {
        roomListData = RoomListData.CreateRoomData(roomList)
    });
    return true;
}
```

`OnAccept` registers only `C2L_LoginLobby`. `OnClientLogin` calls this helper. `OnDisconnected` first checks `connectPlayerDic.TryGetValue`; unlogged connections only unregister login callbacks and return. Move operation callback registration/unregistration into matching helper methods.

- [ ] **Step 3: Send login on the existing connected event**

```csharp
private void OnLobbyConnected(Connect connect)
{
    if (connect != lobbyConnect)
        return;

    LobbyAccountConfig config = ConfigComponent.Instance.Get<LobbyAccountConfig>();
    lobbyConnect.Send(new C2L_LoginLobby
    {
        accountId = config.accountId,
        username = config.username,
    });
}
```

Subscribe before calling `ClientService.Connect`, and unsubscribe during the existing Lobby disconnect cleanup. Do not poll `Connect.State` and do not let UI send room operations before the initial room list arrives.

Because `ClientLobbyManager` owns its `ClientService`, initialize it in `Awake` and add `LateUpdate` to invoke `clientServic.Update()` exactly once per frame; otherwise `OnConnectedSuccess` cannot execute.

- [ ] **Step 4: Verify protocol and compile**

Run an opcode duplicate scan and build:

```powershell
rg -n --glob '*.cs' '\[Message\(Opcode\s*=\s*[0-9]+' Assets/Scripts/Web
dotnet build Crystal-Magic.sln --no-restore
```

Expected: every active message opcode is unique and the build has no identity-migration errors. If the build reports the pre-existing unfinished `OnClientStart` expression, report it separately without changing Battle start behavior.

- [ ] **Step 5: Manually verify two-account Lobby flow**

Use two independent project/build directories, set `LobbyAccountConfig` to `{ accountId: 1, username: "Player1" }` and `{ accountId: 2, username: "Player2" }`, then verify: Player1 creates a room, Player2 joins, ready state and member keys use `ulong` IDs, Player1 leaves/disconnects, and Player2 becomes `ownerAccountId`.

### Task 4: 同步大厅设计中的身份约定

**Files:**
- Modify: `NetworkLobbyDesign.md`

**Interfaces:**
- Documents: the `ulong accountId` identity model, the `ulong roomId` no-room `0UL` sentinel, and the C2L Login-before-Lobby-operation flow.

- [ ] **Step 1: Replace the temporary user ID model**

Update Player and Room descriptions from temporary `userId`/`ownerId` to `accountId`/`ownerAccountId`. State that `accountId` is a fixed `ulong` sourced from `LobbyAccountConfig` during testing and SteamID after Steam authentication; `Connect` is only the replaceable transport connection.

- [ ] **Step 2: Document instance IDs and login gate**

State that `roomId` and `battleId` use `ulong`, `roomId == 0UL` means no current room, and `ServerUtility.CreateRoomId()` owns local room ID allocation. Change the initial flow to client TCP connection -> `C2L_LoginLobby` -> accepted login -> initial `L2C_RefreshRoomList`; room operations are unavailable before that snapshot.

- [ ] **Step 3: Verify terminology**

Run:

```powershell
rg -n 'userId|ownerId|服务端分配的玩家标识|roomId.*-1|Accept.*创建临时玩家' NetworkLobbyDesign.md
```

Expected: no obsolete identity or initial-login statements remain. Keep sections on unfinished battle behavior out of scope.
