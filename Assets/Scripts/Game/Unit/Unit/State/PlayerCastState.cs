using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

[FactoryKey("PlayerCastState")]
public class PlayerCastState : AUnitState
{
    private readonly System.Collections.Generic.List<SkillChainSlotData> _skillSlots = new();

    public override void OnEnter()
    {
        if (!EntityManager.HasComponent<PlayerSkillComponent>(Entity) ||
            !EntityManager.HasComponent<UnitIntentComponent>(Entity))
            return;

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();

        if (!SkillChainResolver.TryBuildSelectedChain(skillConfig, runtimeSkillData, _skillSlots, out int chainIndex))
            return;

        PlayerSkillComponent playerSkill = EntityManager.GetComponentData<PlayerSkillComponent>(Entity);
        playerSkill.Clear();

        for (int i = 0; i < _skillSlots.Count; i++)
        {
            SkillChainSlotData slotData = _skillSlots[i];
            SkillData skillData = SkillChainResolver.GetSkillData(slotData);
            if (skillData == null)
                continue;

            if (playerSkill.SkillIds.Length >= playerSkill.SkillIds.Capacity ||
                playerSkill.SkillEffectIds.Length >= playerSkill.SkillEffectIds.Capacity)
                break;

            playerSkill.SkillIds.Add(skillData.Id);
            playerSkill.SkillEffectIds.Add(slotData?.SkillEffectId ?? 0);
        }

        if (playerSkill.SkillIds.Length == 0)
        {
            EntityManager.SetComponentData(Entity, playerSkill);
            return;
        }

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        playerSkill.HasPendingCast = true;
        playerSkill.HasLockedTarget = true;
        playerSkill.LockedTargetPosition = intent.CastTargetPosition;
        playerSkill.ChainIndex = chainIndex;
        EntityManager.SetComponentData(Entity, playerSkill);
    }

    public override void OnUpdate(float deltaTime)
    {
    }

    public override void OnExit()
    {
    }
}
