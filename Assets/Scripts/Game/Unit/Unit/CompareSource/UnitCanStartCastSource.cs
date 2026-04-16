using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

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
        CharacterData characterData = SaveDataComponent.Instance?.GetCharacterData();
        SkillData firstSkill = SkillChainResolver.GetFirstSkill(characterData);
        if (firstSkill == null)
            return 0f;

        if (firstSkill.SkillType == SkillType.PositionSkill && !intent.HasCastTarget)
            return 0f;

        UnitManaComponent mana = _em.GetComponentData<UnitManaComponent>(_entity);
        return mana.CurrentMana >= firstSkill.MpCost ? 1f : 0f;
    }
}
