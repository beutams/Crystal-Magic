using CrystalMagic.Core;
using CrystalMagic.UI;
using Newtonsoft.Json;
using Server;
using Unity.Entities;

// UI 持有编辑副本，不能直接改联机角色的权威数据。
public sealed class PlayerCharacterEdit
{
    public World World;
    public EntityManager EntityManager;
    public Entity Player;
    public ulong Revision;
    public CharacterData Data;
}

public static class PlayerCharacterUtility
{
    public static CharacterData Clone(CharacterData data)
    {
        return data == null ? null : JsonConvert.DeserializeObject<CharacterData>(JsonConvert.SerializeObject(data));
    }

    public static bool TryBeginEdit(out PlayerCharacterEdit edit)
    {
        edit = null;
        if (!GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager entityManager, out Entity player) ||
            !entityManager.HasComponent<PlayerCharacterComponent>(player) ||
            BattlePlayerStatusUtility.IsInputLocked(entityManager, player))
            return false;

        if (GameWorldContextUtility.Get(entityManager).Role == GameWorldRole.Client &&
            (!FrameManagerUtility.TryGet(entityManager, out ClientFrameManager frame) ||
             !frame.running || frame.HasPendingCharacterEdit))
            return false;

        PlayerCharacterComponent character = entityManager.GetComponentObject<PlayerCharacterComponent>(player);
        edit = new PlayerCharacterEdit
        {
            World = entityManager.World,
            EntityManager = entityManager,
            Player = player,
            Revision = character.Revision,
            Data = Clone(character.Data),
        };
        return true;
    }

    public static bool CommitEdit(PlayerCharacterEdit edit)
    {
        if (edit?.World == null || !edit.World.IsCreated ||
            !edit.EntityManager.Exists(edit.Player) ||
            BattlePlayerStatusUtility.IsInputLocked(edit.EntityManager, edit.Player))
            return false;

        EntityManager entityManager = edit.EntityManager;
        PlayerCharacterComponent character = entityManager.GetComponentObject<PlayerCharacterComponent>(edit.Player);
        if (character.Revision != edit.Revision)
        {
            ShowEditConflict();
            return false;
        }

        if (GameWorldContextUtility.Get(entityManager).Role == GameWorldRole.Client)
        {
            return FrameManagerUtility.TryGet(entityManager, out ClientFrameManager frame) &&
                   frame.TrySendCharacterEdit(new NetworkCharacterEditData
                   {
                       unitId = entityManager.GetComponentData<NetworkIdentityComponent>(edit.Player).id,
                       revision = edit.Revision,
                       characterData = edit.Data,
                   });
        }

        if (!character.TryEdit(edit.Revision, edit.Data))
            return false;
        Rebuild(entityManager, edit.Player);
        Refresh(entityManager, edit.Player);
        return true;
    }

    public static void Rebuild(EntityManager entityManager, Entity player)
    {
        CharacterData data = entityManager.GetComponentObject<PlayerCharacterComponent>(player).Data;
        EquipmentUtility.RebuildProperties(data.Equipment);
        EquipmentUtility.ApplyToUnit(entityManager, player, data.Equipment);
        data.Skills.EnsureValid();
        PlayerSkillChainUtility.Rebuild(entityManager, player);
    }

    // 服务器调用时必须传所属 World 和玩家，不能通过 SaveData 找本机主角。
    public static void MarkChanged(EntityManager entityManager, Entity player)
    {
        entityManager.GetComponentObject<PlayerCharacterComponent>(player).MarkChanged();
        Refresh(entityManager, player);
    }

    public static void Refresh(EntityManager entityManager, Entity player)
    {
        GameWorldRole role = GameWorldContextUtility.Get(entityManager).Role;
        if (role == GameWorldRole.Standalone ||
            (role == GameWorldRole.Client && entityManager.HasComponent<NetworkPlayerComponent>(player)))
            SaveDataComponent.Instance.RefreshCharacterData();
    }

    public static void ShowEditConflict()
    {
        UIComponent.Instance.Open<ConfirmSingleUI>(new ConfirmUIOpenData(
            "修改失败",
            "角色数据已更新，本次修改未生效，请基于最新数据重新操作。",
            showCancelButton: false));
    }
}
