using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class UnitFeatureBakeUtility
{
    public static Entity GetFeatureEntity<T>(this Baker<T> baker) where T : Component
    {
        return baker.GetEntity(TransformUsageFlags.Dynamic);
    }

    public static void AddUnitBattleComponents<T>(this Baker<T> baker, Component authoring, Entity entity) where T : Component
    {
        baker.DependsOnUnitDataTable();

        UnitVitalityModuleData vitalityData = UnitAuthoringUtility.ResolveModuleData<UnitVitalityModuleData>(authoring);
        float baseHealth = vitalityData?.BaseMaxHealth ?? 100f;
        float baseHealthRegenPerSecond = vitalityData?.BaseHealthRegenPerSecond ?? 0f;
        float baseDefense = vitalityData?.BaseDefense ?? 0f;
        baker.AddComponent(entity, new UnitVitalityComponent
        {
            BaseMaxHealth = baseHealth,
            BaseMaxHealthOffset = 0f,
            HealthFactor = 1f,
            HealthBonus = 0f,
            CurrentHealth = baseHealth,
            BaseHealthRegenPerSecond = baseHealthRegenPerSecond,
            BaseHealthRegenOffset = 0f,
            HealthRegenFactor = 1f,
            HealthRegenBonus = 0f,
            BaseDefense = baseDefense,
            BaseDefenseOffset = 0f,
            DefenseFactor = 1f,
            DefenseBonus = 0f,
        });
        baker.AddComponent<DestroyEntityFlag>(entity);
        baker.SetComponentEnabled<DestroyEntityFlag>(entity, false);

        UnitManaModuleData manaData = UnitAuthoringUtility.ResolveModuleData<UnitManaModuleData>(authoring);
        float baseMp = manaData?.BaseMaxMp ?? 50f;
        float baseMpRegenPerSecond = manaData?.BaseMpRegenPerSecond ?? 0f;
        baker.AddComponent(entity, new UnitManaComponent
        {
            BaseMaxMp = baseMp,
            BaseMaxMpOffset = 0f,
            MpFactor = 1f,
            MpBonus = 0f,
            CurrentMana = baseMp,
            BaseMpRegenPerSecond = baseMpRegenPerSecond,
            BaseMpRegenPerSecondOffset = 0f,
            MpRegenFactor = 1f,
            MpRegenBonus = 0f,
        });

        UnitAttackModuleData attackData = UnitAuthoringUtility.ResolveModuleData<UnitAttackModuleData>(authoring);
        baker.AddComponent(entity, new UnitAttackComponent
        {
            BaseAttackPower = attackData?.BaseAttackPower ?? 10f,
            BaseAttackPowerOffset = 0f,
            AttackFactor = 1f,
            AttackBonus = 0f,
            BaseSkillRange = attackData?.BaseSkillRange ?? 1f,
            BaseSkillRangeOffset = 0f,
            RangeFactor = 1f,
            RangeBonus = 0f,
            BaseActionSpeedBonus = attackData?.BaseActionSpeedBonus ?? 0f,
            BaseActionSpeedBonusOffset = 0f,
            ActionSpeedFactor = 1f,
            ActionSpeedBonus = 0f,
            BaseChantSpeedBonus = attackData?.BaseChantSpeedBonus ?? 0f,
            BaseChantSpeedBonusOffset = 0f,
            ChantSpeedFactor = 1f,
            ChantSpeedBonus = 0f,
        });
        baker.AddComponent(entity, new UnitElementComponent
        {
            WaterPower = 0f,
            FirePower = 0f,
            LightningPower = 0f,
            WindPower = 0f,
        });

        baker.AddComponent(entity, new UnitCastComponent());
        baker.AddComponentObject(entity, new UnitCastFollowupRuntimeComponent());
        baker.AddComponentObject(entity, new UnitCastTaskPayloadComponent());
        baker.AddComponentObject(entity, new UnitCastSkillPayloadComponent());

        UnitBuffRuntimeComponent buffRuntimeComponent = new();
        UnitBuffModuleData buffData = UnitAuthoringUtility.ResolveModuleData<UnitBuffModuleData>(authoring);
        if (buffData?.Buffs != null)
        {
            for (int i = 0; i < buffData.Buffs.Count; i++)
            {
                UnitInitialBuffEntry entry = buffData.Buffs[i];
                if (entry == null || entry.BuffId < 0)
                    continue;

                buffRuntimeComponent.Buffs.Add(new UnitBuffRuntimeEntry
                {
                    BuffId = entry.BuffId,
                    RemainingTime = entry.DurationSeconds,
                    StackCount = Mathf.Max(1, entry.StackCount),
                    HasOriginEntity = false,
                    OriginEntity = Entity.Null,
                    SourceSkillId = -1,
                });
            }
        }

        baker.AddComponentObject(entity, buffRuntimeComponent);
        baker.AddComponentObject(entity, new UnitSkillModifierRuntimeComponent());

        UnitFactionModuleData factionData = UnitAuthoringUtility.ResolveModuleData<UnitFactionModuleData>(authoring);
        baker.AddComponent(entity, new UnitFactionComponent
        {
            Value = factionData?.Faction ?? UnitFactionType.Friendly,
        });
    }

    public static void AddUnitDecisionComponents<T>(this Baker<T> baker, Component authoring, Entity entity) where T : Component
    {
        baker.DependsOnUnitDataTable();
        baker.AddComponent(entity, new UnitIntentComponent());

        UnitData unitData = UnitAuthoringUtility.ResolveUnitData(authoring);
        Transform root = authoring.transform.root != null ? authoring.transform.root : authoring.transform;
        baker.AddComponentObject(entity, new UnitStateMachineComponent
        {
            UnitDataId = unitData?.Id ?? -1,
            UnitName = unitData?.Name ?? root.name,
        });
    }

    public static void AddUnitControlComponents<T>(this Baker<T> baker, Entity entity) where T : Component
    {
        baker.AddComponent(entity, new UnitControlRuntimeComponent
        {
            Entries = new FixedList512Bytes<UnitControlRuntimeEntry>(),
            ActiveType = UnitControlType.None,
            ActiveRemainingTime = 0f,
            ActivePriority = 0,
            LockMove = 0,
            LockCast = 0,
            HasControl = 0,
            ActiveSourceEntity = Entity.Null,
            ActiveMotionVelocity = float2.zero,
            ActiveMotionDamping = 0f,
        });
    }

    public static void AddUnitMovementComponents<T>(this Baker<T> baker, Component authoring, Entity entity) where T : Component
    {
        baker.DependsOnUnitDataTable();

        UnitMoveModuleData moveData = UnitAuthoringUtility.ResolveModuleData<UnitMoveModuleData>(authoring);
        baker.AddComponent(entity, new UnitMoveComponent
        {
            BaseMoveSpeed = moveData?.BaseMoveSpeed ?? 5f,
            BaseMoveSpeedOffset = 0f,
            BaseMaxAcceleration = moveData?.BaseMaxAcceleration ?? 30f,
            SpeedFactor = 1f,
            SpeedBonus = 0f,
            DesiredDirection = float2.zero,
            DesiredMaxSpeed = 0f,
            DesiredAcceleration = math.max(0f, moveData?.BaseMaxAcceleration ?? 30f),
            Velocity = float2.zero,
        });
        baker.AddComponent(entity, new UnitFacingComponent
        {
            Direction = new float2(1f, 0f),
        });
    }

    public static void AddUnitVisualComponents<T>(this Baker<T> baker, Transform authoringTransform, Entity entity) where T : Component
    {
        Transform root = authoringTransform.root != null ? authoringTransform.root : authoringTransform;
        baker.AddComponent(entity, UnitAnimationComponent.CreateDefault(new FixedString128Bytes(root.name)));
        baker.AddComponent(entity, new UnitAnimationFrameUvMinProperty
        {
            Value = new float4(0f, 0f, 0f, 0f),
        });
        baker.AddComponent(entity, new UnitAnimationFrameUvSizeProperty
        {
            Value = new float4(1f, 1f, 0f, 0f),
        });
        baker.AddComponent(entity, new UnitAnimationFrameWorldSizeProperty
        {
            Value = new float4(1f, 1f, 0f, 0f),
        });
        baker.AddComponent(entity, new UnitAnimationFramePivotOffsetProperty
        {
            Value = new float4(0f, 0f, 0f, 0f),
        });
    }

    public static void AddPlayerComponents<T>(this Baker<T> baker, Entity entity) where T : Component
    {
        baker.AddComponent<PlayerTag>(entity);
        PlayerSkillComponent playerSkill = default;
        playerSkill.Clear();
        baker.AddComponent(entity, playerSkill);
    }

    public static void AddUnitSkillComponents<T>(this Baker<T> baker, Component authoring, Entity entity) where T : Component
    {
        baker.DependsOnUnitDataTable();

        UnitSkillModuleData skillData = UnitAuthoringUtility.ResolveModuleData<UnitSkillModuleData>(authoring);
        if (skillData == null)
            return;

        UnitSkillComponent component = new UnitSkillComponent
        {
            HasPendingCast = false,
            PendingSkillIndex = -1,
        };

        if (skillData.Skills != null)
        {
            for (int i = 0; i < skillData.Skills.Count; i++)
            {
                UnitSkillSlotData slot = skillData.Skills[i];
                if (slot == null || slot.SkillId < 0)
                    continue;

                if (component.Skills.Length >= component.Skills.Capacity)
                    break;

                component.Skills.Add(new UnitSkillEntry
                {
                    SkillId = slot.SkillId,
                    TagMask = slot.TagMask,
                    MinDistance = math.max(0f, slot.MinDistance),
                    MaxDistance = math.max(slot.MinDistance, slot.MaxDistance),
                    CooldownSeconds = math.max(0f, slot.CooldownSeconds),
                    CooldownRemaining = 0f,
                    Weight = math.max(1, slot.Weight),
                });
            }
        }

        baker.AddComponent(entity, component);
    }

    public static void AddUnitAIComponents<T>(this Baker<T> baker, Component authoring, Entity entity) where T : Component
    {
        baker.DependsOnUnitDataTable();

        UnitPerceptionModuleData perceptionData = UnitAuthoringUtility.ResolveModuleData<UnitPerceptionModuleData>(authoring);
        baker.AddComponent(entity, new UnitPerceptionComponent
        {
            SearchRadius = Mathf.Max(0f, perceptionData?.SearchRadius ?? 8f),
            HasTarget = false,
            TargetEntity = Entity.Null,
            TargetPosition = float2.zero,
            TargetDistance = 0f,
        });

        Transform root = authoring.transform.root != null ? authoring.transform.root : authoring.transform;
        baker.AddComponentObject(entity, new UnitBehaviorTreeComponent
        {
            UnitName = root.name,
        });
    }

    public static void AddUnitJumpComponents<T>(this Baker<T> baker, Entity entity) where T : Component
    {
        baker.AddComponent(entity, new UnitJumpArcComponent
        {
            StartPosition = float3.zero,
            EndPosition = float3.zero,
            Duration = 0f,
            Elapsed = 0f,
            ArcHeight = 0f,
            IsActive = 0,
            IsCompleted = 1,
        });
    }

    public static void AddUnitDropComponents<T>(this Baker<T> baker, Component authoring, Entity entity) where T : Component
    {
        baker.DependsOnUnitDataTable();

        UnitDropModuleData dropData = UnitAuthoringUtility.ResolveModuleData<UnitDropModuleData>(authoring);
        if (dropData == null || dropData.DropDataId < 0)
            return;

        baker.AddComponent(entity, new UnitDropComponent
        {
            DropDataId = dropData.DropDataId,
        });
    }

    public static void AddNpcInteractionComponents<T>(this Baker<T> baker, Component authoring, Entity entity, float interactionRange) where T : Component
    {
#if UNITY_EDITOR
        TextAsset npcDataAsset = NPCAuthoringUtility.GetNpcDataTableAsset();
        if (npcDataAsset != null)
            baker.DependsOn(npcDataAsset);
#endif

        Transform interact = authoring.transform.Find("Interact");
        Entity interactEntity = interact != null
            ? baker.GetEntity(interact, TransformUsageFlags.Dynamic)
            : Entity.Null;
        NPCData npcData = NPCAuthoringUtility.ResolveNpcData(authoring);

        baker.AddComponent<NPCTag>(entity);
        baker.AddComponent(entity, new NPCInteractable
        {
            NpcId = npcData?.Id ?? -1,
            interact = interactEntity,
            interactRangeSq = interactionRange * interactionRange,
        });
    }

    private static void DependsOnUnitDataTable<T>(this Baker<T> baker) where T : Component
    {
        TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
        if (unitDataAsset != null)
            baker.DependsOn(unitDataAsset);
    }
}
