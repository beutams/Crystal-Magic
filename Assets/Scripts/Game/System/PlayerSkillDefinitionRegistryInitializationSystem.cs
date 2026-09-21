using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitSourceDispatcherSystem))]
public partial class PlayerSkillDefinitionRegistryInitializationSystem : SystemBase
{
    private BlobAssetReference<PlayerSkillDefinitionRegistryBlob> _registry;

    protected override void OnUpdate()
    {
        if (_registry.IsCreated)
        {
            Enabled = false;
            return;
        }

        DataTable<SkillData> table = DataComponent.Instance.GetTable<SkillData>();
        if (table == null)
            return;

        List<SkillData> skills = new(table.GetAll());
        skills.RemoveAll(static skill => skill == null);
        skills.Sort(static (left, right) => left.Id.CompareTo(right.Id));

        ModifierConfig modifierConfig = ConfigComponent.Instance.Get<ModifierConfig>();
        Array channelValues = Enum.GetValues(typeof(SkillModifierChannel));
        List<PlayerSkillModifierMinimumFactorBlob> minimumFactors = new(channelValues.Length);
        for (int index = 0; index < channelValues.Length; index++)
        {
            SkillModifierChannel channel = (SkillModifierChannel)channelValues.GetValue(index);
            if (SkillModifierChannelUtility.IsInternalChannel(channel))
                continue;

            minimumFactors.Add(new PlayerSkillModifierMinimumFactorBlob
            {
                Channel = channel,
                MinimumFactor = modifierConfig?.GetSkillModifierMinimumFactor(channel) ?? 0f,
            });
        }
        minimumFactors.Sort(static (left, right) => ((int)left.Channel).CompareTo((int)right.Channel));

        using BlobBuilder builder = new(Allocator.Temp);
        ref PlayerSkillDefinitionRegistryBlob root = ref builder.ConstructRoot<PlayerSkillDefinitionRegistryBlob>();
        BlobBuilderArray<PlayerSkillDefinitionBlob> skillArray = builder.Allocate(ref root.Skills, skills.Count);
        for (int index = 0; index < skills.Count; index++)
        {
            SkillData source = skills[index];
            FixedString128Bytes runtimeType = default;
            CopyError copyError = runtimeType.CopyFrom(source.EffectiveRuntimeType ?? string.Empty);
            skillArray[index] = new PlayerSkillDefinitionBlob
            {
                Id = source.Id,
                MpCost = source.MpCost,
                ChantDuration = source.ChantDuration,
                CastingMoveMultiplier = math.max(0f, source.CastingMoveMultiplier),
                RuntimeType = runtimeType,
                RuntimeTypeValid = copyError == CopyError.None ? (byte)1 : (byte)0,
                InputType = source.InputType,
            };
        }

        BlobBuilderArray<PlayerSkillModifierMinimumFactorBlob> minimumFactorArray =
            builder.Allocate(ref root.ModifierMinimumFactors, minimumFactors.Count);
        for (int index = 0; index < minimumFactors.Count; index++)
            minimumFactorArray[index] = minimumFactors[index];

        _registry = builder.CreateBlobAssetReference<PlayerSkillDefinitionRegistryBlob>(Allocator.Persistent);
        SystemAPI.GetSingletonRW<PlayerSkillDefinitionRegistryComponent>().ValueRW.Value = _registry;

        Enabled = false;
    }

    protected override void OnDestroy()
    {
        if (_registry.IsCreated)
            _registry.Dispose();
    }
}
