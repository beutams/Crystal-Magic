using System;
using CrystalMagic.Core;
using Server;
using Unity.Entities;

[Serializable]
public sealed class NetworkPlayerSkillChainStateData : NetworkStateData
{
    public int skillChainIndex;

    public override void Apply(NetworkStateApplyContext context)
    {
        // 远端玩家没有本地输入职能，状态同步不能为其补回输入组件。
        if (!context.TryGetEntity(unitId, out Entity entity) ||
            !context.EntityManager.HasComponent<PlayerInputComponent>(entity))
            return;

        if (context.IsClient &&
            FrameManagerUtility.TryGet(context.EntityManager, out ClientFrameManager frame) &&
            !frame.ShouldApplySkillChainState(context.Frame))
        {
            return;
        }

        PlayerInputComponent input = context.EntityManager.GetComponentData<PlayerInputComponent>(entity);
        int previousIndex = input.SkillChainIndex;
        input.SkillChainIndex = skillChainIndex;
        context.EntityManager.SetComponentData(entity, input);
        if (context.IsClient && previousIndex != skillChainIndex)
            EventComponent.Instance.Publish(new CommonGameEvent(PlayerInputComponent.SkillChainChangedEventName));
    }
}
