using CrystalMagic.Game.MapDemo;
using UnityEngine;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core
{
    public sealed class RuntimeDataComponent : SingletonNonMono<RuntimeDataComponent>
    {
        public const string SkillRuntimeDataChangedEventName = "Runtime.Skill.Changed";
        public const string PropRuntimeDataChangedEventName = "Runtime.Prop.Changed";

        private readonly RuntimeSkillData _skillData = new();
        private readonly RuntimePropData _propData = new();
        private readonly RuntimeDungeonMapData _dungeonMapData = new();

        public RuntimeSkillData GetSkillData()
        {
            return _skillData;
        }

        public RuntimePropData GetPropData()
        {
            return _propData;
        }

        public RuntimeDungeonMapData GetDungeonMapData()
        {
            return _dungeonMapData;
        }

        public void Reset()
        {
            _skillData.CurrentSkillChainIndex = 0;
            _propData.SharedCooldownRemaining = 0f;
            _dungeonMapData.Clear();
        }

        public void InitializeForGameRun()
        {
            Reset();
            NotifySkillDataChanged();
            NotifyPropDataChanged();
        }

        public void SetCurrentSkillChainIndex(int index, SkillCData skillConfig = null)
        {
            int maxIndex = skillConfig?.Chains != null && skillConfig.Chains.Length > 0
                ? skillConfig.Chains.Length - 1
                : 0;
            int clampedIndex = Mathf.Clamp(index, 0, maxIndex);
            if (_skillData.CurrentSkillChainIndex == clampedIndex)
                return;

            _skillData.CurrentSkillChainIndex = clampedIndex;
            NotifySkillDataChanged();
        }

        public void SelectNextSkillChain(SkillCData skillConfig = null)
        {
            int skillChainCount = GetSkillChainCount(skillConfig);
            if (skillChainCount <= 0)
                return;

            int nextIndex = (_skillData.CurrentSkillChainIndex + 1) % skillChainCount;
            SetCurrentSkillChainIndex(nextIndex, skillConfig);
        }

        public int GetSkillChainCount(SkillCData skillConfig = null)
        {
            skillConfig ??= SaveDataComponent.Instance?.GetSkillData();
            return skillConfig?.Chains != null ? skillConfig.Chains.Length : 0;
        }

        public void TickPropSharedCooldown(float deltaTime)
        {
            if (_propData.SharedCooldownRemaining <= 0f)
                return;

            float nextValue = Mathf.Max(0f, _propData.SharedCooldownRemaining - Mathf.Max(0f, deltaTime));
            if (Mathf.Approximately(nextValue, _propData.SharedCooldownRemaining))
                return;

            _propData.SharedCooldownRemaining = nextValue;
            NotifyPropDataChanged();
        }

        public void StartPropSharedCooldown(float cooldownSeconds)
        {
            float nextValue = Mathf.Max(0f, cooldownSeconds);
            if (Mathf.Approximately(_propData.SharedCooldownRemaining, nextValue))
                return;

            _propData.SharedCooldownRemaining = nextValue;
            NotifyPropDataChanged();
        }

        public void NotifySkillDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(SkillRuntimeDataChangedEventName, _skillData));
        }

        public void NotifyPropDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(PropRuntimeDataChangedEventName, _propData));
        }

        public void SetCurrentDungeonLayout(
            DungeonMakerTunnelingResult layout,
            RuntimeDungeonSceneData sceneData,
            int floor,
            int seed,
            int attemptCount)
        {
            _dungeonMapData.Layout = layout;
            _dungeonMapData.SceneData = sceneData;
            _dungeonMapData.Floor = Mathf.Max(1, floor);
            _dungeonMapData.Seed = seed;
            _dungeonMapData.AttemptCount = Mathf.Max(1, attemptCount);
        }
    }

    public sealed class RuntimeSkillData
    {
        public int CurrentSkillChainIndex;
    }

    public sealed class RuntimePropData
    {
        public float SharedCooldownRemaining;
    }

    public sealed class RuntimeDungeonMapData
    {
        public DungeonMakerTunnelingResult Layout;
        public RuntimeDungeonSceneData SceneData;
        public int Floor;
        public int Seed;
        public int AttemptCount;

        public bool HasLayout => Layout != null;

        public void Clear()
        {
            Layout = null;
            SceneData = null;
            Floor = 0;
            Seed = 0;
            AttemptCount = 0;
        }
    }

    public sealed class RuntimeDungeonSceneData
    {
        public int ThemeId;
        public string ThemeKey;
        public bool IsBossFloor;
        public float CellWorldSize;
        public float ExitInteractionRange;
        public string CorridorMaterialPath;
        public string RoomMaterialPath;
        public string AnteRoomMaterialPath;
        public string WallMaterialPath;
        public string StartMarkerMaterialPath;
        public string ExitClosedMaterialPath;
        public string ExitOpenMaterialPath;
        public RuntimeDungeonObjectData StartObject;
        public RuntimeDungeonObjectData NextLevelEntranceObject;
        public List<RuntimeDungeonMonsterSpawnData> MonsterSpawns = new();
        public List<RuntimeDungeonTreasureSpawnData> TreasureSpawns = new();
    }

    public sealed class RuntimeDungeonObjectData
    {
        public int RegionId;
        public int TileIndex;
        public Vector2Int SourceCoordinate;
        public Vector2Int DisplayCoordinate;
        public Vector3 WorldPosition;
        public bool BlocksMovement;
        public bool RequiresRoomClear;
    }

    public sealed class RuntimeDungeonMonsterSpawnData
    {
        public int RegionId;
        public int TileIndex;
        public int Level;
        public bool IsBoss;
        public string PrefabName;
        public Vector2Int SourceCoordinate;
        public Vector2Int DisplayCoordinate;
        public Vector3 WorldPosition;
    }

    public sealed class RuntimeDungeonTreasureSpawnData
    {
        public int RegionId;
        public int TileIndex;
        public int Level;
        public List<RuntimeDungeonTreasureRewardData> Rewards = new();
        public Vector2Int SourceCoordinate;
        public Vector2Int DisplayCoordinate;
        public Vector3 WorldPosition;
    }

    public sealed class RuntimeDungeonTreasureRewardData
    {
        public DropRewardType RewardType;
        public int ItemId = -1;
        public float Chance = 1f;
        public int MinQuantity = 1;
        public int MaxQuantity = 1;
    }
}
