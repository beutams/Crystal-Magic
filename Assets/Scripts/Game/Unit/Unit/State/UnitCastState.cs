using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Mathematics;
using UnityEngine;

[FactoryKey("UnitCastState")]
public class UnitCastState : AUnitState
{
    public override void OnEnter()
    {
        if (!EntityManager.HasComponent<UnitSkillComponent>(Entity) ||
            !EntityManager.HasComponent<UnitIntentComponent>(Entity) ||
            !EntityManager.HasComponent<UnitPerceptionComponent>(Entity))
            return;

        UnitSkillComponent unitSkill = EntityManager.GetComponentData<UnitSkillComponent>(Entity);
        UnitPerceptionComponent perception = EntityManager.GetComponentData<UnitPerceptionComponent>(Entity);
        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);

        int selectedIndex = SelectSkillIndex(unitSkill, perception.TargetDistance);
        if (selectedIndex < 0)
        {
            unitSkill.ClearPending();
            unitSkill.ClearRequest();
            EntityManager.SetComponentData(Entity, unitSkill);
            return;
        }

        unitSkill.HasPendingCast = true;
        unitSkill.PendingSkillIndex = selectedIndex;
        unitSkill.HasLockedTarget = true;
        unitSkill.LockedTargetPosition = intent.CastTargetPosition;
        EntityManager.SetComponentData(Entity, unitSkill);
    }

    public override void OnUpdate(float deltaTime)
    {
    }

    public override void OnExit()
    {
    }

    private int SelectSkillIndex(UnitSkillComponent unitSkill, float targetDistance)
    {
        int totalWeight = 0;
        for (int i = 0; i < unitSkill.Skills.Length; i++)
        {
            UnitSkillEntry entry = unitSkill.Skills[i];
            if (!CanUseSkill(unitSkill, entry, targetDistance, out _))
                continue;

            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (totalWeight <= 0)
            return -1;

        if (unitSkill.RequestMode == UnitSkillSelectionMode.ExactSkillId)
        {
            for (int i = 0; i < unitSkill.Skills.Length; i++)
            {
                UnitSkillEntry entry = unitSkill.Skills[i];
                if (CanUseSkill(unitSkill, entry, targetDistance, out _))
                    return i;
            }

            return -1;
        }

        int random = UnityEngine.Random.Range(0, totalWeight);
        int accum = 0;
        for (int i = 0; i < unitSkill.Skills.Length; i++)
        {
            UnitSkillEntry entry = unitSkill.Skills[i];
            if (!CanUseSkill(unitSkill, entry, targetDistance, out _))
                continue;

            accum += Mathf.Max(1, entry.Weight);
            if (random < accum)
                return i;
        }

        return -1;
    }

    private bool CanUseSkill(UnitSkillComponent unitSkill, UnitSkillEntry entry, float targetDistance, out ResolvedSkillData resolvedSkill)
    {
        if (entry.SkillId <= 0)
        {
            resolvedSkill = null;
            return false;
        }
        if (entry.CooldownRemaining > 0f)
        {
            resolvedSkill = null;
            return false;
        }
        if (targetDistance < math.max(0f, entry.MinDistance))
        {
            resolvedSkill = null;
            return false;
        }
        if (entry.MaxDistance > 0f && targetDistance > entry.MaxDistance)
        {
            resolvedSkill = null;
            return false;
        }

        if (!TryResolveSkill(entry, out resolvedSkill))
            return false;

        if (EntityManager.HasComponent<UnitManaComponent>(Entity))
        {
            UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(Entity);
            if (mana.CurrentMana < resolvedSkill.MpCost)
                return false;
        }

        return unitSkill.RequestMode switch
        {
            UnitSkillSelectionMode.None => false,
            UnitSkillSelectionMode.RandomAll => true,
            UnitSkillSelectionMode.RandomTagMask => unitSkill.RequestedTagMask != 0 && (entry.TagMask & unitSkill.RequestedTagMask) != 0,
            UnitSkillSelectionMode.ExactSkillId => entry.SkillId == unitSkill.RequestedSkillId,
            _ => false,
        };
    }

    private bool TryResolveSkill(UnitSkillEntry entry, out ResolvedSkillData resolvedSkill)
    {
        resolvedSkill = null;

        DataComponent dataComponent = DataComponent.Instance;
        if (dataComponent == null)
            return false;

        SkillData baseSkill = dataComponent.Get<SkillData>(entry.SkillId);
        if (baseSkill == null)
            return false;

        SkillChainSlotData slotData = entry.SkillEffectId > 0
            ? new SkillChainSlotData { SkillEffectId = entry.SkillEffectId }
            : null;

        SkillModifierSet modifiers = SkillResolver.CollectModifiers(EntityManager, Entity, slotData);
        UnitAttackComponent? attack = EntityManager.HasComponent<UnitAttackComponent>(Entity)
            ? EntityManager.GetComponentData<UnitAttackComponent>(Entity)
            : null;
        UnitElementComponent? element = EntityManager.HasComponent<UnitElementComponent>(Entity)
            ? EntityManager.GetComponentData<UnitElementComponent>(Entity)
            : null;

        resolvedSkill = SkillResolver.Resolve(baseSkill, modifiers, attack, element);
        return resolvedSkill != null;
    }
}
