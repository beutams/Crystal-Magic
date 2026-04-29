using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Buff 系统
/// </summary>
[UpdateBefore(typeof(UnitMoveSystem))]
partial class UnitBuffSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (_, entity) in
            SystemAPI.Query<DynamicBuffer<UnitBuffElement>>().WithEntityAccess())
        {
            DynamicBuffer<UnitBuffElement> buffBuffer = SystemAPI.GetBuffer<UnitBuffElement>(entity);

            // ── 1. 更新时间，移除过期 Buff ────────────────
            for (int i = buffBuffer.Length - 1; i >= 0; i--)
            {
                UnitBuffElement elem = buffBuffer[i];
                elem.RemainingTime -= dt;
                if (elem.RemainingTime <= 0f)
                {
                    buffBuffer.RemoveAt(i);
                    continue;
                }
                buffBuffer[i] = elem;
            }

            // ── 2. 收集 PropertyBuff 因子 ──────────────────
            PropertyModifierSet modifiers = new();

            for (int i = 0; i < buffBuffer.Length; i++)
            {
                if (DataComponent.Instance.Get<BuffData>(buffBuffer[i].BuffId) is not PropertyBuffData prop)
                    continue;

                modifiers.Add(prop.PropertyModifiers, buffBuffer[i].StackCount);
            }

            // ── 3. 按 Component 写入（没挂的跳过）─────────
            if (EntityManager.HasComponent<UnitMoveComponent>(entity))
            {
                var move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
                move.SpeedFactor = modifiers.GetFactor(PropertyModifierChannel.MoveSpeed);
                move.SpeedBonus  = modifiers.GetBonus(PropertyModifierChannel.MoveSpeed);
                EntityManager.SetComponentData(entity, move);
            }

            if (EntityManager.HasComponent<UnitVitalityComponent>(entity))
            {
                var vit = EntityManager.GetComponentData<UnitVitalityComponent>(entity);
                vit.HealthFactor  = modifiers.GetFactor(PropertyModifierChannel.MaxHealth);
                vit.HealthBonus   = modifiers.GetBonus(PropertyModifierChannel.MaxHealth);
                vit.DefenseFactor = modifiers.GetFactor(PropertyModifierChannel.Defense);
                vit.DefenseBonus  = modifiers.GetBonus(PropertyModifierChannel.Defense);
                EntityManager.SetComponentData(entity, vit);
            }

            if (EntityManager.HasComponent<UnitAttackComponent>(entity))
            {
                var atk = EntityManager.GetComponentData<UnitAttackComponent>(entity);
                atk.AttackFactor = modifiers.GetFactor(PropertyModifierChannel.AttackPower);
                atk.AttackBonus  = modifiers.GetBonus(PropertyModifierChannel.AttackPower);
                atk.RangeFactor  = modifiers.GetFactor(PropertyModifierChannel.SkillRange);
                atk.RangeBonus   = modifiers.GetBonus(PropertyModifierChannel.SkillRange);
                EntityManager.SetComponentData(entity, atk);
            }

            if (EntityManager.HasComponent<UnitManaComponent>(entity))
            {
                var mp = EntityManager.GetComponentData<UnitManaComponent>(entity);
                mp.MpFactor = modifiers.GetFactor(PropertyModifierChannel.MaxMp);
                mp.MpBonus  = modifiers.GetBonus(PropertyModifierChannel.MaxMp);
                EntityManager.SetComponentData(entity, mp);
            }
        }
    }
}
