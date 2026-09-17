using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(ServerNetworkEntitySpawnCollectSystem))]
[UpdateBefore(typeof(DestroyEntitySystem))]
public partial class ServerNetworkStateCollectSystem : SystemBase
{
    private uint _lastCollectedFrame = uint.MaxValue;
    private int _frameInterval = 33;
    private readonly List<Action<uint, NetworkEntitySpawnInfo[], List<NetworkStateData>>> _snapshotCallbacks = new();

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
        uint currentFrame = frame.currentFrame;
        if (_lastCollectedFrame == currentFrame)
            return;

        _lastCollectedFrame = currentFrame;
        List<NetworkStateData> states = new();
        CollectStates(states, currentFrame, true);

        if (states.Count > 0)
        {
            if (!frame.sendOrder.TryGetValue(currentFrame, out Queue<NetworkStateData> queue))
            {
                queue = new Queue<NetworkStateData>();
                frame.sendOrder.Add(currentFrame, queue);
            }

            for (int index = 0; index < states.Count; index++)
                queue.Enqueue(states[index]);
        }

        if (_snapshotCallbacks.Count == 0)
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
        CollectMoveStates(states, onlyDirty);
        CollectFacingStates(states, onlyDirty);
        CollectVitalityStates(states, onlyDirty);
        CollectManaStates(states, onlyDirty);
        CollectBuffStates(states, currentFrame, onlyDirty);
        CollectControlStates(states, currentFrame, onlyDirty);
        CollectDeathStates(states, onlyDirty);
        CollectInteractableStates(states, onlyDirty);
        CollectTreasureStates(states, onlyDirty);
        CollectProjectileStates(states, onlyDirty);
        CollectPlayerPropCooldownStates(states, currentFrame, onlyDirty);
        CollectPlayerSkillSelectionStates(states, onlyDirty);
    }

    private void CollectMoveStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<UnitMoveComponent> moveRef,
                  RefRO<LocalTransform> transformRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitMoveComponent>, RefRO<LocalTransform>>())
        {
            UnitMoveComponent move = moveRef.ValueRO;
            if ((onlyDirty && move.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            LocalTransform transform = transformRef.ValueRO;
            states.Add(new NetworkMoveStateData
            {
                unitId = identityRef.ValueRO.id,
                baseMoveSpeed = move.BaseMoveSpeed,
                baseMoveSpeedOffset = move.BaseMoveSpeedOffset,
                baseMaxAcceleration = move.BaseMaxAcceleration,
                directionX = move.Direction.x,
                directionY = move.Direction.y,
                stateMoveMultiplier = move.StateMoveMultiplier,
                velocityX = move.Velocity.x,
                velocityY = move.Velocity.y,
                frameVelocityX = move.FrameVelocity.x,
                frameVelocityY = move.FrameVelocity.y,
                hasFrameVelocity = move.HasFrameVelocity,
                commandMoveSpeed = move.CommandMoveSpeed,
                positionX = transform.Position.x,
                positionY = transform.Position.y,
                positionZ = transform.Position.z,
            });
            if (onlyDirty)
            {
                move.NetworkDirty = 0;
                moveRef.ValueRW = move;
            }
        }
    }

    private void CollectFacingStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitFacingComponent> facingRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitFacingComponent>>())
        {
            UnitFacingComponent facing = facingRef.ValueRO;
            if ((onlyDirty && facing.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            states.Add(new NetworkFacingStateData
            {
                unitId = identityRef.ValueRO.id,
                directionX = facing.Direction.x,
                directionY = facing.Direction.y,
            });
            if (onlyDirty)
            {
                facing.NetworkDirty = 0;
                facingRef.ValueRW = facing;
            }
        }
    }

    private void CollectVitalityStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitVitalityComponent> vitalityRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitVitalityComponent>>())
        {
            UnitVitalityComponent vitality = vitalityRef.ValueRO;
            if ((onlyDirty && vitality.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

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
    }

    private void CollectManaStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitManaComponent> manaRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitManaComponent>>())
        {
            UnitManaComponent mana = manaRef.ValueRO;
            if ((onlyDirty && mana.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

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
    }

    private void CollectBuffStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, UnitBuffRuntimeComponent buffRuntime) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, UnitBuffRuntimeComponent>())
        {
            if ((onlyDirty && buffRuntime.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            NetworkBuffStateData state = new() { unitId = identityRef.ValueRO.id };
            for (int index = 0; index < buffRuntime.Buffs.Count; index++)
            {
                UnitBuffRuntimeEntry buff = buffRuntime.Buffs[index];
                if (buff == null)
                    continue;

                state.buffs.Add(new NetworkBuffEntryStateData
                {
                    buffId = buff.BuffId,
                    endFrame = GetEndFrame(buff.RemainingTime, currentFrame),
                    stackCount = buff.StackCount,
                    originUnitId = GetNetworkId(buff.HasOriginEntity ? buff.OriginEntity : Entity.Null),
                    sourceSkillId = buff.SourceSkillId,
                });
            }

            states.Add(state);
            if (onlyDirty)
            {
                buffRuntime.NetworkDirty = 0;
            }
        }
    }

    private void CollectControlStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitControlRuntimeComponent> controlRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitControlRuntimeComponent>>())
        {
            UnitControlRuntimeComponent control = controlRef.ValueRO;
            if ((onlyDirty && control.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            NetworkControlStateData state = new() { unitId = identityRef.ValueRO.id };
            for (int index = 0; index < control.Entries.Length; index++)
            {
                UnitControlRuntimeEntry entry = control.Entries[index];
                state.entries.Add(new NetworkControlEntryStateData
                {
                    controlType = entry.ControlType,
                    endFrame = GetEndFrame(entry.RemainingTime, currentFrame),
                    priority = entry.Priority,
                    lockMove = entry.LockMove,
                    lockCast = entry.LockCast,
                    interruptOnApply = entry.InterruptOnApply,
                    sourceUnitId = GetNetworkId(entry.SourceEntity),
                    motionVelocityX = entry.MotionVelocity.x,
                    motionVelocityY = entry.MotionVelocity.y,
                    motionDamping = entry.MotionDamping,
                });
            }

            states.Add(state);
            if (onlyDirty)
            {
                control.NetworkDirty = 0;
                controlRef.ValueRW = control;
            }
        }
    }

    private void CollectDeathStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitDeathComponent> deathRef, Entity entity) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitDeathComponent>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            UnitDeathComponent death = deathRef.ValueRO;
            if ((onlyDirty && death.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            states.Add(new NetworkDeathStateData
            {
                unitId = identityRef.ValueRO.id,
                isDead = EntityManager.IsComponentEnabled<UnitDeathComponent>(entity) ? (byte)1 : (byte)0,
            });
            if (onlyDirty)
            {
                death.NetworkDirty = 0;
                deathRef.ValueRW = death;
            }
        }
    }

    private void CollectInteractableStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<UnitInteractableComponent> interactableRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<UnitInteractableComponent>>())
        {
            UnitInteractableComponent interactable = interactableRef.ValueRO;
            if ((onlyDirty && interactable.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

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
    }

    private void CollectTreasureStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<TreasureComponent> treasureRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<TreasureComponent>>())
        {
            TreasureComponent treasure = treasureRef.ValueRO;
            if ((onlyDirty && treasure.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            states.Add(new NetworkTreasureStateData
            {
                unitId = identityRef.ValueRO.id,
                regionId = treasure.RegionId,
                randomSeed = treasure.RandomSeed,
                interestSize = treasure.InterestSize,
                isOpened = treasure.IsOpened,
            });
            if (onlyDirty)
            {
                treasure.NetworkDirty = 0;
                treasureRef.ValueRW = treasure;
            }
        }
    }

    private void CollectProjectileStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef,
                  RefRW<SkillProjectileComponent> projectileRef,
                  RefRO<LocalTransform> transformRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<SkillProjectileComponent>, RefRO<LocalTransform>>())
        {
            SkillProjectileComponent projectile = projectileRef.ValueRO;
            if ((onlyDirty && projectile.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            LocalTransform transform = transformRef.ValueRO;
            states.Add(new NetworkProjectileStateData
            {
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
    }

    private void CollectPlayerPropCooldownStates(List<NetworkStateData> states, uint currentFrame, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<PlayerPropCooldownComponent> cooldownRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<PlayerPropCooldownComponent>>())
        {
            PlayerPropCooldownComponent cooldown = cooldownRef.ValueRO;
            if ((onlyDirty && cooldown.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

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
    }

    private void CollectPlayerSkillSelectionStates(List<NetworkStateData> states, bool onlyDirty)
    {
        foreach ((RefRO<NetworkIdentityComponent> identityRef, RefRW<PlayerSkillSelectionComponent> selectionRef) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>, RefRW<PlayerSkillSelectionComponent>>())
        {
            PlayerSkillSelectionComponent selection = selectionRef.ValueRO;
            if ((onlyDirty && selection.NetworkDirty == 0) || identityRef.ValueRO.id == Guid.Empty)
                continue;

            states.Add(new NetworkPlayerSkillSelectionStateData
            {
                unitId = identityRef.ValueRO.id,
                currentChainIndex = selection.CurrentChainIndex,
            });
            if (onlyDirty)
            {
                selection.NetworkDirty = 0;
                selectionRef.ValueRW = selection;
            }
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
