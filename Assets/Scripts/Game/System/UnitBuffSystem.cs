using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Server;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(BuffEffectRegistryInitializationSystem))]
[UpdateBefore(typeof(UnitRecoverySystem))]
public partial struct UnitBuffSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuffEffectRegistryComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (FrameManagerUtility.TryGet(state.EntityManager, out FrameManager frame) && !frame.running)
            return;

        BlobAssetReference<BuffEffectRegistryBlob> registry =
            SystemAPI.GetSingleton<BuffEffectRegistryComponent>().Value;
        if (!registry.IsCreated)
            return;

        state.Dependency = new UnitBuffJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Registry = registry,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct UnitBuffJob : IJobEntity
{
    public float DeltaTime;
    public BlobAssetReference<BuffEffectRegistryBlob> Registry;

    private void Execute(
        Entity entity,
        ref UnitBuffComponent component,
        DynamicBuffer<UnitBuffElement> buffs,
        DynamicBuffer<UnitBuffHookRequestElement> hookRequests,
        DynamicBuffer<EffectEntry> effectRequests)
    {
        ProcessHookRequests(entity, ref component, buffs, hookRequests, effectRequests);
        UpdateBuffs(entity, ref component, buffs, effectRequests);
    }

    private void ProcessHookRequests(
        Entity targetEntity,
        ref UnitBuffComponent component,
        DynamicBuffer<UnitBuffElement> buffs,
        DynamicBuffer<UnitBuffHookRequestElement> hookRequests,
        DynamicBuffer<EffectEntry> effectRequests)
    {
        for (int requestIndex = 0; requestIndex < hookRequests.Length; requestIndex++)
        {
            UnitBuffHookRequestElement request = hookRequests[requestIndex];
            for (int buffIndex = buffs.Length - 1; buffIndex >= 0; buffIndex--)
            {
                UnitBuffElement buff = buffs[buffIndex];
                if (!HasValidDefinition(in buff))
                {
                    buffs.RemoveAt(buffIndex);
                    MarkDirty(ref component);
                    continue;
                }

                ref BuffDefinitionBlob definition = ref Registry.Value.Buffs[buff.DefinitionIndex];
                int previousStackCount = buff.StackCount;
                for (int effectIndex = 0; effectIndex < definition.Effects.Length; effectIndex++)
                {
                    BuffEffectReference effectReference = definition.Effects[effectIndex];
                    if (effectReference.Type != BuffEffectType.TriggeredEffect ||
                        effectReference.Id < 0 ||
                        effectReference.Id >= Registry.Value.TriggeredEffects.Length)
                    {
                        continue;
                    }

                    BuffTriggeredEffectBlob trigger = Registry.Value.TriggeredEffects[effectReference.Id];
                    if (trigger.TriggerType != BuffTriggerType.Hook || trigger.HookType != request.HookType)
                        continue;

                    EnqueueHookEffect(targetEntity, in buff, in request, in trigger, effectRequests);
                    if (trigger.ConsumeStackOnTrigger != 0)
                    {
                        buff.StackCount = math.max(0, buff.StackCount - 1);
                        if (buff.StackCount == 0)
                            break;
                    }
                }

                if (buff.StackCount <= 0)
                {
                    buffs.RemoveAt(buffIndex);
                    MarkDirty(ref component);
                }
                else
                {
                    buffs[buffIndex] = buff;
                    if (buff.StackCount != previousStackCount)
                        MarkDirty(ref component);
                }
            }
        }

        hookRequests.Clear();
    }

    private void UpdateBuffs(
        Entity targetEntity,
        ref UnitBuffComponent component,
        DynamicBuffer<UnitBuffElement> buffs,
        DynamicBuffer<EffectEntry> effectRequests)
    {
        for (int buffIndex = buffs.Length - 1; buffIndex >= 0; buffIndex--)
        {
            UnitBuffElement buff = buffs[buffIndex];
            if (!HasValidDefinition(in buff))
            {
                buffs.RemoveAt(buffIndex);
                MarkDirty(ref component);
                continue;
            }

            ref BuffDefinitionBlob definition = ref Registry.Value.Buffs[buff.DefinitionIndex];
            bool hasInfiniteDuration = buff.RemainingTime < 0f;
            float effectiveDeltaTime = DeltaTime;
            if (!hasInfiniteDuration)
            {
                effectiveDeltaTime = math.min(DeltaTime, buff.RemainingTime);
                buff.RemainingTime = math.max(0f, buff.RemainingTime - DeltaTime);
            }

            int previousStackCount = buff.StackCount;
            if (effectiveDeltaTime > 0f)
            {
                float previousElapsedTime = buff.ElapsedTime;
                buff.ElapsedTime += effectiveDeltaTime;

                for (int effectIndex = 0; effectIndex < definition.Effects.Length; effectIndex++)
                {
                    BuffEffectReference effectReference = definition.Effects[effectIndex];
                    if (effectReference.Type != BuffEffectType.TriggeredEffect ||
                        effectReference.Id < 0 ||
                        effectReference.Id >= Registry.Value.TriggeredEffects.Length)
                    {
                        continue;
                    }

                    BuffTriggeredEffectBlob trigger = Registry.Value.TriggeredEffects[effectReference.Id];
                    if (trigger.TriggerType != BuffTriggerType.Tick || trigger.TickIntervalSeconds <= 0f ||
                        !CrossedTick(previousElapsedTime, buff.ElapsedTime, trigger.TickIntervalSeconds))
                    {
                        continue;
                    }

                    EnqueueTickEffect(targetEntity, in buff, in trigger, effectRequests);
                    if (trigger.ConsumeStackOnTrigger != 0)
                    {
                        buff.StackCount = math.max(0, buff.StackCount - 1);
                        if (buff.StackCount == 0)
                            break;
                    }
                }
            }

            if (buff.StackCount <= 0 || (!hasInfiniteDuration && buff.RemainingTime <= 0f))
            {
                buffs.RemoveAt(buffIndex);
                MarkDirty(ref component);
            }
            else
            {
                buffs[buffIndex] = buff;
                if (buff.StackCount != previousStackCount)
                    MarkDirty(ref component);
            }
        }
    }

    private bool HasValidDefinition(in UnitBuffElement buff)
    {
        if (buff.DefinitionIndex < 0 || buff.DefinitionIndex >= Registry.Value.Buffs.Length)
            return false;

        return Registry.Value.Buffs[buff.DefinitionIndex].BuffId == buff.BuffId;
    }

    private static bool CrossedTick(float previousTime, float currentTime, float interval)
    {
        return (int)math.floor(currentTime / interval) > (int)math.floor(previousTime / interval);
    }

    private static void MarkDirty(ref UnitBuffComponent component)
    {
        component.NetworkDirty = 1;
        component.ModifierDirty = 1;
    }

    private static void EnqueueTickEffect(
        Entity targetEntity,
        in UnitBuffElement buff,
        in BuffTriggeredEffectBlob trigger,
        DynamicBuffer<EffectEntry> effectRequests)
    {
        if (!trigger.EffectListId.IsValid)
            return;

        effectRequests.Add(new EffectEntry
        {
            EffectListId = trigger.EffectListId,
            RepeatCount = math.max(1, buff.StackCount),
            Completion = EffectCompletionType.None,
            Context = new EffectRequestContext
            {
                TriggerSource = SkillTriggerSource.BuffHook,
                HookType = SkillHookType.OnBuffTick,
                OriginEntity = buff.OriginEntity,
                TargetEntity = targetEntity,
                SourceSkillId = buff.SourceSkillId,
                HasOriginEntity = buff.OriginEntity != Entity.Null ? (byte)1 : (byte)0,
                HasTargetEntity = 1,
            },
        });
    }

    private static void EnqueueHookEffect(
        Entity targetEntity,
        in UnitBuffElement buff,
        in UnitBuffHookRequestElement request,
        in BuffTriggeredEffectBlob trigger,
        DynamicBuffer<EffectEntry> effectRequests)
    {
        if (!trigger.EffectListId.IsValid)
            return;

        bool useIncomingOriginAsOther = request.HasOriginEntity != 0;
        bool hasOtherEntity = useIncomingOriginAsOther || request.HasOtherEntity != 0;
        effectRequests.Add(new EffectEntry
        {
            EffectListId = trigger.EffectListId,
            RepeatCount = math.max(1, buff.StackCount),
            Completion = EffectCompletionType.None,
            Context = new EffectRequestContext
            {
                TriggerSource = SkillTriggerSource.BuffHook,
                HookType = request.HookType,
                OriginEntity = buff.OriginEntity,
                TargetEntity = targetEntity,
                OtherEntity = useIncomingOriginAsOther ? request.OriginEntity : request.OtherEntity,
                SourceSkillId = buff.SourceSkillId,
                Position = request.Position,
                TriggerValue = request.TriggerValue,
                HasOriginEntity = buff.OriginEntity != Entity.Null ? (byte)1 : (byte)0,
                HasTargetEntity = 1,
                HasOtherEntity = hasOtherEntity ? (byte)1 : (byte)0,
                HasPosition = request.HasPosition,
            },
        });
    }
}
