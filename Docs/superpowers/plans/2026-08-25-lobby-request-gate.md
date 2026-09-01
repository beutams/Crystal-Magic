# Lobby Request Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serialize client lobby create, join, and leave operations behind one request gate, with delayed waiting UI events and explicit failure results.

**Architecture:** `ClientLobbyManager` owns one `LobbyRequest` instance. A new command is ignored while that instance exists; a 500 ms timer publishes a waiting event, and each matched response or timeout clears both timers before publishing its success or failure event. The server returns operation-specific `LobbyRequestType` values so UI code can map a single failure type to text.

**Tech Stack:** Unity C#, existing `TimerManager`, existing TCP lobby protocol, Unity UI MVC event bridge.

**Spec:** `NetworkLobbyDesign.md`

## Global Constraints

- Keep the current public-field, `Action`-event coding style in `Assets/Scripts/Web/Lobby/`.
- Do not add request identifiers; one request remains active until it receives a response, and timeouts require transport recovery before another command.
- Do not make UI code send TCP messages or mutate lobby state.

---

### Task 1: Define request state and operation-specific protocol results

**Files:**
- Create: `Assets/Scripts/Web/Lobby/LobbyRequest.cs`
- Modify: `Assets/Scripts/Web/Lobby/LobbyMessageType.cs`
- Modify: `Assets/Scripts/Web/Lobby/ServerLobbyManager.cs`

**Interfaces:**
- Produces: `LobbyRequest` with `type`, `waitTimerId`, and `timeoutTimerId` fields.
- Produces: `LobbyRequestType` values for create, join, and leave requests, results, and timeouts.

- [ ] **Step 1: Add the request container**

```csharp
public class LobbyRequest
{
    public LobbyRequestType type;
    public long waitTimerId;
    public long timeoutTimerId;
}
```

- [ ] **Step 2: Replace generic result values with operation-specific values**

```csharp
CreateRequest, CreateSuccess, CreateFail, CreateTimeout,
JoinRequest, JoinSuccess, JoinFail, JoinRoomClosed, JoinRoomFull, JoinTimeout,
LeaveRequest, LeaveSuccess, LeaveFail, LeaveTimeout
```

- [ ] **Step 3: Return the corresponding result from every server create, join, and leave branch**

```csharp
connect.Send(new L2C_JoinReturn()
{
    type = LobbyRequestType.JoinRoomFull,
});
```

- [ ] **Step 4: Verify source references**

Run: `rg -n -S "LobbyRequestType\.(Success|Fail|RoomClosed|RoomFull|Timeout)" Assets/Scripts/Web/Lobby --glob '*.cs'`

Expected: no references remain.

### Task 2: Gate all client lobby commands and publish request UI events

**Files:**
- Modify: `Assets/Scripts/Web/Lobby/ClientLobbyManager.cs`

**Interfaces:**
- Consumes: `LobbyRequest` and operation-specific `LobbyRequestType` values from Task 1.
- Produces: `onRequestWaiting(LobbyRequestType)`, `onRequestFinished()`, and `onRequestFail(LobbyRequestType)` notifications.

- [ ] **Step 1: Add the single active request field and events**

```csharp
protected LobbyRequest request;
public Action<LobbyRequestType> onRequestWaiting;
public Action onRequestFinished;
```

- [ ] **Step 2: Implement `BeginRequest`, `FinishRequest`, and `OnRequestTimeout`**

```csharp
if (request != null)
    return false;
```

The waiting timer fires after 500 ms; all response paths cancel it and the timeout timer before clearing `request`.

- [ ] **Step 3: Route create, join, and leave through the request gate**

```csharp
if (!BeginRequest(LobbyRequestType.JoinRequest))
    return;

lobbyConnect.Send(new C2L_JoinRoom() { roomId = roomId });
```

- [ ] **Step 4: Accept only the response corresponding to the current request kind**

```csharp
if (!FinishRequest(LobbyRequestType.JoinRequest))
    return;
```

- [ ] **Step 5: Keep client room state authoritative**

Set `room` before publishing `onRoomInfoRefresh` for `L2C_RefreshRoomInfo`, and clear `room` before invoking leave events after `LeaveSuccess`.

### Task 3: Verify interaction paths and synchronize the design document

**Files:**
- Modify: `NetworkLobbyDesign.md`

**Interfaces:**
- Consumes: final operation-specific protocol result values and client request gate behavior.

- [ ] **Step 1: Update result-code and request-gate documentation**

Document the operation-specific `LobbyRequestType` values, the 500 ms waiting-mask threshold, ignored input while a request is active, and timeout transport recovery.

- [ ] **Step 2: Check for stale protocol terms**

Run: `rg -n -S "LobbyRequestType\.(Success|Fail|RoomClosed|RoomFull|Timeout)|bool success" NetworkLobbyDesign.md Assets/Scripts/Web/Lobby --glob '*.cs'`

Expected: no stale generic result references remain.

- [ ] **Step 3: Compile the solution**

Run: `dotnet build Crystal-Magic.sln --no-restore`

Expected: build succeeds with no errors.
