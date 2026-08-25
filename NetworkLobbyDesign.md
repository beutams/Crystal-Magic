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
- 服务端创建临时玩家与房间。
- 创建房间、加入房间、主动离房、断线离房。
- 房主断线时转移房主。
- 空房间删除。

## 2. 服务端大厅状态

### 2.1 Player

每个已连接客户端对应一个 `Player`：

- `userId`：服务端分配的玩家标识。当前为临时自增 Id，后续登录系统接入后替换为账号 Id。
- `username`：大厅展示名称。
- `roomId`：当前房间 Id；`-1` 表示未加入房间。
- `connect`：该玩家当前的网络连接。

### 2.2 Room

每个房间包含：

- `roomId`
- `roomName`
- `ownerId`
- `maxNum`
- `players`

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

- 创建成功：`L2C_CreateReturn { success = true, roomData }`。
- 创建失败：`L2C_CreateReturn { success = false }`。
- 加入成功或失败：`L2C_JoinReturn`。
- 离开成功或失败：`L2C_LeaveReturn`。

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

- 客户端刚建立 TCP 连接。
- 登录大厅时：只向该客户端单独发送当前房间列表。
- 创建、加入、离开失败。
- 不在房间的客户端断线。

`L2C_RefreshRoomInfo`：

- 房间创建后，发送给创建者。
- 玩家加入、主动离开或断线后，发送给该房间全部剩余成员。
- 房主发生转移后，发送给该房间全部剩余成员。

## 4. 客户端大厅设计

### 4.1 ClientLobbyManager

`ClientLobbyManager` 是客户端大厅的唯一状态入口。它持有 `ClientService`，注册大厅协议回调，并维护下列本地状态：

- `LobbyConnectionState`：`Disconnected`、`Connecting`、`InLobby`、`InRoom`。
- 当前玩家 Id 与展示名。
- 房间列表：`RoomListData`。
- 当前所在房间：`RoomData`，未加入时为 `null`。
- 最近一次操作错误或提示文本。

网络回调只能修改这些状态并发布状态变化事件；不得直接操作 Unity UI。

### 4.2 客户端操作

客户端对 UI 暴露以下方法：

- `Connect(endpoint)`：建立 TCP 连接。
- `LoginLobby()`：连接成功后发送 `C2L_LoginLobby`。
- `CreateRoom(roomName)`：发送 `C2L_CreateRoom`。
- `JoinRoom(roomId)`：发送 `C2L_JoinRoom`。
- `LeaveRoom()`：发送 `C2L_LeaveRoom`。
- `Disconnect()`：关闭本地连接并清理大厅状态。

UI 不直接构造或发送网络消息。

### 4.3 客户端消息处理

| 服务端消息 | 客户端状态变化 |
| --- | --- |
| `L2C_RefreshRoomList` | 替换房间列表，通知房间列表 UI 刷新。 |
| `L2C_CreateReturn` | 成功时设置当前房间并进入 `InRoom`；失败时记录错误。 |
| `L2C_JoinReturn` | 成功时设置当前房间并进入 `InRoom`；失败时记录错误。 |
| `L2C_LeaveReturn` | 成功时清空当前房间并进入 `InLobby`；失败时记录错误。 |
| `L2C_RefreshRoomInfo` | 替换当前房间详情，刷新成员、房主和人数。 |
| 底层断线事件 | 清空当前房间与房间列表，进入 `Disconnected`，通知 UI 显示断线状态。 |

服务端消息都是权威快照。客户端不根据本地点击预先修改人数、房主或成员列表，必须等待服务端响应。

### 4.4 UI 边界

大厅 UI 只订阅 `ClientLobbyManager` 状态：

- 房间列表变化：刷新搜索结果和房间列表。
- 当前房间变化：刷新成员、人数、房主和离开按钮。
- 状态变化：显示连接中、已连接、断线或操作失败提示。

当前阶段不实现准备、开始游戏和战斗入口，因此房间 UI 只提供成员展示与离开房间。

## 5. 大厅消息流

```text
客户端连接成功
  -> C2L_LoginLobby
  <- L2C_RefreshRoomList

客户端创建房间
  -> C2L_CreateRoom
  <- L2C_CreateReturn
  <- L2C_RefreshRoomList（所有大厅客户端）

客户端加入房间
  -> C2L_JoinRoom
  <- L2C_JoinReturn（加入者）
  <- L2C_RefreshRoomInfo（原房间成员）
  <- L2C_RefreshRoomList（所有大厅客户端）

客户端离开或断线
  -> C2L_LeaveRoom（主动离开时）
  <- L2C_LeaveReturn（主动离开者）
  <- L2C_RefreshRoomInfo（剩余成员，若房间仍存在）
  <- L2C_RefreshRoomList（所有大厅客户端，若状态变化）
```

## 6. 当前实施顺序

1. 服务端提取并使用统一的移出房间方法。
2. 补齐创建失败、房间参数和状态校验。
3. 收紧房间列表与房间详情广播条件。
4. 实现 `ClientLobbyManager` 的状态和协议处理。
5. 将大厅 UI 绑定到 `ClientLobbyManager` 状态。
