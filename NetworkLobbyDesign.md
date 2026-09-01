# 联网大厅设计

本文只描述联网大厅：连接后浏览房间、创建房间、加入房间、离开房间和断线处理。

不包含准备、开始游戏、战斗同步、地牢加入、存档、重连恢复和结算。

## 1. 当前范围

大厅服务端作为唯一权威，维护全部玩家与房间状态。

- 客户端只发送大厅操作请求。
- 服务端校验请求、修改状态，并向相关客户端发送结果或状态快照。
- 房间列表只在真实房间状态发生变化后广播。
- 房间内成员详情只发送给该房间的剩余成员。

当前已有的基础能力：

- TCP 长度帧协议与 opcode 消息注册。
- 客户端 Ping、服务端 Pong、10 秒无消息超时断开。
- 服务端创建玩家与房间。
- 创建房间、加入房间、主动离房、断线离房。
- 房主断线时转移房主。
- 空房间删除。

## 2. 服务端大厅状态

### 2.1 Player

每个已连接客户端对应一个 `Player`：

- `ulong accountId`：玩家账号标识。测试时身份来自 `LobbyAccountConfig { accountId, username }`；生产环境由 Steam 认证提供 SteamID。
- `username`：大厅展示名称。
- `roomId`：当前房间 Id，类型为 `ulong`；`0UL` 表示未加入房间。
- `connect`：该玩家当前的可替换传输网络连接，不承担身份认证职责。

### 2.2 Room

每个房间包含：

- `ulong roomId`
- `roomName`
- `ulong ownerAccountId`
- `maxNum`
- `players`

`roomId` 与 `battleId` 均为 `ulong`。`roomId == 0UL` 表示当前没有房间；房间实例 Id 由 `ServerUtility.CreateRoomId()` 统一分配。

`enterNum` 为 `players.Count` 的镜像值。后续可以移除该冗余字段，或只通过统一方法更新它。

## 3. 服务端待补逻辑

### 3.1 统一移出房间

主动离房和客户端断线必须共用同一个服务端方法，例如：

```csharp
RemovePlayerFromRoom(Player player, bool notifyLeavingPlayer)
```

该方法按以下顺序执行：

1. 校验玩家当前确实在房间内。
2. 从 `Room.players` 删除玩家，并更新成员数量。
3. 若房间为空，删除房间。
4. 若离开者是房主，选择一个剩余成员成为新房主。
5. 若房间仍存在，向全部剩余成员发送最新 `RoomData`。
6. 仅在成员数、房主或房间存在性真正变化后广播房间列表。

这样主动离房与断线不会产生不同的房主转移或成员同步规则。

### 3.2 请求与失败响应

每个请求都必须有明确结果：

- `L2C_CreateReturn`、`L2C_JoinReturn` 与 `L2C_LeaveReturn` 都使用
  `LobbyRequestType type` 表示结果，而不是 `bool success`。
- `CreateSuccess`、`JoinSuccess` 与 `LeaveSuccess`：服务端已成功应用对应请求；创建和
  加入成功时会附带 `roomData`。
- `CreateFail`、`JoinFail` 与 `LeaveFail`：对应操作的通用业务失败，例如玩家状态不满足
  操作条件。
- `JoinRoomClosed`：加入目标已不存在。
- `JoinRoomFull`：加入目标人数已满。
- `CreateTimeout`、`JoinTimeout` 与 `LeaveTimeout`：仅由客户端在等待服务端响应超时时
  产生，服务端不发送该结果。

枚举的默认值必须是非成功状态（例如 `Unknown = 0`），避免消息遗漏
`type` 或协议版本不一致时被错误地视为成功。

服务端需要校验：

- 房间名为空、过长或非法。
- 房间不存在或已满。
- 玩家已在其他房间，或重复加入当前房间。
- 玩家未在房间时请求离开。
- 连接与玩家映射已失效。

失败请求不改变房间状态，也不广播房间列表。

### 3.3 广播规则

`L2C_RefreshRoomList`：

- 新房间创建成功。
- 玩家成功加入某房间。
- 玩家成功离开某房间。
- 房内玩家断线导致成员、房主或房间存在性改变。

不广播的情况：

- 创建、加入、离开失败。
- 不在房间的客户端断线。

客户端 TCP 连接成功后，客户端发送 `C2L_LoginLobby`。服务端验证登录身份并创建
`Player`，随后只向该客户端主动发送一次 `L2C_RefreshRoomList` 作为初始大厅快照；
它不是面向所有客户端的房间列表广播。客户端在收到初始快照前不得发送创建、加入、
离开等房间操作。

`L2C_RefreshRoomInfo`：

- 玩家加入、主动离开或断线后，发送给该房间全部剩余成员。
- 房主发生转移后，发送给该房间全部剩余成员。

创建者通过 `L2C_CreateReturn { type = Success, roomData }` 获得新房间详情；加入者
通过 `L2C_JoinReturn { type = Success, roomData }` 获得加入后的房间详情。

## 4. 客户端大厅设计

### 4.1 ClientLobbyManager

`ClientLobbyManager` 是客户端大厅的唯一状态入口。它持有 `ClientService`，在 `Awake` 中自动建立连接并注册大厅协议回调；连接成功回调 `OnConnectedSuccess` 私下发送配置的登录消息。它维护下列本地状态：

- `accountId`：配置的玩家账号标识；断线时重置为 `0`。
- `loggedIn`：收到非空的初始房间列表快照后才置为 `true`，作为发送房间操作的门槛。
- 房间列表：`RoomListData`。
- 当前所在房间：`RoomData`，未加入时为 `null`。
- 当前待处理的大厅操作及其超时计时器。

