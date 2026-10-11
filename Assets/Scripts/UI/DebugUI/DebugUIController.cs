using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.UI
{
    public sealed class DebugUIController : UIControllerBase<DebugUI, DebugUIModel>
    {
        private const float UnitSpawnDistance = 4f;
        private float _nextMonsterCountRefresh;

        private readonly List<string> _registeredUnitNames = new();
        private readonly List<UnitDebugUnitEntry> _inspectorUnits = new();
        private readonly List<UnitDebugComponentEntry> _inspectorComponents = new();
        private readonly List<UnitDebugBuffEntry> _inspectorBuffs = new();

        public DebugUIController(DebugUI view, DebugUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Model.SetLocalAccountId(SteamComponent.Instance.LaunchConfig.accountId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            RefreshAccountInfo();
            Bindings.Bind(() => View.LocalAccountIdChanged += Model.SetLocalAccountId,
                () => View.LocalAccountIdChanged -= Model.SetLocalAccountId);
            Bindings.Bind(() => View.AccountSwitchRequested += HandleAccountSwitch,
                () => View.AccountSwitchRequested -= HandleAccountSwitch);
            Bindings.Bind(() => View.AccountReturnMenuRequested += HandleAccountReturnMenu,
                () => View.AccountReturnMenuRequested -= HandleAccountReturnMenu);
            Bindings.Bind(
                () => View.ContentToggleRequested += HandleContentToggleRequested,
                () => View.ContentToggleRequested -= HandleContentToggleRequested);
            Bindings.Bind(
                () => View.ContentHideRequested += HandleContentHideRequested,
                () => View.ContentHideRequested -= HandleContentHideRequested);
            Bindings.Bind(
                () => View.PageRequested += HandlePageRequested,
                () => View.PageRequested -= HandlePageRequested);
            Bindings.Bind(
                () => View.PreviousSpawnUnitRequested += HandlePreviousSpawnUnitRequested,
                () => View.PreviousSpawnUnitRequested -= HandlePreviousSpawnUnitRequested);
            Bindings.Bind(
                () => View.NextSpawnUnitRequested += HandleNextSpawnUnitRequested,
                () => View.NextSpawnUnitRequested -= HandleNextSpawnUnitRequested);
            Bindings.Bind(
                () => View.SpawnUnitRequested += HandleSpawnUnitRequested,
                () => View.SpawnUnitRequested -= HandleSpawnUnitRequested);
            Bindings.Bind(
                () => View.UnitInspectorRefreshRequested += HandleUnitInspectorRefreshRequested,
                () => View.UnitInspectorRefreshRequested -= HandleUnitInspectorRefreshRequested);
            Bindings.Bind(
                () => View.PreviousInspectorUnitRequested += HandlePreviousInspectorUnitRequested,
                () => View.PreviousInspectorUnitRequested -= HandlePreviousInspectorUnitRequested);
            Bindings.Bind(
                () => View.NextInspectorUnitRequested += HandleNextInspectorUnitRequested,
                () => View.NextInspectorUnitRequested -= HandleNextInspectorUnitRequested);
            Bindings.Bind(
                () => View.PreviousInspectorComponentRequested += HandlePreviousInspectorComponentRequested,
                () => View.PreviousInspectorComponentRequested -= HandlePreviousInspectorComponentRequested);
            Bindings.Bind(
                () => View.NextInspectorComponentRequested += HandleNextInspectorComponentRequested,
                () => View.NextInspectorComponentRequested -= HandleNextInspectorComponentRequested);
            Bindings.Bind(
                () => View.PreviousInspectorBuffRequested += HandlePreviousInspectorBuffRequested,
                () => View.PreviousInspectorBuffRequested -= HandlePreviousInspectorBuffRequested);
            Bindings.Bind(
                () => View.NextInspectorBuffRequested += HandleNextInspectorBuffRequested,
                () => View.NextInspectorBuffRequested -= HandleNextInspectorBuffRequested);
            Bindings.Bind(
                () => View.UnitInspectorApplyRequested += HandleUnitInspectorApplyRequested,
                () => View.UnitInspectorApplyRequested -= HandleUnitInspectorApplyRequested);
            Bindings.Bind(
                () => View.UnitInspectorAddBuffRequested += HandleUnitInspectorAddBuffRequested,
                () => View.UnitInspectorAddBuffRequested -= HandleUnitInspectorAddBuffRequested);
            Bindings.Bind(
                () => View.UnitInspectorRemoveBuffRequested += HandleUnitInspectorRemoveBuffRequested,
                () => View.UnitInspectorRemoveBuffRequested -= HandleUnitInspectorRemoveBuffRequested);
            Bindings.Bind(
                () => View.UnitInspectorClearBehaviorRequested += HandleUnitInspectorClearBehaviorRequested,
                () => View.UnitInspectorClearBehaviorRequested -= HandleUnitInspectorClearBehaviorRequested);
            Bindings.Bind(
                () => View.UnitInspectorClearStateScriptRequested += HandleUnitInspectorClearStateScriptRequested,
                () => View.UnitInspectorClearStateScriptRequested -= HandleUnitInspectorClearStateScriptRequested);
            BindEvent<UnitDamagedEvent>(Model.HandleUnitDamaged);
            _nextMonsterCountRefresh = 0f;
        }

        protected override void OnUpdate()
        {
            if (UnityEngine.Time.unscaledTime >= _nextMonsterCountRefresh)
            {
                _nextMonsterCountRefresh = UnityEngine.Time.unscaledTime + 0.5f;
                RefreshMonsterCounts();
                RefreshAccountInfo();
            }
            Model.RefreshRuntime();
        }

        private bool HasAccountBattleContext()
        {
            NetworkComponent network = NetworkComponent.Instance;
            return network.Role != NetworkRole.Client || network.clientBattleManager.HasPreBattleSnapshot ||
                network.clientBattleManager.HasPendingSettlement || network.clientBattleManager.battleConnect != null ||
                network.battleHostManager.IsHosting || network.clientLobbyManager.BattleControlActive;
        }

        private void RefreshAccountInfo()
        {
            if (!ClientAccountPolicy.DevelopmentAccountsAllowed) return;
            SteamComponent steam = SteamComponent.Instance;
            ClientAccountIdentity account = steam.Account;
            bool ready = !TransitionComponent.Instance.IsTransitioning && !HasAccountBattleContext();
            GameState state = GameFlowComponent.Instance.GetCurrentState();
            string mode = account.IsLocalTestAccount ? "本地测试 / Socket" : "Steam / Steam P2P";
            string identity = account.IsValid ? $"{account.Name}  (ID: {account.AccountId})" : "身份不可用，请检查 Steam 登录";
            string hint = HasAccountBattleContext() ? "联机战斗/恢复处理中：请先结算回城。" :
                state is MainMenuState ? "可切换。完成后需重新选择该账号的存档。" :
                state is TownState || state is TrainingState ? "先保存并返回主菜单，再选择账号。" : "请先正常结束地牢并返回城镇。";
            Model.SetAccountInfo($"当前：{mode}\n账号：{identity}\n大厅：{steam.LaunchConfig.lobbyAddress}:{steam.LaunchConfig.lobbyPort}\n{hint}",
                ready && state is MainMenuState, ready && (state is TownState || state is TrainingState));
        }

        private void HandleAccountSwitch(bool local)
        {
            RefreshAccountInfo();
            if (!Model.CanSwitchAccount) return;
            try
            {
                if (!SteamComponent.Instance.TrySwitchDebugAccount(local, Model.LocalAccountIdText, out string error))
                    Model.SetAccountStatus(error);
                else
                {
                    // A slot list opened before switching belongs to the old account.
                    UIComponent.Instance.CloseUI<LoadUI>();
                    UIComponent.Instance.CloseUI<SaveUI>();
                    Model.SetAccountStatus("切换成功，已断开旧连接并清空旧存档选择。\n请新建/加载此账号的存档再进大厅；多开测试请使用不同编号。\n已记住选择；启动命令行参数优先。");
                }
            }
            catch (System.Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                Model.SetAccountStatus("账号切换未完成：" + exception.Message);
            }
            RefreshAccountInfo();
        }

        private void HandleAccountReturnMenu()
        {
            RefreshAccountInfo();
            if (!Model.CanReturnForAccount) return;
            UIComponent.Instance.OpenChild<ConfirmUI>(View, new ConfirmUIOpenData("切换账号",
                "保存当前进度并返回主菜单？\n返回后再选择测试账号或 Steam。", () =>
                {
                    RefreshAccountInfo();
                    if (!Model.CanReturnForAccount) return;
                    if (SaveDataComponent.Instance.CurrentSaveIndex >= 0 && !SaveDataComponent.Instance.Save())
                    {
                        Model.SetAccountStatus("保存失败，已取消返回；当前账号未变。请检查 Console。");
                        return;
                    }
                    Model.SetAccountStatus("正在返回主菜单，账号尚未切换。");
                    GameFlowComponent.Instance.BeginTransition(new TransitionData
                    {
                        TargetSceneName = "MainMenu", TargetStateType = typeof(MainMenuState),
                        TransitionUIName = "TransitionUI", KeepCurrentMainScene = true,
                        ActiveSubSceneNames = System.Array.Empty<string>(),
                        OnLoadFailed = message => Model.SetAccountStatus("返回失败，账号未变：" + message),
                        OnComplete = () => { Model.SetAccountStatus("已回到主菜单，请选择测试账号或 Steam。原账号存档不受影响。"); RefreshAccountInfo(); },
                    });
                }));
        }

        private void RefreshMonsterCounts()
        {
            World world = GameWorldManager.GameWorld;
            if (world == null || GameWorldManager.SceneMode != GameSceneMode.Dungeon)
            {
                Model.ClearMonsterCounts();
                return;
            }

            bool isHost = false;
            if (GameWorldManager.Role == GameWorldRole.Client)
            {
                // The host's client world has already despawned sleeping monsters.
                // Read its authoritative battle world instead of counting client copies.
                BattleHostManager host = NetworkComponent.Instance.battleHostManager;
                string sessionId = NetworkComponent.Instance.clientBattleManager?.ConnectionInfo?.sessionId;
                if (host?.Runtime != null && !string.IsNullOrEmpty(sessionId))
                    foreach (BattleRoom room in host.Runtime.battleRooms.Values)
                    {
                        if (room.sessionId != sessionId || room.world?.World == null) continue;
                        world = room.world.World;
                        isHost = true;
                        break;
                    }
            }

            DungeonMonsterDistanceSystem distanceSystem = world.GetExistingSystemManaged<DungeonMonsterDistanceSystem>();
            if (distanceSystem != null)
            {
                using EntityQuery floorQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DungeonFloorControllerComponent>());
                if (floorQuery.IsEmptyIgnoreFilter)
                    Model.ClearMonsterCounts();
                else
                {
                    int Blocked(DungeonMonsterSleepBlockReason reason) => distanceSystem.GetBlockedCount(reason);
                    var settings = distanceSystem.Settings ?? ConfigComponent.Instance.Get<CrystalMagic.Game.Config.DungeonConfig>();
                    string diagnostics = $"活跃分布：≤{settings.MonsterWakeDistance:g} {distanceSystem.ActiveWithinWakeCount} | " +
                        $"缓冲圈 {distanceSystem.ActiveInBufferCount} | >{settings.MonsterSleepDistance:g} {distanceSystem.ActiveFarCount} | 观察者 {distanceSystem.ObserverCount}\n" +
                        $"远处：等待 {distanceSystem.WaitingToSleepCount} | Buff {Blocked(DungeonMonsterSleepBlockReason.Buff)} | 战斗 {Blocked(DungeonMonsterSleepBlockReason.Combat)} | 感知 {Blocked(DungeonMonsterSleepBlockReason.Perception)}\n" +
                        $"效果 {Blocked(DungeonMonsterSleepBlockReason.PendingEffect)} | 引用 {Blocked(DungeonMonsterSleepBlockReason.Referenced)} | 动作 {Blocked(DungeonMonsterSleepBlockReason.RunningAction)} | 受控 {Blocked(DungeonMonsterSleepBlockReason.Control)}\n" +
                        $"初始化 {Blocked(DungeonMonsterSleepBlockReason.Initializing)} | 受伤 {Blocked(DungeonMonsterSleepBlockReason.RecentDamage)} | 无玩家 {Blocked(DungeonMonsterSleepBlockReason.NoObserver)} | 关闭 {Blocked(DungeonMonsterSleepBlockReason.FeatureDisabled)}\n" +
                        $"休眠中仍需移动 {distanceSystem.DormantMovingCount} | 上次休眠检查 {distanceSystem.LastCheckMilliseconds:F2} ms";
                    Model.SetMonsterCounts(distanceSystem.ActiveCount, distanceSystem.SleepingCount, isHost, diagnostics);
                }
                return;
            }

            // Remote clients do not possess the sleeping population. Never display
            // zero sleeping / a full-map total calculated from their partial replica.
            using EntityQuery query = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<DungeonRuntimeOwnedEntity>(),
                    ComponentType.ReadOnly<UnitFactionComponent>(), ComponentType.ReadOnly<LocalTransform>() },
                None = new[] { ComponentType.ReadOnly<UnitDeathComponent>(), ComponentType.ReadOnly<DestroyEntityFlag>() },
            });
            using NativeArray<UnitFactionComponent> factions = query.ToComponentDataArray<UnitFactionComponent>(Allocator.Temp);
            int loaded = 0;
            foreach (UnitFactionComponent faction in factions)
                if (faction.Value == UnitFactionType.Enemy || faction.Value == UnitFactionType.Boss) loaded++;
            Model.SetMonsterCounts(loaded, null, false);
        }

        private void HandleContentToggleRequested()
        {
            Model.SetContentVisible(!Model.IsContentVisible);
        }

        private void HandleContentHideRequested()
        {
            Model.SetContentVisible(false);
        }

        private void HandlePageRequested(DebugPage page)
        {
            Model.SelectPage(page);

            if (page == DebugPage.UnitSpawner)
                RefreshRegisteredUnitNames();
            else if (page == DebugPage.UnitInspector)
                RefreshUnitInspector();
        }

        private void HandlePreviousSpawnUnitRequested() => Model.SelectPreviousSpawnUnit();

        private void HandleNextSpawnUnitRequested() => Model.SelectNextSpawnUnit();

        private void HandleSpawnUnitRequested()
        {
            if (!DebugComponent.Instance.IsEnabled ||
                !Model.IsTrainingGroundActive ||
                string.IsNullOrEmpty(Model.SelectedSpawnUnitName) ||
                !TryGetPlayerSpawnContext(out EntityManager entityManager, out float3 spawnPosition, out float2 spawnFacing))
            {
                return;
            }

            if (!EntitySpawnRegistryUtility.TryInstantiateUnit(
                    entityManager,
                    new FixedString128Bytes(Model.SelectedSpawnUnitName),
                    out Entity unitEntity))
            {
                return;
            }

            if (!entityManager.HasComponent<LocalTransform>(unitEntity))
                return;

            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(unitEntity);
            transform.Position = spawnPosition;
            transform.Rotation = quaternion.identity;
            entityManager.SetComponentData(unitEntity, transform);
            UnitFacingUtility.SetFacing(entityManager, unitEntity, -spawnFacing);
        }

        private void RefreshRegisteredUnitNames()
        {
            _registeredUnitNames.Clear();

            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
                EntitySpawnRegistryUtility.GetRegisteredUnitNames(world.EntityManager, _registeredUnitNames);

            Model.SetSpawnableUnitNames(_registeredUnitNames);
        }

        private static bool TryGetPlayerSpawnContext(
            out EntityManager entityManager,
            out float3 spawnPosition,
            out float2 spawnFacing)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                spawnPosition = default;
                spawnFacing = default;
                return false;
            }

            entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
                    continue;

                LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(entity);
                UnitFacingUtility.TryGetFacing(entityManager, entity, out spawnFacing);
                spawnPosition = playerTransform.Position + new float3(spawnFacing.x, spawnFacing.y, 0f) * UnitSpawnDistance;
                return true;
            }

            spawnPosition = default;
            spawnFacing = default;
            return false;
        }

        private void HandleUnitInspectorRefreshRequested()
        {
            RefreshUnitInspector();
        }

        private void HandlePreviousInspectorUnitRequested()
        {
            if (Model.SelectPreviousInspectorUnit())
                RefreshSelectedInspectorData();
        }

        private void HandleNextInspectorUnitRequested()
        {
            if (Model.SelectNextInspectorUnit())
                RefreshSelectedInspectorData();
        }

        private void HandlePreviousInspectorComponentRequested()
        {
            if (Model.SelectPreviousInspectorComponent())
                RefreshSelectedInspectorData();
        }

        private void HandleNextInspectorComponentRequested()
        {
            if (Model.SelectNextInspectorComponent())
                RefreshSelectedInspectorData();
        }

        private void HandlePreviousInspectorBuffRequested()
        {
            if (Model.SelectPreviousInspectorBuff())
                RefreshSelectedInspectorData();
        }

        private void HandleNextInspectorBuffRequested()
        {
            if (Model.SelectNextInspectorBuff())
                RefreshSelectedInspectorData();
        }

        private void HandleUnitInspectorApplyRequested(string text)
        {
            if (!TryGetInspectorContext(out EntityManager entityManager, out Entity entity, out UnitDebugComponentEntry component))
                return;

            UnitDebugInspectorUtility.ApplyEditorText(
                entityManager,
                entity,
                component.Kind,
                Model.SelectedInspectorBuffIndex,
                text,
                out string message);
            RefreshSelectedInspectorData(message);
        }

        private void HandleUnitInspectorAddBuffRequested(string text)
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            UnitDebugInspectorUtility.AddBuff(entityManager, entity, text, out string message);
            RefreshSelectedInspectorData(message);
        }

        private void HandleUnitInspectorRemoveBuffRequested()
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            UnitDebugInspectorUtility.RemoveSelectedBuff(entityManager, entity, Model.SelectedInspectorBuffIndex, out string message);
            RefreshSelectedInspectorData(message);
        }

        private void HandleUnitInspectorClearBehaviorRequested()
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            bool cleared = UnitDebugInspectorUtility.ClearBehaviorTree(entityManager, entity);
            RefreshSelectedInspectorData(cleared ? "Behavior tree cleared." : "No behavior tree is attached.");
        }

        private void HandleUnitInspectorClearStateScriptRequested()
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            bool cleared = UnitDebugInspectorUtility.ClearStateScript(entityManager, entity);
            RefreshSelectedInspectorData(cleared ? "State script cleared." : "No state script is attached.");
        }

        private void RefreshUnitInspector()
        {
            if (!DebugComponent.Instance.IsEnabled || !Model.IsTrainingGroundActive)
            {
                Model.SetUnitInspectorDetail("Unit Inspector is available only while Debug is enabled in the training ground.");
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Model.SetUnitInspectorDetail("Runtime world is not available.");
                return;
            }

            UnitDebugInspectorUtility.GetUnits(world.EntityManager, _inspectorUnits);
            Model.SetInspectorUnits(_inspectorUnits);
            RefreshSelectedInspectorData();
        }

        private void RefreshSelectedInspectorData(string status = "")
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            UnitDebugInspectorUtility.GetComponents(entityManager, entity, _inspectorComponents);
            Model.SetInspectorComponents(_inspectorComponents);
            UnitDebugInspectorUtility.GetBuffs(entityManager, entity, _inspectorBuffs);
            Model.SetInspectorBuffs(_inspectorBuffs);

            if (!Model.TryGetSelectedInspectorComponent(out UnitDebugComponentEntry component))
            {
                Model.SetUnitInspectorDetail("No supported Component is attached to this unit.", status);
                return;
            }

            Model.SetUnitInspectorDetail(
                UnitDebugInspectorUtility.BuildEditorText(
                    entityManager,
                    entity,
                    component.Kind,
                    Model.SelectedInspectorBuffIndex),
                status);
        }

        private bool TryGetInspectorContext(
            out EntityManager entityManager,
            out Entity entity,
            out UnitDebugComponentEntry component)
        {
            if (TryGetInspectorEntity(out entityManager, out entity) && Model.TryGetSelectedInspectorComponent(out component))
                return true;

            component = default;
            return false;
        }

        private bool TryGetInspectorEntity(out EntityManager entityManager, out Entity entity)
        {
            entityManager = default;
            entity = Entity.Null;
            if (!DebugComponent.Instance.IsEnabled ||
                !Model.IsTrainingGroundActive ||
                !Model.TryGetSelectedInspectorUnit(out UnitDebugUnitEntry unit))
            {
                return false;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || !world.EntityManager.Exists(unit.Entity))
                return false;

            entityManager = world.EntityManager;
            entity = unit.Entity;
            return true;
        }
    }
}
