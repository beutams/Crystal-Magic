using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Server;
using Unity.Entities;

[Serializable]
public sealed class NetworkCharacterEditData : NetworkStateData
{
    public Guid requestId;
    public ulong revision;
    public CharacterData characterData;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.IsClient || !FrameManagerUtility.TryGet(context.EntityManager, out ServerFrameManager frame))
            return;

        NetworkCharacterStateData result = new()
        {
            unitId = unitId,
            requestId = requestId,
        };
        if (context.TryGetEntity(unitId, out Entity player) &&
            context.EntityManager.HasComponent<PlayerCharacterComponent>(player))
        {
            PlayerCharacterComponent character = context.EntityManager.GetComponentObject<PlayerCharacterComponent>(player);
            result.accepted = !BattlePlayerStatusUtility.IsInputLocked(context.EntityManager, player) &&
                              character.TryEdit(revision, characterData);
            if (result.accepted)
                PlayerCharacterUtility.Rebuild(context.EntityManager, player);
            result.revision = character.Revision;
            result.characterData = PlayerCharacterUtility.Clone(character.Data);
        }

        // 结果跟随本帧输出。即使实体已不存在，也必须返回失败，释放客户端的等待。
        if (!frame.sendOrder.TryGetValue(frame.currentFrame, out Queue<NetworkStateData> states))
        {
            states = new Queue<NetworkStateData>();
            frame.sendOrder.Add(frame.currentFrame, states);
        }
        states.Enqueue(result);
    }
}
