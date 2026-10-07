using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(ServerNetworkEntitySpawnCollectSystem))]
public partial class ServerNetworkStateCollectSystem : SystemBase
{
    private BattleSimulationScope _scope;
    private readonly List<NetworkStateData> _states = new();
    private EntityQuery _presentationEventsQuery;
    private Predicate<Entity> _isMissingEntity;
    private static readonly ProfilerMarker PlayerCollectMarker = new("Network.Server.CollectPlayerStates");
    private static readonly ProfilerMarker WorldCollectMarker = new("Network.Server.CollectWorldStates");
    private static readonly ProfilerMarker SnapshotCollectMarker = new("Network.Server.CollectSnapshot");
    private readonly HashSet<Entity> _despawnedEntities = new();
    private int _frameInterval = 33;
    private readonly List<Action<uint, NetworkEntitySpawnInfo[], List<NetworkStateData>>> _snapshotCallbacks = new();

    protected override void OnCreate()
    {
        _presentationEventsQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkPresentationEventQueueComponent>());
        _isMissingEntity = entity => !EntityManager.Exists(entity);
    }

    public void ResetScene()
    {
        _states.Clear();
        _despawnedEntities.Clear();
        _snapshotCallbacks.Clear();
    }

    public void RequestSnapshot(Action<uint, NetworkEntitySpawnInfo[], List<NetworkStateData>> callback)
    {
        if (callback != null)
        {
            _snapshotCallbacks.Add(callback);
        }
    }

    protected override void OnUpdate()
    {
        if (!FrameManagerUtility.TryGet(EntityManager, out ServerFrameManager frame) || !frame.running)
            return;

        _frameInterval = frame.frameInterval;
        SystemAPI.TryGetSingleton(out _scope);
        bool playerStep = _scope.Pass == BattleSimulationPass.Players;
        uint currentFrame = playerStep || frame.currentFrame == 0 ? frame.currentFrame : frame.currentFrame - 1;
        List<NetworkStateData> states = _states;
        states.Clear();
        CollectStates(states, currentFrame, true);
        CollectPresentationEvents(states);

        // 拾取等跨实体操作也会发生在玩家帧；销毁前立即记录，且同一实体只通知一次。
        if (_despawnedEntities.Count > 0)
            _despawnedEntities.RemoveWhere(_isMissingEntity);
        CollectDespawnStates(states);

        if (states.Count > 0)
        {
            if (!frame.sendOrder.TryGetValue(currentFrame, out Queue<NetworkStateData> queue))
            {
                queue = new Queue<NetworkStateData>();
                frame.sendOrder.Add(currentFrame, queue);
            }

            for (int index = 0; index < states.Count; index++)
            {
                queue.Enqueue(states[index]);
                if (states[index] is NetworkVitalityStateData or NetworkPresentationEventStateData or
                    NetworkEntitySpawnStateData or NetworkEntityDespawnStateData)
                    frame.RequestImmediateFlush();
            }
        }

        states.Clear();
        if (playerStep || _snapshotCallbacks.Count == 0)
        {
            return;
        }

        List<Action<uint, NetworkEntitySpawnInfo[], List<NetworkStateData>>> callbacks = new(_snapshotCallbacks);
        _snapshotCallbacks.Clear();
        List<NetworkStateData> snapshotStates = new();
        CollectStates(snapshotStates, currentFrame, false);
        NetworkEntitySpawnInfo[] entityInfos = NetworkEntitySpawnUtility.CreateSnapshotInfos(EntityManager);
        for (int index = 0; index < callbacks.Count; index++)
        {
            callbacks[index](currentFrame, entityInfos, snapshotStates);
        }
    }

    private void CollectStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        ProfilerMarker marker = !onlyDirty ? SnapshotCollectMarker
            : _scope.Pass == BattleSimulationPass.Players ? PlayerCollectMarker : WorldCollectMarker;
        using var profile = marker.Auto();
        CollectMoveStates(states, onlyDirty);
        CollectFacingStates(states, onlyDirty);
        CollectAnimationStates(states, currentFrame, onlyDirty);
        CollectVitalityStates(states, onlyDirty);
        CollectManaStates(states, onlyDirty);
        CollectBuffStates(states, currentFrame, onlyDirty);
        CollectControlStates(states, currentFrame, onlyDirty);
        CollectBattlePlayerStatusStates(states, onlyDirty);
        CollectDeathStates(states, onlyDirty);
        CollectInteractableStates(states, onlyDirty);
        CollectTreasureStates(states, onlyDirty);
        CollectProjectileStates(states, onlyDirty);
        CollectPlayerPropCooldownStates(states, currentFrame, onlyDirty);
        CollectPlayerSkillChainStates(states, onlyDirty);
        CollectCharacterStates(states, onlyDirty);
    }

    private void CollectBattlePlayerStatusStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<BattlePlayerStatusComponent> statusRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<BattlePlayerStatusComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectBattlePlayerStatusState(states, onlyDirty, identityRef, statusRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<BattlePlayerStatusComponent> statusRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<BattlePlayerStatusComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectBattlePlayerStatusState(states, onlyDirty, identityRef, statusRef);
        }
    }

    private void CollectBattlePlayerStatusState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<BattlePlayerStatusComponent> statusRef)
    {
        BattlePlayerStatusComponent status = statusRef.ValueRO;
        if ((onlyDirty && status.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkBattlePlayerStatusStateData
        {
            unitId = identityRef.ValueRO.id,
            lifeState = status.LifeState,
            connectionState = status.ConnectionState,
            transitionReady = status.TransitionReady,
        });
        if (onlyDirty)
        {
            status.NetworkDirty = 0;
            statusRef.ValueRW = status;
        }
    }

    private void CollectCharacterStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((PlayerCharacterComponent character, RefRO<NetworkIdentityComponent> identity) in
                     SystemAPI.Query<PlayerCharacterComponent, RefRO<NetworkIdentityComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectCharacterState(states, onlyDirty, character, identity);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((PlayerCharacterComponent character, RefRO<NetworkIdentityComponent> identity) in
                     SystemAPI.Query<PlayerCharacterComponent, RefRO<NetworkIdentityComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectCharacterState(states, onlyDirty, character, identity);
        }
    }

    private void CollectCharacterState(List<NetworkStateData> states, bool onlyDirty,
        PlayerCharacterComponent character, RefRO<NetworkIdentityComponent> identity)
    {
        if ((onlyDirty && character.NetworkDirty == 0) || identity.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkCharacterStateData
        {
            unitId = identity.ValueRO.id,
            revision = character.Revision,
            characterData = PlayerCharacterUtility.Clone(character.Data),
        });
        if (onlyDirty)
            character.NetworkDirty = 0;
    }

    private void CollectPresentationEvents(List<NetworkStateData> states)
    {
        EntityQuery query = _presentationEventsQuery;
        if (query.IsEmptyIgnoreFilter)
            return;

        NetworkPresentationEventQueueComponent queue =
            EntityManager.GetComponentObject<NetworkPresentationEventQueueComponent>(query.GetSingletonEntity());
        if (queue == null || queue.Events.Count == 0)
            return;

        states.AddRange(queue.Events);
        queue.Events.Clear();
    }

    private void CollectMoveStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<UnitMoveComponent> moveRef,
                  RefRO<LocalTransform> transformRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitMoveComponent>, RefRO<LocalTransform>>()
                     .WithAll<PlayerInputComponent>())
                CollectMoveState(states, onlyDirty, identityRef, moveRef, transformRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<UnitMoveComponent> moveRef,
                  RefRO<LocalTransform> transformRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitMoveComponent>, RefRO<LocalTransform>>()
                     .WithNone<PlayerInputComponent>())
                CollectMoveState(states, onlyDirty, identityRef, moveRef, transformRef);
        }
    }

    private void CollectMoveState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitMoveComponent> moveRef, RefRO<LocalTransform> transformRef)
    {
        UnitMoveComponent move = moveRef.ValueRO;
        if ((onlyDirty && move.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        LocalTransform transform = transformRef.ValueRO;
        states.Add(NetworkUnitStateSnapshotUtility.CreateMoveState(
            identityRef.ValueRO.id,
            move,
            transform));
        if (onlyDirty)
        {
            move.NetworkDirty = 0;
            moveRef.ValueRW = move;
        }
    }

    private void CollectFacingStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitFacingComponent> facingRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitFacingComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectFacingState(states, onlyDirty, identityRef, facingRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitFacingComponent> facingRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitFacingComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectFacingState(states, onlyDirty, identityRef, facingRef);
        }
    }

    private void CollectFacingState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitFacingComponent> facingRef)
    {
        UnitFacingComponent facing = facingRef.ValueRO;
        if ((onlyDirty && facing.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(NetworkUnitStateSnapshotUtility.CreateFacingState(
            identityRef.ValueRO.id,
            facing));
        if (onlyDirty)
        {
            facing.NetworkDirty = 0;
            facingRef.ValueRW = facing;
        }
    }

    private void CollectAnimationStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<UnitAnimationComponent> animationRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitAnimationComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectAnimationState(states, currentFrame, onlyDirty, identityRef, animationRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<UnitAnimationComponent> animationRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitAnimationComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectAnimationState(states, currentFrame, onlyDirty, identityRef, animationRef);
        }
    }

    private void CollectAnimationState(List<NetworkStateData> states, uint currentFrame, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitAnimationComponent> animationRef)
    {
        UnitAnimationComponent animation = animationRef.ValueRO;
        if ((onlyDirty && animation.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        if (onlyDirty)
            animation.StartFrame = currentFrame;

        states.Add(new NetworkAnimationStateData
        {
            unitId = identityRef.ValueRO.id,
            animationName = animation.AnimationName.ToString(),
            startFrame = animation.StartFrame,
            sequence = animation.Sequence,
        });
        if (onlyDirty)
        {
            animation.NetworkDirty = 0;
            animationRef.ValueRW = animation;
        }
    }

    private void CollectVitalityStates(List<NetworkStateData> states, bool onlyDirty)
    {
        // World-pass projectile impacts can damage players after their tick.
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitVitalityComponent> vitalityRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitVitalityComponent>>()
                 .WithAll<PlayerInputComponent>())
            CollectVitalityState(states, onlyDirty, identityRef, vitalityRef);
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitVitalityComponent> vitalityRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitVitalityComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectVitalityState(states, onlyDirty, identityRef, vitalityRef);
        }
    }

    private void CollectVitalityState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitVitalityComponent> vitalityRef)
    {
        UnitVitalityComponent vitality = vitalityRef.ValueRO;
        if ((onlyDirty && vitality.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkVitalityStateData
        {
            unitId = identityRef.ValueRO.id,
            baseMaxHealth = vitality.BaseMaxHealth,
            baseMaxHealthOffset = vitality.BaseMaxHealthOffset,
            currentHealth = vitality.CurrentHealth,
            baseHealthRegenPerSecond = vitality.BaseHealthRegenPerSecond,
            baseHealthRegenOffset = vitality.BaseHealthRegenOffset,
            baseDefense = vitality.BaseDefense,
            baseDefenseOffset = vitality.BaseDefenseOffset,
        });
        if (onlyDirty)
        {
            vitality.NetworkDirty = 0;
            vitalityRef.ValueRW = vitality;
        }
    }

    private void CollectManaStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitManaComponent> manaRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitManaComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectManaState(states, onlyDirty, identityRef, manaRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitManaComponent> manaRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitManaComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectManaState(states, onlyDirty, identityRef, manaRef);
        }
    }

    private void CollectManaState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitManaComponent> manaRef)
    {
        UnitManaComponent mana = manaRef.ValueRO;
        if ((onlyDirty && mana.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkManaStateData
        {
            unitId = identityRef.ValueRO.id,
            baseMaxMp = mana.BaseMaxMp,
            baseMaxMpOffset = mana.BaseMaxMpOffset,
            currentMana = mana.CurrentMana,
            baseMpRegenPerSecond = mana.BaseMpRegenPerSecond,
            baseMpRegenPerSecondOffset = mana.BaseMpRegenPerSecondOffset,
        });
        if (onlyDirty)
        {
            mana.NetworkDirty = 0;
            manaRef.ValueRW = mana;
        }
    }

    private void CollectBuffStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitBuffComponent> buffComponentRef,
                     DynamicBuffer<UnitBuffElement> buffs) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitBuffComponent>, DynamicBuffer<UnitBuffElement>>()
                     .WithAll<PlayerInputComponent>())
                CollectBuffState(states, currentFrame, onlyDirty, identityRef, buffComponentRef, buffs);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitBuffComponent> buffComponentRef,
                     DynamicBuffer<UnitBuffElement> buffs) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitBuffComponent>, DynamicBuffer<UnitBuffElement>>()
                     .WithNone<PlayerInputComponent>())
                CollectBuffState(states, currentFrame, onlyDirty, identityRef, buffComponentRef, buffs);
        }
    }

    private void CollectBuffState(List<NetworkStateData> states, uint currentFrame, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitBuffComponent> buffComponentRef, DynamicBuffer<UnitBuffElement> buffs)
    {
        if ((onlyDirty && buffComponentRef.ValueRO.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        NetworkBuffStateData state = new() { unitId = identityRef.ValueRO.id };
        for (int index = 0; index < buffs.Length; index++)
        {
            UnitBuffElement buff = buffs[index];
            state.buffs.Add(new NetworkBuffEntryStateData
            {
                buffId = buff.BuffId,
                endFrame = GetEndFrame(buff.RemainingTime, currentFrame),
                stackCount = buff.StackCount,
                originUnitId = GetNetworkId(buff.OriginEntity),
                sourceSkillId = buff.SourceSkillId,
            });
        }

        states.Add(state);
        if (onlyDirty)
        {
            buffComponentRef.ValueRW.NetworkDirty = 0;
        }
    }

    private void CollectControlStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitControlRuntimeComponent> controlRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitControlRuntimeComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectControlState(states, currentFrame, onlyDirty, identityRef, controlRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitControlRuntimeComponent> controlRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitControlRuntimeComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectControlState(states, currentFrame, onlyDirty, identityRef, controlRef);
        }
    }

    private void CollectControlState(List<NetworkStateData> states, uint currentFrame, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitControlRuntimeComponent> controlRef)
    {
        UnitControlRuntimeComponent control = controlRef.ValueRO;
        if ((onlyDirty && control.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(NetworkControlStateData.Capture(EntityManager, identityRef.ValueRO.id,
            control, currentFrame, _frameInterval));
        if (onlyDirty)
        {
            control.NetworkDirty = 0;
            controlRef.ValueRW = control;
        }
    }

    private void CollectDeathStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitDeathComponent> deathRef, Entity entity) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitDeathComponent>>()
                     .WithAll<PlayerInputComponent>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
                CollectDeathState(states, onlyDirty, identityRef, deathRef, entity);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitDeathComponent> deathRef, Entity entity) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitDeathComponent>>()
                     .WithNone<PlayerInputComponent>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
                CollectDeathState(states, onlyDirty, identityRef, deathRef, entity);
        }
    }

    private void CollectDeathState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitDeathComponent> deathRef, Entity entity)
    {
        UnitDeathComponent death = deathRef.ValueRO;
        if ((onlyDirty && death.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkDeathStateData
        {
            unitId = identityRef.ValueRO.id,
            // 联机玩家死亡后保留实体并进入观战状态，不能触发客户端的死亡销毁表现。
            isDead = EntityManager.HasComponent<BattlePlayerStatusComponent>(entity)
                ? (byte)0
                : EntityManager.IsComponentEnabled<UnitDeathComponent>(entity) ? (byte)1 : (byte)0,
        });
        if (onlyDirty)
        {
            death.NetworkDirty = 0;
            deathRef.ValueRW = death;
        }
    }

    private void CollectInteractableStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitInteractableComponent> interactableRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitInteractableComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectInteractableState(states, onlyDirty, identityRef, interactableRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitInteractableComponent> interactableRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitInteractableComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectInteractableState(states, onlyDirty, identityRef, interactableRef);
        }
    }

    private void CollectInteractableState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitInteractableComponent> interactableRef)
    {
        UnitInteractableComponent interactable = interactableRef.ValueRO;
        if ((onlyDirty && interactable.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkInteractableStateData
        {
            unitId = identityRef.ValueRO.id,
            kind = interactable.Data.Kind,
            dataId = interactable.Data.DataId,
            amount = interactable.Data.Amount,
            variant = interactable.Data.Variant,
            rangeSq = interactable.RangeSq,
            isEnabled = interactable.IsEnabled,
        });
        if (onlyDirty)
        {
            interactable.NetworkDirty = 0;
            interactableRef.ValueRW = interactable;
        }
    }

    private void CollectTreasureStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<TreasureComponent> treasureRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<TreasureComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectTreasureState(states, onlyDirty, identityRef, treasureRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<TreasureComponent> treasureRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<TreasureComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectTreasureState(states, onlyDirty, identityRef, treasureRef);
        }
    }

    private void CollectTreasureState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<TreasureComponent> treasureRef)
    {
        TreasureComponent treasure = treasureRef.ValueRO;
        if ((onlyDirty && treasure.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkTreasureStateData
        {
            unitId = identityRef.ValueRO.id,
            regionId = treasure.RegionId,
            randomSeed = treasure.RandomSeed,
            interestSize = treasure.InterestSize,
            quality = treasure.Quality,
            isOpened = treasure.IsOpened,
        });
        if (onlyDirty)
        {
            treasure.NetworkDirty = 0;
            treasureRef.ValueRW = treasure;
        }
    }

    private void CollectProjectileStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<SkillProjectileComponent> projectileRef,
                  RefRO<LocalTransform> transformRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<SkillProjectileComponent>, RefRO<LocalTransform>>()
                     .WithAll<PlayerInputComponent>())
                CollectProjectileState(states, onlyDirty, identityRef, projectileRef, transformRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<SkillProjectileComponent> projectileRef,
                  RefRO<LocalTransform> transformRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<SkillProjectileComponent>, RefRO<LocalTransform>>()
                     .WithNone<PlayerInputComponent>())
                CollectProjectileState(states, onlyDirty, identityRef, projectileRef, transformRef);
        }
    }

    private void CollectProjectileState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<SkillProjectileComponent> projectileRef, RefRO<LocalTransform> transformRef)
    {
        SkillProjectileComponent projectile = projectileRef.ValueRO;
        if ((onlyDirty && projectile.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        LocalTransform transform = transformRef.ValueRO;
        states.Add(new NetworkProjectileStateData
        {
            identity = projectile.Identity,
            hitSequence = projectile.HitSequence,
            ended = projectile.Ended,
            repeatHitIntervalSeconds = projectile.RepeatHitIntervalSeconds,
            unitId = identityRef.ValueRO.id,
            directionX = projectile.Direction.x,
            directionY = projectile.Direction.y,
            directionZ = projectile.Direction.z,
            speed = projectile.Speed,
            maxRange = projectile.MaxRange,
            traveledDistance = projectile.TraveledDistance,
            hitRadius = projectile.HitRadius,
            canPierce = projectile.CanPierce,
            triggerDestroyEffectsOnMaxRange = projectile.TriggerDestroyEffectsOnMaxRange,
            positionX = transform.Position.x,
            positionY = transform.Position.y,
            positionZ = transform.Position.z,
        });
        if (onlyDirty)
        {
            projectile.NetworkDirty = 0;
            projectileRef.ValueRW = projectile;
        }
    }

    private void CollectPlayerPropCooldownStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<PlayerPropCooldownComponent> cooldownRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<PlayerPropCooldownComponent>>()
                     .WithAll<PlayerInputComponent>())
                CollectPlayerPropCooldownState(states, currentFrame, onlyDirty, identityRef, cooldownRef);
        }
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.Players)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<PlayerPropCooldownComponent> cooldownRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<PlayerPropCooldownComponent>>()
                     .WithNone<PlayerInputComponent>())
                CollectPlayerPropCooldownState(states, currentFrame, onlyDirty, identityRef, cooldownRef);
        }
    }

    private void CollectPlayerPropCooldownState(List<NetworkStateData> states, uint currentFrame, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<PlayerPropCooldownComponent> cooldownRef)
    {
        PlayerPropCooldownComponent cooldown = cooldownRef.ValueRO;
        if ((onlyDirty && cooldown.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkPlayerPropCooldownStateData
        {
            unitId = identityRef.ValueRO.id,
            endFrame = GetEndFrame(cooldown.SharedCooldownRemaining, currentFrame),
        });
        if (onlyDirty)
        {
            cooldown.NetworkDirty = 0;
            cooldownRef.ValueRW = cooldown;
        }
    }

    private void CollectPlayerSkillChainStates(List<NetworkStateData> states, bool onlyDirty)
    {
        if (!onlyDirty || _scope.Pass != BattleSimulationPass.World)
        {
            foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<PlayerInputComponent> inputRef) in
                     SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<PlayerInputComponent>>())
                CollectPlayerSkillChainState(states, onlyDirty, identityRef, inputRef);
        }
    }

    private void CollectPlayerSkillChainState(List<NetworkStateData> states, bool onlyDirty,
        RefRO<NetworkIdentityComponent> identityRef, RefRW<PlayerInputComponent> inputRef)
    {
        PlayerInputComponent input = inputRef.ValueRO;
        if ((onlyDirty && input.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
            return;

        states.Add(new NetworkPlayerSkillChainStateData
        {
            unitId = identityRef.ValueRO.id,
            skillChainIndex = input.SkillChainIndex,
        });
        if (onlyDirty)
        {
            input.NetworkDirty = 0;
            inputRef.ValueRW = input;
        }
    }

    private void CollectDespawnStates(List<NetworkStateData> states)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  EnabledRefRO<DestroyEntityFlag> _,
                  Entity entity) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, EnabledRefRO<DestroyEntityFlag>>()
                     .WithEntityAccess())
        {
            Guid unitId = identityRef.ValueRO.id;
            if (unitId == Guid.Empty || !_despawnedEntities.Add(entity))
                continue;

            bool isDead = EntityManager.HasComponent<UnitDeathComponent>(entity) &&
                          EntityManager.IsComponentEnabled<UnitDeathComponent>(entity);
            if (isDead)
            {
                states.Add(new NetworkDeathStateData
                {
                    unitId = unitId,
                    isDead = 1,
                });
            }

            if (EntityManager.HasComponent<UnitFacingComponent>(entity))
            {
                states.Add(NetworkUnitStateSnapshotUtility.CreateFacingState(
                    unitId,
                    EntityManager.GetComponentData<UnitFacingComponent>(entity)));
            }

            NetworkEntityDespawnStateData despawn = new()
            {
                unitId = unitId,
                waitForDeathPresentation = isDead ? (byte)1 : (byte)0,
            };
            if (EntityManager.HasComponent<LocalTransform>(entity))
            {
                float3 position = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                despawn.hasPosition = true;
                despawn.positionX = position.x;
                despawn.positionY = position.y;
                despawn.positionZ = position.z;
            }
            states.Add(despawn);
        }
    }

    private Guid GetNetworkId(Entity entity)
    {
        if (entity == Entity.Null ||
            !EntityManager.Exists(entity) ||
            !EntityManager.HasComponent<NetworkIdentityComponent>(entity))
        {
            return Guid.Empty;
        }

        return EntityManager.GetComponentData<NetworkIdentityComponent>(entity).id;
    }

    private uint GetEndFrame(float remainingTime, uint currentFrame)
    {
        if (remainingTime < 0f)
            return uint.MaxValue;

        double frameCount = Math.Ceiling(remainingTime * 1000d / Math.Max(1, _frameInterval));
        uint offset = frameCount >= uint.MaxValue ? uint.MaxValue : (uint)frameCount;
        return uint.MaxValue - currentFrame <= offset ? uint.MaxValue : currentFrame + offset;
    }
}
