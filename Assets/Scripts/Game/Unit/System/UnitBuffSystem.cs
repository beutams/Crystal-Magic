using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Buff 系统——
/// 1. 每帧更新 Buff 剩余时间，移除过期 Buff
/// 2. 遍历 PropertyBuffData，按 Component 分别写入 Factor/Bonus
///    每个 Component 独立，Entity 没挂对应 Component 则跳过该部分
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
            float moveFactor = 1f, moveBonus = 0f;
            float healthFactor = 1f, healthBonus = 0f;
            float defenseFactor = 1f, defenseBonus = 0f;
            float attackFactor = 1f, attackBonus = 0f;
            float rangeFactor = 1f, rangeBonus = 0f;
            float mpFactor = 1f, mpBonus = 0f;

            for (int i = 0; i < buffBuffer.Length; i++)
            {
                if (DataComponent.Instance.Get<BuffData>(buffBuffer[i].BuffId) is not PropertyBuffData prop)
                    continue;

                int stacks = buffBuffer[i].StackCount;

                moveFactor    += prop.MoveSpeedFactor   * stacks;
                moveBonus     += prop.MoveSpeedBonus    * stacks;
                healthFactor  += prop.MaxHealthFactor    * stacks;
                healthBonus   += prop.MaxHealthBonus     * stacks;
                defenseFactor += prop.DefenseFactor      * stacks;
                defenseBonus  += prop.DefenseBonus       * stacks;
                attackFactor  += prop.AttackPowerFactor  * stacks;
                attackBonus   += prop.AttackPowerBonus   * stacks;
                rangeFactor   += prop.SkillRangeFactor   * stacks;
                rangeBonus    += prop.SkillRangeBonus    * stacks;
                mpFactor      += prop.MaxMpFactor        * stacks;
                mpBonus       += prop.MaxMpBonus         * stacks;
            }

            // ── 3. 按 Component 写入（没挂的跳过）─────────
            if (EntityManager.HasComponent<UnitMoveComponent>(entity))
            {
                var move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
                move.SpeedFactor = moveFactor;
                move.SpeedBonus  = moveBonus;
                EntityManager.SetComponentData(entity, move);
            }

            if (EntityManager.HasComponent<UnitVitalityComponent>(entity))
            {
                var vit = EntityManager.GetComponentData<UnitVitalityComponent>(entity);
                vit.HealthFactor  = healthFactor;
                vit.HealthBonus   = healthBonus;
                vit.DefenseFactor = defenseFactor;
                vit.DefenseBonus  = defenseBonus;
                EntityManager.SetComponentData(entity, vit);
            }

            if (EntityManager.HasComponent<UnitAttackComponent>(entity))
            {
                var atk = EntityManager.GetComponentData<UnitAttackComponent>(entity);
                atk.AttackFactor = attackFactor;
                atk.AttackBonus  = attackBonus;
                atk.RangeFactor  = rangeFactor;
                atk.RangeBonus   = rangeBonus;
                EntityManager.SetComponentData(entity, atk);
            }

            if (EntityManager.HasComponent<UnitManaComponent>(entity))
            {
                var mp = EntityManager.GetComponentData<UnitManaComponent>(entity);
                mp.MpFactor = mpFactor;
                mp.MpBonus  = mpBonus;
                EntityManager.SetComponentData(entity, mp);
            }
        }
    }
}
