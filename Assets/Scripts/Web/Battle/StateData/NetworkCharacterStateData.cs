using System;
using CrystalMagic.Core;
using Server;
using Unity.Entities;

[Serializable]
public sealed class NetworkCharacterStateData : NetworkStateData
{
    public ulong revision;
    public CharacterData characterData;
    // 普通置脏同步为空，编辑回执带上原请求的 ID。
    public Guid requestId;
    public bool accepted;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.IsClient)
            return;

        if (characterData != null && context.TryGetEntity(unitId, out Entity player) &&
            context.EntityManager.HasComponent<PlayerCharacterComponent>(player))
        {
            PlayerCharacterComponent character = context.EntityManager.GetComponentObject<PlayerCharacterComponent>(player);
            if (revision >= character.Revision)
            {
                character.Data = PlayerCharacterUtility.Clone(characterData);
                character.Revision = revision;
                character.NetworkDirty = 0;
                PlayerCharacterUtility.Rebuild(context.EntityManager, player);
                PlayerCharacterUtility.Refresh(context.EntityManager, player);
            }
        }

        if (FrameManagerUtility.TryGet(context.EntityManager, out ClientFrameManager frame) &&
            frame.CompleteCharacterEdit(unitId, requestId) && !accepted)
            PlayerCharacterUtility.ShowEditConflict();
    }
}