网络回调只能修改这些状态并发布状态变化事件；不得直接操作 Unity UI。

当前 UI 所需的状态事件为：

- `onRoomRefresh(RoomListData)`：收到新的房间列表快照时触发。
- `onRoomInfoRefresh(RoomData)`：当前房间变化时触发；参数为 `null` 表示已离开房间。
- `onRequestFail(LobbyRequestType)`：主动创建、加入或离开请求失败时触发；失败可以
  来自服务端结果码或客户端超时。

一个客户端同一时刻只允许一个待处理大厅操作。发送主动请求后，新的操作直接忽略；若
500 ms 后仍未收到响应，则发布等待 Mask 事件并拦截 UI 操作。收到对应响应后，无论结果
是成功还是失败，都必须取消等待与超时计时器。超时后关闭当前连接并进入底层断线恢复，
在真正收到断线回调前保持请求门关闭，网络层也不再收发该连接的数据，防止迟到回包污染
下一次操作。底层断线不走 `onRequestFail`，由网络层统一显示断线提示，
`ClientLobbyManager` 只负责清空本地大厅状态。

### 4.2 客户端操作

客户端对 UI 暴露以下方法；连接、登录和断开由 `ClientLobbyManager` 生命周期及网络回调内部处理，不暴露 `Connect`、`LoginLobby` 或 `Disconnect` 公共 API：

- `CreateRoom(roomName)`：发送 `C2L_CreateRoom`。
- `JoinRoom(roomId)`：发送 `C2L_JoinRoom`。
- `LeaveRoom()`：发送 `C2L_LeaveRoom`。

客户端必须先完成内部登录并收到初始 `L2C_RefreshRoomList` 快照，才能发送任何房间操作。
UI 不直接构造或发送网络消息。

### 4.3 客户端消息处理

| 服务端消息 | 客户端状态变化 |
| --- | --- |
| `L2C_RefreshRoomList` | 替换房间列表，触发 `onRoomRefresh`。首次收到该快照后进入 `InLobby`。 |
| `L2C_CreateReturn` | 取消创建等待与超时计时器。`CreateSuccess` 时设置当前房间并进入 `InRoom`；其他结果触发 `onRequestFail`。 |
| `L2C_JoinReturn` | 取消加入等待与超时计时器。`JoinSuccess` 时设置当前房间并进入 `InRoom`；其他结果触发 `onRequestFail`。 |
| `L2C_LeaveReturn` | 取消离开等待与超时计时器。`LeaveSuccess` 时清空当前房间并进入 `InLobby`；其他结果触发 `onRequestFail`。 |
| `L2C_RefreshRoomInfo` | 替换当前房间详情，刷新成员、房主和人数。 |
| 底层断线事件 | 清空当前房间与房间列表，进入 `Disconnected`；断线提示由底层网络 UI 统一处理。 |

服务端消息都是权威快照。客户端不根据本地点击预先修改人数、房主或成员列表，必须等待服务端响应。

### 4.4 UI 边界

大厅 UI 通过项目的 MVC 框架订阅 `ClientLobbyManager` 状态：

- `LobbyUIController` 将房间列表状态写入 `LobbyUIModel`；`LobbyUI` 刷新搜索结果、
  房间列表，并将创建/加入点击意图交给 Controller。
- `LobbyRoomUIController` 将当前房间状态写入 `LobbyRoomUIModel`；`LobbyRoomUI` 刷新
  成员、人数、房主和离开按钮，并将离开点击意图交给 Controller。
- 当前房间从 `null` 变为非空时打开 `LobbyRoomUI`；变回 `null` 时关闭它。
- 两个 UI 不直接注册 TCP 协议回调、不直接发送网络消息，也不自行修改人数或房主。
- `onRequestFail` 由 Controller 转换为对应提示文本和等待状态；断线提示不由大厅业务 UI 处理。

当前阶段不实现准备、开始游戏和战斗入口，因此房间 UI 只提供成员展示与离开房间。

## 5. 大厅消息流

```text
客户端连接成功并被服务端 Accept
  -> C2L_LoginLobby
服务端验证身份并创建 Player
  <- L2C_RefreshRoomList（初始大厅快照）
（收到初始快照前不得发送房间操作）

客户端创建房间
  -> C2L_CreateRoom
  <- L2C_CreateReturn { type = CreateSuccess | CreateFail }
  <- L2C_RefreshRoomList（所有大厅客户端）

客户端加入房间
  -> C2L_JoinRoom
  <- L2C_JoinReturn { type = JoinSuccess | JoinFail | JoinRoomClosed | JoinRoomFull }（加入者）
  <- L2C_RefreshRoomInfo（原房间成员）
  <- L2C_RefreshRoomList（所有大厅客户端）

客户端离开或断线
  -> C2L_LeaveRoom（主动离开时）
  <- L2C_LeaveReturn { type = LeaveSuccess | LeaveFail }（主动离开者）
  <- L2C_RefreshRoomInfo（剩余成员，若房间仍存在）
  <- L2C_RefreshRoomList（所有大厅客户端，若状态变化）

客户端主动请求超时
  <- 本地触发 onRequestFail(CreateTimeout | JoinTimeout | LeaveTimeout)，并关闭连接
```

## 6. 当前实施顺序

1. 服务端提取并使用统一的移出房间方法。
2. 补齐房间参数和状态校验，并用 `LobbyRequestType` 返回失败原因。
3. 收紧房间列表与房间详情广播条件。
4. 完成 `ClientLobbyManager` 的全部大厅回包注册、当前房间更新、离开处理及主动请求超时清理。
5. 通过 `LobbyUIModel`、`LobbyRoomUIModel` 和对应 Controller 将大厅状态绑定到 UI。
