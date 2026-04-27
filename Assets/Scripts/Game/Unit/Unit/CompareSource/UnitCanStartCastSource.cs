using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

[FactoryKey("UnitCanStartCastSource")]
public class UnitCanStartCastSource : ISource
{
    private Entity _entity;
    private EntityManager _em;

    public void Init(Entity entity, EntityManager em)
    {
        _entity = entity;
        _em = em;
    }

    public float GetValue()
    {
        if (!_em.HasComponent<UnitIntentComponent>(_entity) || !_em.HasComponent<UnitManaComponent>(_entity))
            return 0f;

        UnitIntentComponent intent = _em.GetComponentData<UnitIntentComponent>(_entity);
        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
        SkillChainSlotData firstSlot = SkillChainResolver.GetFirstSlot(skillConfig, runtimeSkillData);
        SkillData firstSkill = SkillChainResolver.GetSkillData(firstSlot);
        if (firstSkill == null)
            return 0f;

        if (firstSkill.SkillType == SkillType.PositionSkill && !intent.HasCastTarget)
            return 0f;

        SkillModifierSet modifiers = SkillResolver.CollectModifiers(_em, _entity, firstSlot);
        ResolvedSkillData resolvedSkill = SkillResolver.Resolve(firstSkill, modifiers);
        UnitManaComponent mana = _em.GetComponentData<UnitManaComponent>(_entity);
        return resolvedSkill != null && mana.CurrentMana >= resolvedSkill.MpCost ? 1f : 0f;
    }
}
