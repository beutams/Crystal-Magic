using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.Game.Unit
{
    public enum UnitDebugComponentKind
    {
        Transform,
        Faction,
        Vitality,
        Mana,
        Attack,
        Element,
        Move,
        Perception,
        Facing,
        Control,
        Buffs,
        BehaviorTree,
        StateScript,
        Death,
        Destroy,
        Drop,
        Interactable,
        PlayerCurrentSkill,
        SkillRelease,
        Variables,
    }

    public readonly struct UnitDebugUnitEntry
    {
        public UnitDebugUnitEntry(Entity entity, string label)
        {
            Entity = entity;
            Label = label;
        }

        public Entity Entity { get; }
        public string Label { get; }
    }

    public readonly struct UnitDebugComponentEntry
    {
        public UnitDebugComponentEntry(UnitDebugComponentKind kind, string label)
        {
            Kind = kind;
            Label = label;
        }

        public UnitDebugComponentKind Kind { get; }
        public string Label { get; }
    }

    public readonly struct UnitDebugBuffEntry
    {
        public UnitDebugBuffEntry(int buffId, string label)
        {
            BuffId = buffId;
            Label = label;
        }

        public int BuffId { get; }
        public string Label { get; }
    }

    /// <summary>
    /// Debug-only, main-thread helpers for inspecting live unit ECS data. The UI submits a
    /// compact <c>Field=Value</c> document so every simple field can be edited without
    /// allowing runtime-object reflection to corrupt behavior/state-script internals.
    /// </summary>
    public static class UnitDebugInspectorUtility
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public static void GetUnits(EntityManager entityManager, List<UnitDebugUnitEntry> destination)
        {
            destination.Clear();

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!IsUnit(entityManager, entity))
                    continue;

                destination.Add(new UnitDebugUnitEntry(entity, BuildUnitLabel(entityManager, entity)));
            }

            destination.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.Ordinal));
        }

        public static void GetComponents(EntityManager entityManager, Entity entity, List<UnitDebugComponentEntry> destination)
        {
            destination.Clear();
            if (entity == Entity.Null || !entityManager.Exists(entity))
                return;

            AddIfPresent<LocalTransform>(entityManager, entity, destination, UnitDebugComponentKind.Transform, "LocalTransform");
            AddIfPresent<UnitFactionComponent>(entityManager, entity, destination, UnitDebugComponentKind.Faction, "UnitFactionComponent");
            AddIfPresent<UnitVitalityComponent>(entityManager, entity, destination, UnitDebugComponentKind.Vitality, "UnitVitalityComponent");
            AddIfPresent<UnitManaComponent>(entityManager, entity, destination, UnitDebugComponentKind.Mana, "UnitManaComponent");
            AddIfPresent<UnitAttackComponent>(entityManager, entity, destination, UnitDebugComponentKind.Attack, "UnitAttackComponent");
            AddIfPresent<UnitElementComponent>(entityManager, entity, destination, UnitDebugComponentKind.Element, "UnitElementComponent");
            AddIfPresent<UnitMoveComponent>(entityManager, entity, destination, UnitDebugComponentKind.Move, "UnitMoveComponent");
            AddIfPresent<UnitPerceptionComponent>(entityManager, entity, destination, UnitDebugComponentKind.Perception, "UnitPerceptionComponent");
            AddIfPresent<UnitFacingComponent>(entityManager, entity, destination, UnitDebugComponentKind.Facing, "UnitFacingComponent");
            AddIfPresent<UnitControlRuntimeComponent>(entityManager, entity, destination, UnitDebugComponentKind.Control, "UnitControlRuntimeComponent");
            // Buff is intentionally exposed even before the first Buff is added. Its Add button
            // creates the managed runtime component through UnitBuffUtility when necessary.
            destination.Add(new UnitDebugComponentEntry(UnitDebugComponentKind.Buffs, "UnitBuffRuntimeComponent"));
            AddIfPresent<UnitBehaviorTreeComponent>(entityManager, entity, destination, UnitDebugComponentKind.BehaviorTree, "UnitBehaviorTreeComponent (Clear only)");
            AddIfPresent<UnitStateScriptComponent>(entityManager, entity, destination, UnitDebugComponentKind.StateScript, "UnitStateScriptComponent (Clear only)");
            AddIfPresent<UnitDeathComponent>(entityManager, entity, destination, UnitDebugComponentKind.Death, "UnitDeathComponent");
            AddIfPresent<DestroyEntityFlag>(entityManager, entity, destination, UnitDebugComponentKind.Destroy, "DestroyEntityFlag");
            AddIfPresent<UnitDropComponent>(entityManager, entity, destination, UnitDebugComponentKind.Drop, "UnitDropComponent");
            AddIfPresent<UnitInteractableComponent>(entityManager, entity, destination, UnitDebugComponentKind.Interactable, "UnitInteractableComponent");
            AddIfPresent<PlayerCurrentSkillComponent>(entityManager, entity, destination, UnitDebugComponentKind.PlayerCurrentSkill, "PlayerCurrentSkillComponent");
            AddIfPresent<UnitSkillReleaseComponent>(entityManager, entity, destination, UnitDebugComponentKind.SkillRelease, "UnitSkillReleaseComponent");
            AddIfPresent<UnitVariableComponent>(entityManager, entity, destination, UnitDebugComponentKind.Variables, "UnitVariableComponent");
        }

        public static void GetBuffs(EntityManager entityManager, Entity entity, List<UnitDebugBuffEntry> destination)
        {
            destination.Clear();
            if (!UnitBuffUtility.TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent component) || component.Buffs == null)
                return;

            for (int i = 0; i < component.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = component.Buffs[i];
                if (entry == null)
                    continue;

                BuffData data = DataComponent.Instance?.Get<BuffData>(entry.BuffId);
                string name = string.IsNullOrWhiteSpace(data?.Name) ? $"Buff {entry.BuffId}" : data.Name;
                destination.Add(new UnitDebugBuffEntry(entry.BuffId, $"{i + 1}. {name}  x{entry.StackCount}  {Format(entry.RemainingTime)}s"));
            }
        }

        public static string BuildEditorText(EntityManager entityManager, Entity entity, UnitDebugComponentKind component, int selectedBuffIndex)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
                return "The selected entity no longer exists.";

            StringBuilder builder = new(512);
            switch (component)
            {
                case UnitDebugComponentKind.Transform:
                    if (entityManager.HasComponent<LocalTransform>(entity))
                    {
                        LocalTransform value = entityManager.GetComponentData<LocalTransform>(entity);
                        Append(builder, "Position.X", value.Position.x);
                        Append(builder, "Position.Y", value.Position.y);
                        Append(builder, "Position.Z", value.Position.z);
                        Append(builder, "Rotation.X", value.Rotation.value.x);
                        Append(builder, "Rotation.Y", value.Rotation.value.y);
                        Append(builder, "Rotation.Z", value.Rotation.value.z);
                        Append(builder, "Rotation.W", value.Rotation.value.w);
                        Append(builder, "Scale", value.Scale);
                    }
                    break;
                case UnitDebugComponentKind.Faction:
                    if (entityManager.HasComponent<UnitFactionComponent>(entity))
                        Append(builder, "Value", (int)entityManager.GetComponentData<UnitFactionComponent>(entity).Value);
                    break;
                case UnitDebugComponentKind.Vitality:
                    if (entityManager.HasComponent<UnitVitalityComponent>(entity))
                    {
                        UnitVitalityComponent value = entityManager.GetComponentData<UnitVitalityComponent>(entity);
                        Append(builder, "BaseMaxHealth", value.BaseMaxHealth);
                        Append(builder, "BaseMaxHealthOffset", value.BaseMaxHealthOffset);
                        Append(builder, "CurrentHealth", value.CurrentHealth);
                        Append(builder, "BaseHealthRegenPerSecond", value.BaseHealthRegenPerSecond);
                        Append(builder, "BaseHealthRegenOffset", value.BaseHealthRegenOffset);
                        Append(builder, "BaseDefense", value.BaseDefense);
                        Append(builder, "BaseDefenseOffset", value.BaseDefenseOffset);
                    }
                    break;
                case UnitDebugComponentKind.Mana:
                    if (entityManager.HasComponent<UnitManaComponent>(entity))
                    {
                        UnitManaComponent value = entityManager.GetComponentData<UnitManaComponent>(entity);
                        Append(builder, "BaseMaxMp", value.BaseMaxMp);
                        Append(builder, "BaseMaxMpOffset", value.BaseMaxMpOffset);
                        Append(builder, "CurrentMana", value.CurrentMana);
                        Append(builder, "BaseMpRegenPerSecond", value.BaseMpRegenPerSecond);
                        Append(builder, "BaseMpRegenPerSecondOffset", value.BaseMpRegenPerSecondOffset);
                    }
                    break;
                case UnitDebugComponentKind.Attack:
                    if (entityManager.HasComponent<UnitAttackComponent>(entity))
                    {
                        UnitAttackComponent value = entityManager.GetComponentData<UnitAttackComponent>(entity);
                        Append(builder, "BaseAttackPower", value.BaseAttackPower);
                        Append(builder, "BaseAttackPowerOffset", value.BaseAttackPowerOffset);
                        Append(builder, "BaseSkillRange", value.BaseSkillRange);
                        Append(builder, "BaseSkillRangeOffset", value.BaseSkillRangeOffset);
                        Append(builder, "BaseChantSpeedBonus", value.BaseChantSpeedBonus);
                        Append(builder, "BaseChantSpeedBonusOffset", value.BaseChantSpeedBonusOffset);
                    }
                    break;
                case UnitDebugComponentKind.Element:
                    if (entityManager.HasComponent<UnitElementComponent>(entity))
                    {
                        UnitElementComponent value = entityManager.GetComponentData<UnitElementComponent>(entity);
                        Append(builder, "WaterPower", value.WaterPower);
                        Append(builder, "FirePower", value.FirePower);
                        Append(builder, "LightningPower", value.LightningPower);
                        Append(builder, "WindPower", value.WindPower);
                    }
                    break;
                case UnitDebugComponentKind.Move:
                    if (entityManager.HasComponent<UnitMoveComponent>(entity))
                    {
                        UnitMoveComponent value = entityManager.GetComponentData<UnitMoveComponent>(entity);
                        Append(builder, "BaseMoveSpeed", value.BaseMoveSpeed);
                        Append(builder, "BaseMoveSpeedOffset", value.BaseMoveSpeedOffset);
                        Append(builder, "BaseMaxAcceleration", value.BaseMaxAcceleration);
                        Append(builder, "Direction.X", value.Direction.x);
                        Append(builder, "Direction.Y", value.Direction.y);
                        Append(builder, "StateMoveMultiplier", value.StateMoveMultiplier);
                        Append(builder, "Velocity.X", value.Velocity.x);
                        Append(builder, "Velocity.Y", value.Velocity.y);
                    }
                    break;
                case UnitDebugComponentKind.Perception:
                    if (entityManager.HasComponent<UnitPerceptionComponent>(entity))
                    {
                        Append(builder, "SearchRadius", entityManager.GetComponentData<UnitPerceptionComponent>(entity).SearchRadius);
                        if (entityManager.HasBuffer<UnitPerceptionEntityElement>(entity))
                        {
                            DynamicBuffer<UnitPerceptionEntityElement> entries = entityManager.GetBuffer<UnitPerceptionEntityElement>(entity);
                            builder.Append("Entities=");
                            for (int i = 0; i < entries.Length; i++)
                            {
                                if (i > 0)
                                    builder.Append(',');
                                builder.Append(Format(entries[i].Value));
                            }
                            builder.AppendLine();
                        }
                    }
                    break;
                case UnitDebugComponentKind.Facing:
                    if (entityManager.HasComponent<UnitFacingComponent>(entity))
                    {
                        float2 direction = entityManager.GetComponentData<UnitFacingComponent>(entity).Direction;
                        Append(builder, "Direction.X", direction.x);
                        Append(builder, "Direction.Y", direction.y);
                    }
                    break;
                case UnitDebugComponentKind.Control:
                    if (entityManager.HasComponent<UnitControlRuntimeComponent>(entity))
                    {
                        UnitControlRuntimeComponent value = entityManager.GetComponentData<UnitControlRuntimeComponent>(entity);
                        Append(builder, "ActiveType", (int)value.ActiveType);
                        Append(builder, "ActiveRemainingTime", value.ActiveRemainingTime);
                        Append(builder, "ActivePriority", value.ActivePriority);
                        Append(builder, "LockMove", value.LockMove);
                        Append(builder, "LockCast", value.LockCast);
                        Append(builder, "HasControl", value.HasControl);
                        Append(builder, "ActiveSourceEntity", Format(value.ActiveSourceEntity));
                        Append(builder, "ActiveMotionVelocity.X", value.ActiveMotionVelocity.x);
                        Append(builder, "ActiveMotionVelocity.Y", value.ActiveMotionVelocity.y);
                        Append(builder, "ActiveMotionDamping", value.ActiveMotionDamping);
                        builder.AppendLine("Entries format: Type|Time|Priority|LockMove|LockCast|Interrupt|Entity|VelocityX|VelocityY|Damping;...");
                        builder.Append("Entries=");
                        for (int i = 0; i < value.Entries.Length; i++)
                        {
                            UnitControlRuntimeEntry entry = value.Entries[i];
                            builder.Append((int)entry.ControlType).Append('|')
                                .Append(Format(entry.RemainingTime)).Append('|')
                                .Append(entry.Priority).Append('|')
                                .Append(entry.LockMove).Append('|')
                                .Append(entry.LockCast).Append('|')
                                .Append(entry.InterruptOnApply).Append('|')
                                .Append(Format(entry.SourceEntity)).Append('|')
                                .Append(Format(entry.MotionVelocity.x)).Append('|')
                                .Append(Format(entry.MotionVelocity.y)).Append('|')
                                .Append(Format(entry.MotionDamping));
                            if (i + 1 < value.Entries.Length)
                                builder.Append(';');
                        }
                        builder.AppendLine();
                    }
                    break;
                case UnitDebugComponentKind.Buffs:
                    BuildBuffEditorText(entityManager, entity, selectedBuffIndex, builder);
                    break;
                case UnitDebugComponentKind.BehaviorTree:
                    builder.AppendLine("Clear only. Removing this component stops behavior-tree execution for this unit.");
                    break;
                case UnitDebugComponentKind.StateScript:
                    builder.AppendLine("Clear only. Removing this component stops state-script execution for this unit.");
                    break;
                case UnitDebugComponentKind.Death:
                    if (entityManager.HasComponent<UnitDeathComponent>(entity))
                        Append(builder, "Enabled", entityManager.IsComponentEnabled<UnitDeathComponent>(entity) ? 1 : 0);
                    break;
                case UnitDebugComponentKind.Destroy:
                    if (entityManager.HasComponent<DestroyEntityFlag>(entity))
                        Append(builder, "Enabled", entityManager.IsComponentEnabled<DestroyEntityFlag>(entity) ? 1 : 0);
                    break;
                case UnitDebugComponentKind.Drop:
                    if (entityManager.HasComponent<UnitDropComponent>(entity))
                        Append(builder, "DropDataId", entityManager.GetComponentData<UnitDropComponent>(entity).DropDataId);
                    break;
                case UnitDebugComponentKind.Interactable:
                    if (entityManager.HasComponent<UnitInteractableComponent>(entity))
                    {
                        UnitInteractableComponent value = entityManager.GetComponentData<UnitInteractableComponent>(entity);
                        Append(builder, "Kind", (int)value.Data.Kind);
                        Append(builder, "DataId", value.Data.DataId);
                        Append(builder, "Amount", value.Data.Amount);
                        Append(builder, "Variant", value.Data.Variant);
                        Append(builder, "RangeSq", value.RangeSq);
                        Append(builder, "IsEnabled", value.IsEnabled);
                    }
                    break;
                case UnitDebugComponentKind.PlayerCurrentSkill:
                    if (entityManager.HasComponent<PlayerCurrentSkillComponent>(entity))
                    {
                        PlayerCurrentSkillComponent value = entityManager.GetComponentObject<PlayerCurrentSkillComponent>(entity);
                        Append(builder, "CurrentChainId", value.CurrentChainId);
                        Append(builder, "CurrentSlotIndex", value.CurrentSlotIndex);
                        builder.AppendLine("PendingExtraModifiers is runtime-owned and not edited here.");
                    }
                    break;
                case UnitDebugComponentKind.SkillRelease:
                    if (entityManager.HasComponent<UnitSkillReleaseComponent>(entity))
                    {
                        UnitSkillReleaseComponent value = entityManager.GetComponentObject<UnitSkillReleaseComponent>(entity);
                        builder.AppendLine("PendingRequests format: SkillId|OriginEntity|OriginX|OriginY|OriginZ|FacingX|FacingY|HasTargetEntity|TargetEntity|HasTargetPosition|TargetX|TargetY|TargetZ;...");
                        builder.Append("PendingRequests=");
                        if (value.PendingRequests != null)
                        {
                            for (int i = 0; i < value.PendingRequests.Count; i++)
                            {
                                SkillReleaseRequest request = value.PendingRequests[i];
                                if (request == null)
                                    continue;

                                if (i > 0)
                                    builder.Append(';');
                                builder.Append(request.SkillId).Append('|')
                                    .Append(Format(request.OriginEntity)).Append('|')
                                    .Append(Format(request.OriginPosition.x)).Append('|')
                                    .Append(Format(request.OriginPosition.y)).Append('|')
                                    .Append(Format(request.OriginPosition.z)).Append('|')
                                    .Append(Format(request.OriginFacing.x)).Append('|')
                                    .Append(Format(request.OriginFacing.y)).Append('|')
                                    .Append(request.HasTargetEntity ? 1 : 0).Append('|')
                                    .Append(Format(request.TargetEntity)).Append('|')
                                    .Append(request.HasTargetPosition ? 1 : 0).Append('|')
                                    .Append(Format(request.TargetPosition.x)).Append('|')
                                    .Append(Format(request.TargetPosition.y)).Append('|')
                                    .Append(Format(request.TargetPosition.z));
                            }
                        }
                        builder.AppendLine();
                    }
                    break;
                case UnitDebugComponentKind.Variables:
                    if (entityManager.HasComponent<UnitVariableComponent>(entity))
                    {
                        UnitVariableComponent value = entityManager.GetComponentObject<UnitVariableComponent>(entity);
                        builder.AppendLine("Variables are a typed runtime dictionary and are displayed read-only.");
                        if (value.Values != null)
                        {
                            foreach (KeyValuePair<string, UnitValue> pair in value.Values)
                                builder.Append(pair.Key).Append('=').Append(pair.Value).AppendLine();
                        }
                    }
                    break;
            }

            return builder.ToString();
        }

        public static bool ApplyEditorText(
            EntityManager entityManager,
            Entity entity,
            UnitDebugComponentKind component,
            int selectedBuffIndex,
            string text,
            out string message)
        {
            message = string.Empty;
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                message = "The selected entity no longer exists.";
                return false;
            }

            Dictionary<string, string> values = ParseValues(text);
            bool changed = component switch
            {
                UnitDebugComponentKind.Transform => ApplyTransform(entityManager, entity, values),
                UnitDebugComponentKind.Faction => ApplyFaction(entityManager, entity, values),
                UnitDebugComponentKind.Vitality => ApplyVitality(entityManager, entity, values),
                UnitDebugComponentKind.Mana => ApplyMana(entityManager, entity, values),
                UnitDebugComponentKind.Attack => ApplyAttack(entityManager, entity, values),
                UnitDebugComponentKind.Element => ApplyElement(entityManager, entity, values),
                UnitDebugComponentKind.Move => ApplyMove(entityManager, entity, values),
                UnitDebugComponentKind.Perception => ApplyPerception(entityManager, entity, values),
                UnitDebugComponentKind.Facing => ApplyFacing(entityManager, entity, values),
                UnitDebugComponentKind.Control => ApplyControl(entityManager, entity, values),
                UnitDebugComponentKind.Buffs => ApplyBuff(entityManager, entity, selectedBuffIndex, values),
                UnitDebugComponentKind.Death => ApplyEnabled<UnitDeathComponent>(entityManager, entity, values),
                UnitDebugComponentKind.Destroy => ApplyEnabled<DestroyEntityFlag>(entityManager, entity, values),
                UnitDebugComponentKind.Drop => ApplyDrop(entityManager, entity, values),
                UnitDebugComponentKind.Interactable => ApplyInteractable(entityManager, entity, values),
                UnitDebugComponentKind.PlayerCurrentSkill => ApplyPlayerCurrentSkill(entityManager, entity, values),
                UnitDebugComponentKind.SkillRelease => ApplySkillRelease(entityManager, entity, values),
                _ => false,
            };

            message = changed ? "Applied." : "No valid editable value was supplied.";
            return changed;
        }

        public static bool AddBuff(EntityManager entityManager, Entity entity, string text, out string message)
        {
            Dictionary<string, string> values = ParseValues(text);
            if (!TryGetInt(values, "BuffId", out int buffId))
            {
                message = "Add Buff requires BuffId=<id>.";
                return false;
            }

            float duration = TryGetFloat(values, "Duration", out float parsedDuration) ? parsedDuration : -1f;
            int stackCount = TryGetInt(values, "StackCount", out int parsedStackCount)
                ? parsedStackCount
                : TryGetInt(values, "Stacks", out parsedStackCount) ? parsedStackCount : 1;
            bool applied = UnitBuffUtility.Apply(entityManager, entity, buffId, duration, stackCount, Entity.Null, -1);
            message = applied ? "Buff added." : "Unable to add Buff. Check BuffId.";
            return applied;
        }

        public static bool RemoveSelectedBuff(EntityManager entityManager, Entity entity, int selectedBuffIndex, out string message)
        {
            if (!TryGetBuff(entityManager, entity, selectedBuffIndex, out UnitBuffRuntimeEntry entry))
            {
                message = "Select a Buff first.";
                return false;
            }

            bool removed = UnitBuffUtility.RemoveAll(entityManager, entity, entry.BuffId);
            message = removed ? "Buff removed." : "Unable to remove Buff.";
            return removed;
        }

        public static bool ClearBehaviorTree(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<UnitBehaviorTreeComponent>(entity))
                return false;

            entityManager.RemoveComponent<UnitBehaviorTreeComponent>(entity);
            return true;
        }

        public static bool ClearStateScript(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<UnitStateScriptComponent>(entity))
                return false;

            entityManager.RemoveComponent<UnitStateScriptComponent>(entity);
            return true;
        }

        private static bool IsUnit(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<UnitFactionComponent>(entity) ||
                   entityManager.HasComponent<UnitVitalityComponent>(entity) ||
                   entityManager.HasComponent<UnitAttackComponent>(entity) ||
                   entityManager.HasComponent<UnitMoveComponent>(entity) ||
                   entityManager.HasComponent<UnitManaComponent>(entity) ||
                   entityManager.HasComponent<UnitPerceptionComponent>(entity) ||
                   entityManager.HasComponent<UnitBehaviorTreeComponent>(entity) ||
                   entityManager.HasComponent<UnitStateScriptComponent>(entity);
        }

        private static string BuildUnitLabel(EntityManager entityManager, Entity entity)
        {
            string faction = entityManager.HasComponent<UnitFactionComponent>(entity)
                ? entityManager.GetComponentData<UnitFactionComponent>(entity).Value.ToString()
                : "Unit";
            return $"{faction}  [{entity.Index}:{entity.Version}]";
        }

        private static void AddIfPresent<T>(
            EntityManager entityManager,
            Entity entity,
            List<UnitDebugComponentEntry> destination,
            UnitDebugComponentKind kind,
            string label)
            where T : IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
                destination.Add(new UnitDebugComponentEntry(kind, label));
        }

        private static void BuildBuffEditorText(EntityManager entityManager, Entity entity, int selectedBuffIndex, StringBuilder builder)
        {
            if (!UnitBuffUtility.TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent component) || component.Buffs == null || component.Buffs.Count == 0)
            {
                builder.AppendLine("No Buff. Add with:");
                builder.AppendLine("BuffId=-1");
                builder.AppendLine("Duration=-1");
                builder.AppendLine("StackCount=1");
                return;
            }

            for (int i = 0; i < component.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = component.Buffs[i];
                if (entry == null)
                    continue;

                BuffData data = DataComponent.Instance?.Get<BuffData>(entry.BuffId);
                string name = string.IsNullOrWhiteSpace(data?.Name) ? $"Buff {entry.BuffId}" : data.Name;
                builder.Append(i == selectedBuffIndex ? "> " : "  ")
                    .Append(i + 1).Append(". ").Append(name)
                    .Append("  Id=").Append(entry.BuffId)
                    .Append("  StackCount=").Append(entry.StackCount)
                    .Append("  RemainingTime=").Append(Format(entry.RemainingTime)).AppendLine();
            }

            if (TryGetBuff(entityManager, entity, selectedBuffIndex, out UnitBuffRuntimeEntry selected))
            {
                builder.AppendLine();
                builder.AppendLine("Selected Buff editable values:");
                Append(builder, "StackCount", selected.StackCount);
                Append(builder, "RemainingTime", selected.RemainingTime);
            }

            builder.AppendLine("Add Buff values: BuffId=<id>, Duration=<seconds|-1>, StackCount=<count>");
        }

        private static bool ApplyTransform(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<LocalTransform>(entity))
                return false;

            LocalTransform value = manager.GetComponentData<LocalTransform>(entity);
            bool changed = Set(ref value.Position.x, values, "Position.X") |
                           Set(ref value.Position.y, values, "Position.Y") |
                           Set(ref value.Position.z, values, "Position.Z") |
                           Set(ref value.Scale, values, "Scale");
            quaternion rotation = value.Rotation;
            bool rotationChanged = Set(ref rotation.value.x, values, "Rotation.X") |
                                   Set(ref rotation.value.y, values, "Rotation.Y") |
                                   Set(ref rotation.value.z, values, "Rotation.Z") |
                                   Set(ref rotation.value.w, values, "Rotation.W");
            if (rotationChanged)
            {
                value.Rotation = math.normalizesafe(rotation, quaternion.identity);
                changed = true;
            }

            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyFaction(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitFactionComponent>(entity) || !TryGetInt(values, "Value", out int factionValue) || !Enum.IsDefined(typeof(UnitFactionType), factionValue))
                return false;

            UnitFactionComponent value = manager.GetComponentData<UnitFactionComponent>(entity);
            value.Value = (UnitFactionType)factionValue;
            manager.SetComponentData(entity, value);
            return true;
        }

        private static bool ApplyVitality(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitVitalityComponent>(entity))
                return false;

            UnitVitalityComponent value = manager.GetComponentData<UnitVitalityComponent>(entity);
            bool changed = Set(ref value.BaseMaxHealth, values, "BaseMaxHealth") |
                           Set(ref value.BaseMaxHealthOffset, values, "BaseMaxHealthOffset") |
                           Set(ref value.CurrentHealth, values, "CurrentHealth") |
                           Set(ref value.BaseHealthRegenPerSecond, values, "BaseHealthRegenPerSecond") |
                           Set(ref value.BaseHealthRegenOffset, values, "BaseHealthRegenOffset") |
                           Set(ref value.BaseDefense, values, "BaseDefense") |
                           Set(ref value.BaseDefenseOffset, values, "BaseDefenseOffset");
            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyMana(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitManaComponent>(entity))
                return false;

            UnitManaComponent value = manager.GetComponentData<UnitManaComponent>(entity);
            bool changed = Set(ref value.BaseMaxMp, values, "BaseMaxMp") |
                           Set(ref value.BaseMaxMpOffset, values, "BaseMaxMpOffset") |
                           Set(ref value.CurrentMana, values, "CurrentMana") |
                           Set(ref value.BaseMpRegenPerSecond, values, "BaseMpRegenPerSecond") |
                           Set(ref value.BaseMpRegenPerSecondOffset, values, "BaseMpRegenPerSecondOffset");
            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyAttack(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitAttackComponent>(entity))
                return false;

            UnitAttackComponent value = manager.GetComponentData<UnitAttackComponent>(entity);
            bool changed = Set(ref value.BaseAttackPower, values, "BaseAttackPower") |
                           Set(ref value.BaseAttackPowerOffset, values, "BaseAttackPowerOffset") |
                           Set(ref value.BaseSkillRange, values, "BaseSkillRange") |
                           Set(ref value.BaseSkillRangeOffset, values, "BaseSkillRangeOffset") |
                           Set(ref value.BaseChantSpeedBonus, values, "BaseChantSpeedBonus") |
                           Set(ref value.BaseChantSpeedBonusOffset, values, "BaseChantSpeedBonusOffset");
            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyElement(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitElementComponent>(entity))
                return false;

            UnitElementComponent value = manager.GetComponentData<UnitElementComponent>(entity);
            bool changed = Set(ref value.WaterPower, values, "WaterPower") |
                           Set(ref value.FirePower, values, "FirePower") |
                           Set(ref value.LightningPower, values, "LightningPower") |
                           Set(ref value.WindPower, values, "WindPower");
            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyMove(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitMoveComponent>(entity))
                return false;

            UnitMoveComponent value = manager.GetComponentData<UnitMoveComponent>(entity);
            bool changed = Set(ref value.BaseMoveSpeed, values, "BaseMoveSpeed") |
                           Set(ref value.BaseMoveSpeedOffset, values, "BaseMoveSpeedOffset") |
                           Set(ref value.BaseMaxAcceleration, values, "BaseMaxAcceleration") |
                           Set(ref value.Direction.x, values, "Direction.X") |
                           Set(ref value.Direction.y, values, "Direction.Y") |
                           Set(ref value.StateMoveMultiplier, values, "StateMoveMultiplier") |
                           Set(ref value.Velocity.x, values, "Velocity.X") |
                           Set(ref value.Velocity.y, values, "Velocity.Y");
            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyPerception(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitPerceptionComponent>(entity))
                return false;

            UnitPerceptionComponent value = manager.GetComponentData<UnitPerceptionComponent>(entity);
            bool changed = Set(ref value.SearchRadius, values, "SearchRadius");
            if (changed)
                manager.SetComponentData(entity, value);

            if (!values.TryGetValue("Entities", out string serializedEntries) || !manager.HasBuffer<UnitPerceptionEntityElement>(entity))
                return changed;

            if (!TryParseEntityList(serializedEntries, out List<Entity> entries))
                return changed;

            DynamicBuffer<UnitPerceptionEntityElement> buffer = manager.GetBuffer<UnitPerceptionEntityElement>(entity);
            buffer.Clear();
            for (int i = 0; i < entries.Count; i++)
                buffer.Add(new UnitPerceptionEntityElement { Value = entries[i] });
            return true;
        }

        private static bool ApplyFacing(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitFacingComponent>(entity))
                return false;

            UnitFacingComponent value = manager.GetComponentData<UnitFacingComponent>(entity);
            bool changed = Set(ref value.Direction.x, values, "Direction.X") |
                           Set(ref value.Direction.y, values, "Direction.Y");
            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyControl(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitControlRuntimeComponent>(entity))
                return false;

            UnitControlRuntimeComponent value = manager.GetComponentData<UnitControlRuntimeComponent>(entity);
            bool changed = SetEnum(ref value.ActiveType, values, "ActiveType") |
                           Set(ref value.ActiveRemainingTime, values, "ActiveRemainingTime") |
                           Set(ref value.ActivePriority, values, "ActivePriority") |
                           Set(ref value.LockMove, values, "LockMove") |
                           Set(ref value.LockCast, values, "LockCast") |
                           Set(ref value.HasControl, values, "HasControl") |
                           Set(ref value.ActiveMotionVelocity.x, values, "ActiveMotionVelocity.X") |
                           Set(ref value.ActiveMotionVelocity.y, values, "ActiveMotionVelocity.Y") |
                           Set(ref value.ActiveMotionDamping, values, "ActiveMotionDamping");
            if (TryGetEntity(values, "ActiveSourceEntity", out Entity sourceEntity))
            {
                value.ActiveSourceEntity = sourceEntity;
                changed = true;
            }

            if (values.TryGetValue("Entries", out string serializedEntries) && TryParseControlEntries(serializedEntries, out FixedList512Bytes<UnitControlRuntimeEntry> entries))
            {
                value.Entries = entries;
                changed = true;
            }

            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyBuff(EntityManager manager, Entity entity, int selectedBuffIndex, Dictionary<string, string> values)
        {
            if (!TryGetBuff(manager, entity, selectedBuffIndex, out UnitBuffRuntimeEntry entry))
                return false;

            bool changed = false;
            if (TryGetInt(values, "StackCount", out int stackCount))
            {
                entry.StackCount = math.max(1, stackCount);
                changed = true;
            }
            if (TryGetFloat(values, "RemainingTime", out float remainingTime))
            {
                entry.RemainingTime = remainingTime < 0f ? -1f : math.max(0f, remainingTime);
                changed = true;
            }
            return changed;
        }

        private static bool ApplyEnabled<T>(EntityManager manager, Entity entity, Dictionary<string, string> values)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (!manager.HasComponent<T>(entity) || !TryGetInt(values, "Enabled", out int enabled))
                return false;

            manager.SetComponentEnabled<T>(entity, enabled != 0);
            return true;
        }

        private static bool ApplyDrop(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitDropComponent>(entity) || !TryGetInt(values, "DropDataId", out int dropDataId))
                return false;

            manager.SetComponentData(entity, new UnitDropComponent { DropDataId = dropDataId });
            return true;
        }

        private static bool ApplyInteractable(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitInteractableComponent>(entity))
                return false;

            UnitInteractableComponent value = manager.GetComponentData<UnitInteractableComponent>(entity);
            bool changed = SetEnum(ref value.Data.Kind, values, "Kind") |
                           Set(ref value.Data.DataId, values, "DataId") |
                           Set(ref value.Data.Amount, values, "Amount") |
                           Set(ref value.Data.Variant, values, "Variant") |
                           Set(ref value.RangeSq, values, "RangeSq") |
                           Set(ref value.IsEnabled, values, "IsEnabled");
            if (changed)
                manager.SetComponentData(entity, value);
            return changed;
        }

        private static bool ApplyPlayerCurrentSkill(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<PlayerCurrentSkillComponent>(entity))
                return false;

            PlayerCurrentSkillComponent value = manager.GetComponentObject<PlayerCurrentSkillComponent>(entity);
            bool changed = Set(ref value.CurrentChainId, values, "CurrentChainId") |
                           Set(ref value.CurrentSlotIndex, values, "CurrentSlotIndex");
            return changed;
        }

        private static bool ApplySkillRelease(EntityManager manager, Entity entity, Dictionary<string, string> values)
        {
            if (!manager.HasComponent<UnitSkillReleaseComponent>(entity) ||
                !values.TryGetValue("PendingRequests", out string serializedRequests) ||
                !TryParseSkillReleaseRequests(serializedRequests, out List<SkillReleaseRequest> requests))
            {
                return false;
            }

            UnitSkillReleaseComponent value = manager.GetComponentObject<UnitSkillReleaseComponent>(entity);
            value.PendingRequests = requests;
            return true;
        }

        private static bool TryGetBuff(EntityManager manager, Entity entity, int index, out UnitBuffRuntimeEntry entry)
        {
            entry = null;
            return UnitBuffUtility.TryGetRuntimeComponent(manager, entity, out UnitBuffRuntimeComponent component) &&
                   component.Buffs != null &&
                   index >= 0 &&
                   index < component.Buffs.Count &&
                   (entry = component.Buffs[index]) != null;
        }

        private static Dictionary<string, string> ParseValues(string text)
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return values;

            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = lines[i][..separator].Trim();
                string value = lines[i][(separator + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(key))
                    values[key] = value;
            }
            return values;
        }

        private static bool TryGetFloat(Dictionary<string, string> values, string key, out float value)
        {
            value = 0f;
            return values.TryGetValue(key, out string source) &&
                   float.TryParse(source, NumberStyles.Float, InvariantCulture, out value) &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private static bool TryGetInt(Dictionary<string, string> values, string key, out int value)
        {
            value = 0;
            return values.TryGetValue(key, out string source) &&
                   int.TryParse(source, NumberStyles.Integer, InvariantCulture, out value);
        }

        private static bool TryGetEntity(Dictionary<string, string> values, string key, out Entity entity)
        {
            entity = Entity.Null;
            return values.TryGetValue(key, out string source) && TryParseEntity(source, out entity);
        }

        private static bool Set(ref float target, Dictionary<string, string> values, string key)
        {
            if (!TryGetFloat(values, key, out float value))
                return false;
            target = value;
            return true;
        }

        private static bool Set(ref int target, Dictionary<string, string> values, string key)
        {
            if (!TryGetInt(values, key, out int value))
                return false;
            target = value;
            return true;
        }

        private static bool Set(ref byte target, Dictionary<string, string> values, string key)
        {
            if (!TryGetInt(values, key, out int value) || value < byte.MinValue || value > byte.MaxValue)
                return false;
            target = (byte)value;
            return true;
        }

        private static bool SetEnum<T>(ref T target, Dictionary<string, string> values, string key) where T : struct, Enum
        {
            if (!TryGetInt(values, key, out int value) || !Enum.IsDefined(typeof(T), value))
                return false;
            target = (T)Enum.ToObject(typeof(T), value);
            return true;
        }

        private static bool TryParseEntityList(string source, out List<Entity> entries)
        {
            entries = new List<Entity>();
            if (string.IsNullOrWhiteSpace(source))
                return true;

            string[] values = source.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                if (!TryParseEntity(values[i], out Entity entity))
                    return false;
                entries.Add(entity);
            }
            return true;
        }

        private static bool TryParseControlEntries(string source, out FixedList512Bytes<UnitControlRuntimeEntry> entries)
        {
            entries = new FixedList512Bytes<UnitControlRuntimeEntry>();
            if (string.IsNullOrWhiteSpace(source) || source.StartsWith("Type|", StringComparison.Ordinal))
                return true;

            string[] itemStrings = source.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < itemStrings.Length; i++)
            {
                string[] fields = itemStrings[i].Split('|');
                if (fields.Length != 10 ||
                    !int.TryParse(fields[0], NumberStyles.Integer, InvariantCulture, out int rawType) ||
                    !Enum.IsDefined(typeof(UnitControlType), rawType) ||
                    !TryParseFloat(fields[1], out float remainingTime) ||
                    !int.TryParse(fields[2], NumberStyles.Integer, InvariantCulture, out int priority) ||
                    !byte.TryParse(fields[3], NumberStyles.Integer, InvariantCulture, out byte lockMove) ||
                    !byte.TryParse(fields[4], NumberStyles.Integer, InvariantCulture, out byte lockCast) ||
                    !byte.TryParse(fields[5], NumberStyles.Integer, InvariantCulture, out byte interrupt) ||
                    !TryParseEntity(fields[6], out Entity sourceEntity) ||
                    !TryParseFloat(fields[7], out float velocityX) ||
                    !TryParseFloat(fields[8], out float velocityY) ||
                    !TryParseFloat(fields[9], out float damping))
                {
                    return false;
                }

                if (entries.Length >= entries.Capacity)
                    return false;

                entries.Add(new UnitControlRuntimeEntry
                {
                    ControlType = (UnitControlType)rawType,
                    RemainingTime = remainingTime,
                    Priority = priority,
                    LockMove = lockMove,
                    LockCast = lockCast,
                    InterruptOnApply = interrupt,
                    SourceEntity = sourceEntity,
                    MotionVelocity = new float2(velocityX, velocityY),
                    MotionDamping = damping,
                });
            }
            return true;
        }

        private static bool TryParseEntity(string source, out Entity entity)
        {
            entity = Entity.Null;
            if (string.Equals(source, "null", StringComparison.OrdinalIgnoreCase))
                return true;

            string[] parts = source.Split(':');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, InvariantCulture, out int index) ||
                !int.TryParse(parts[1], NumberStyles.Integer, InvariantCulture, out int version))
            {
                return false;
            }

            entity = new Entity { Index = index, Version = version };
            return true;
        }

        private static bool TryParseSkillReleaseRequests(string source, out List<SkillReleaseRequest> requests)
        {
            requests = new List<SkillReleaseRequest>();
            if (string.IsNullOrWhiteSpace(source))
                return true;

            string[] items = source.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < items.Length; i++)
            {
                string[] fields = items[i].Split('|');
                if (fields.Length != 13 ||
                    !int.TryParse(fields[0], NumberStyles.Integer, InvariantCulture, out int skillId) ||
                    !TryParseEntity(fields[1], out Entity originEntity) ||
                    !TryParseFloat(fields[2], out float originX) ||
                    !TryParseFloat(fields[3], out float originY) ||
                    !TryParseFloat(fields[4], out float originZ) ||
                    !TryParseFloat(fields[5], out float facingX) ||
                    !TryParseFloat(fields[6], out float facingY) ||
                    !TryParseBool(fields[7], out bool hasTargetEntity) ||
                    !TryParseEntity(fields[8], out Entity targetEntity) ||
                    !TryParseBool(fields[9], out bool hasTargetPosition) ||
                    !TryParseFloat(fields[10], out float targetX) ||
                    !TryParseFloat(fields[11], out float targetY) ||
                    !TryParseFloat(fields[12], out float targetZ))
                {
                    return false;
                }

                requests.Add(new SkillReleaseRequest
                {
                    SkillId = skillId,
                    OriginEntity = originEntity,
                    OriginPosition = new float3(originX, originY, originZ),
                    OriginFacing = new float2(facingX, facingY),
                    HasTargetEntity = hasTargetEntity,
                    TargetEntity = targetEntity,
                    HasTargetPosition = hasTargetPosition,
                    TargetPosition = new float3(targetX, targetY, targetZ),
                });
            }

            return true;
        }

        private static bool TryParseFloat(string source, out float value)
        {
            return float.TryParse(source, NumberStyles.Float, InvariantCulture, out value) &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private static bool TryParseBool(string source, out bool value)
        {
            value = false;
            return source == "1" || bool.TryParse(source, out value);
        }

        private static void Append(StringBuilder builder, string key, float value)
        {
            builder.Append(key).Append('=').Append(Format(value)).AppendLine();
        }

        private static void Append(StringBuilder builder, string key, int value)
        {
            builder.Append(key).Append('=').Append(value).AppendLine();
        }

        private static void Append(StringBuilder builder, string key, byte value)
        {
            builder.Append(key).Append('=').Append(value).AppendLine();
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').Append(value).AppendLine();
        }

        private static string Format(float value) => value.ToString("0.###", InvariantCulture);

        private static string Format(Entity entity) => entity == Entity.Null ? "null" : $"{entity.Index}:{entity.Version}";
    }
}
