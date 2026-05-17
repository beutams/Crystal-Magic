using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

[FactoryKey("UnitCanStartCastSource")]
[EditorLabel("可以开始施法")]
public class UnitCanStartCastSource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        return _context.HasRuntimeEntity &&
            _context.EntityManager.Exists(_context.Entity) &&
            _context.EntityManager.HasComponent<UnitIntentComponent>(_context.Entity);
    }

    public float GetValue()
    {
        if (!CanUse())
            return 0f;

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
        SkillChainSlotData slotData = SkillChainResolver.GetFirstSlot(skillConfig, runtimeSkillData);
        SkillData baseSkill = SkillChainResolver.GetSkillData(slotData);
        if (baseSkill == null)
            return 0f;

        SkillModifierSet modifiers = SkillResolver.CollectModifiers(_context.EntityManager, _context.Entity, baseSkill, slotData);
        UnitAttackComponent? attack = _context.EntityManager.HasComponent<UnitAttackComponent>(_context.Entity)
            ? _context.EntityManager.GetComponentData<UnitAttackComponent>(_context.Entity)
            : null;
        UnitElementComponent? element = _context.EntityManager.HasComponent<UnitElementComponent>(_context.Entity)
            ? _context.EntityManager.GetComponentData<UnitElementComponent>(_context.Entity)
            : null;
        ResolvedSkillData resolvedSkill = SkillResolver.Resolve(baseSkill, modifiers, attack, element);
        if (resolvedSkill == null)
            return 0f;

        if (!_context.EntityManager.HasComponent<UnitManaComponent>(_context.Entity))
            return resolvedSkill.MpCost <= 0 ? 1f : 0f;

        UnitManaComponent mana = _context.EntityManager.GetComponentData<UnitManaComponent>(_context.Entity);
        return mana.CurrentMana >= resolvedSkill.MpCost ? 1f : 0f;
    }
}
