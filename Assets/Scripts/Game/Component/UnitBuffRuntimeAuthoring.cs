using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitBuffRuntimeAuthoring : MonoBehaviour
{
    private sealed class UnitBuffRuntimeBaker : Baker<UnitBuffRuntimeAuthoring>
    {
        public override void Bake(UnitBuffRuntimeAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitBuffComponent { ModifierDirty = 1 });
            AddComponent(entity, UnitModifierComponent.CreateIdentity());
            AddBuffer<UnitBuffElement>(entity);
            AddBuffer<UnitBuffHookRequestElement>(entity);
            AddBuffer<EffectEntry>(entity);
        }
    }
}

public struct UnitBuffComponent : IComponentData
{
    public byte NetworkDirty;
    public byte ModifierDirty;
}

public struct UnitBuffElement : IBufferElementData
{
    public int BuffId;
    public int DefinitionIndex;
    public float RemainingTime;
    public float ElapsedTime;
    public int StackCount;
    public Entity OriginEntity;
    public int SourceSkillId;
}

public struct UnitBuffHookRequestElement : IBufferElementData
{
    public SkillHookType HookType;
    public SkillTriggerSource TriggerSource;
    public Entity OriginEntity;
    public Entity OtherEntity;
    public int SourceSkillId;
    public float3 Position;
    public float TriggerValue;
    public byte HasOriginEntity;
    public byte HasOtherEntity;
    public byte HasPosition;
}

[UnitSourceProvider(typeof(UnitBuffComponent), typeof(UnitBuffRuntimeAuthoring))]
public static class UnitBuffSource
{
    [UnitSourceGet(0, "unit.buffs.count", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.buffs.idAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(2, "unit.buffs.remainingTimeAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(3, "unit.buffs.stackCountAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(4, "unit.buffs.hasOriginAt", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(5, "unit.buffs.originEntityAt", UnitValueCategory.Entity, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(6, "unit.buffs.sourceSkillIdAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(7, "unit.buffs.findIndex", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    [UnitSourceGet(8, "unit.buffs.has", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    [UnitSourceGet(9, "unit.buffs.stackCount", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    public static bool TryGet(
        int operation,
        Entity entity,
        in ComponentLookup<UnitBuffComponent> componentLookup,
        in BufferLookup<UnitBuffElement> buffLookup,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!componentLookup.HasComponent(entity) || !buffLookup.HasBuffer(entity))
            return false;

        DynamicBuffer<UnitBuffElement> buffs = buffLookup[entity];
        if (operation == 0)
        {
            result = UnitSourceValue.FromInt(buffs.Length);
            return true;
        }

        if (operation >= 1 && operation <= 6)
        {
            if (!GetEntry(buffs, in arguments, out UnitBuffElement entry))
                return false;

            result = operation switch
            {
                1 => UnitSourceValue.FromInt(entry.BuffId),
                2 => UnitSourceValue.FromFloat(entry.RemainingTime),
                3 => UnitSourceValue.FromInt(entry.StackCount),
                4 => UnitSourceValue.FromBool(entry.OriginEntity != Entity.Null),
                5 => UnitSourceValue.FromEntity(entry.OriginEntity),
                6 => UnitSourceValue.FromInt(entry.SourceSkillId),
                _ => UnitSourceValue.None,
            };
            return true;
        }

        int index = FindIndex(buffs, in arguments);
        switch (operation)
        {
            case 7:
                result = UnitSourceValue.FromInt(index);
                return true;
            case 8:
                result = UnitSourceValue.FromBool(index >= 0);
                return true;
            case 9:
                result = UnitSourceValue.FromInt(index >= 0 ? buffs[index].StackCount : 0);
                return true;
            default:
                return false;
        }
    }

    [UnitSourceSet(0, "unit.buffs.remove", UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    [UnitSourceSet(1, "unit.buffs.removeStacks", UnitValueCategory.Number, UnitValueCategory.Number,
        ParameterNames = new[] { "BuffId", "StackCount" })]
    public static bool TrySet(
        int operation,
        Entity entity,
        ref ComponentLookup<UnitBuffComponent> componentLookup,
        ref BufferLookup<UnitBuffElement> buffLookup,
        in UnitSourceArguments arguments)
    {
        if (!arguments.TryGetInt(0, out int buffId) ||
            !componentLookup.HasComponent(entity) ||
            !buffLookup.HasBuffer(entity))
        {
            return false;
        }

        RefRW<UnitBuffComponent> component = componentLookup.GetRefRW(entity);
        DynamicBuffer<UnitBuffElement> buffs = buffLookup[entity];

        if (operation == 0)
        {
            UnitBuffUtility.RemoveAll(ref component.ValueRW, buffs, buffId);
            return true;
        }

        return operation == 1 && arguments.TryGetInt(1, out int stackCount) &&
               UnitBuffUtility.TryRemoveStacks(ref component.ValueRW, buffs, buffId, stackCount);
    }

    private static bool GetEntry(
        DynamicBuffer<UnitBuffElement> buffs,
        in UnitSourceArguments arguments,
        out UnitBuffElement entry)
    {
        entry = default;
        if (!arguments.TryGetInt(0, out int index) || index < 0 || index >= buffs.Length)
            return false;

        entry = buffs[index];
        return true;
    }

    private static int FindIndex(DynamicBuffer<UnitBuffElement> buffs, in UnitSourceArguments arguments)
    {
        if (!arguments.TryGetInt(0, out int buffId))
            return -1;

        for (int i = 0; i < buffs.Length; i++)
        {
            if (buffs[i].BuffId == buffId)
                return i;
        }

        return -1;
    }
}
