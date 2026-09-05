using CrystalMagic.Game.MapDemo;
using UnityEngine;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;

namespace CrystalMagic.Core
{
    public sealed class RuntimeDataComponent : SingletonNonMono<RuntimeDataComponent>
    {
        public const string SkillRuntimeDataChangedEventName = "Runtime.Skill.Changed";
        public const string PropRuntimeDataChangedEventName = "Runtime.Prop.Changed";

        private readonly RuntimeSkillData _skillData = new();
        private readonly RuntimePropData _propData = new();
        private readonly RuntimeDungeonMapData _dungeonMapData = new();

        protected override void Initialize()
        {
            EventComponent.Instance.Subscribe(
                new CommonGameEvent(GameplayEventNames.PropUsed),
                HandlePropUsed);
        }

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
            StopPropSharedCooldown();
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

        public void StartPropSharedCooldown(float cooldownSeconds)
        {
            float nextValue = Mathf.Max(0f, cooldownSeconds);
            bool hasRegisteredTimer = _propData.SharedCooldownTimerId > 0;
            if (hasRegisteredTimer
                && Mathf.Approximately(TimerComponent.Instance.GetRemainingSeconds(_propData.SharedCooldownTimerId), nextValue)
                && Mathf.Approximately(_propData.SharedCooldownRemaining, nextValue))
                return;

            if (!hasRegisteredTimer)
            {
                _propData.SharedCooldownTimerId = TimerComponent.Instance.Register(
                    nextValue,
                    remainingSeconds =>
                    {
                        if (Mathf.Approximately(_propData.SharedCooldownRemaining, remainingSeconds))
                            return;

                        _propData.SharedCooldownRemaining = remainingSeconds;
                        NotifyPropDataChanged();
                    },
                    () =>
                    {
                        if (Mathf.Approximately(_propData.SharedCooldownRemaining, 0f))
                            return;

                        _propData.SharedCooldownRemaining = 0f;
                        NotifyPropDataChanged();
                    });
            }

            _propData.SharedCooldownRemaining = nextValue;
            NotifyPropDataChanged();
            if (nextValue <= 0f)
            {
                TimerComponent.Instance.ResetTimer(_propData.SharedCooldownTimerId, 0f, false);
                return;
            }

            TimerComponent.Instance.ResetTimer(_propData.SharedCooldownTimerId, nextValue);
        }

        public void NotifySkillDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(SkillRuntimeDataChangedEventName, _skillData));
        }

        public void NotifyPropDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(PropRuntimeDataChangedEventName, _propData));
        }

        public void SetCurrentOpenFieldDungeonLayout(
            OpenFieldDungeonLayout layout,
            RuntimeDungeonSceneData sceneData,
            int floor,
            int seed,
            int attemptCount)
        {
            _dungeonMapData.OpenFieldLayout = layout;
            _dungeonMapData.SceneData = sceneData;
            _dungeonMapData.Floor = Mathf.Max(1, floor);
            _dungeonMapData.Seed = seed;
            _dungeonMapData.AttemptCount = Mathf.Max(1, attemptCount);
        }

        private void HandlePropUsed(CommonGameEvent gameEvent)
        {
            GameplayEventReference reference = gameEvent.GetData<GameplayEventReference>();
            if (!reference.Value.TryGetNumber(out float cooldownSeconds))
            {
                Debug.LogError("[RuntimeDataComponent] Gameplay.Prop.Used requires a numeric reference.");
                return;
            }

            StartPropSharedCooldown(cooldownSeconds);
        }

        private void StopPropSharedCooldown(bool notify = true)
        {
            if (_propData.SharedCooldownTimerId > 0)
            {
                TimerComponent.Instance.Cancel(_propData.SharedCooldownTimerId);
                _propData.SharedCooldownTimerId = 0;
            }

            bool changed = !Mathf.Approximately(_propData.SharedCooldownRemaining, 0f);
            _propData.SharedCooldownRemaining = 0f;
            if (notify && changed)
                NotifyPropDataChanged();
        }
    }

    public sealed class RuntimeSkillData
    {
        public int CurrentSkillChainIndex;
    }

    public sealed class RuntimePropData
    {
        public float SharedCooldownRemaining;
        public int SharedCooldownTimerId;
    }

    public sealed class RuntimeDungeonMapData
    {
        public OpenFieldDungeonLayout OpenFieldLayout;
        public RuntimeDungeonSceneData SceneData;
        public int Floor;
        public int Seed;
        public int AttemptCount;

        public bool HasLayout => OpenFieldLayout != null;

        public void Clear()
        {
            OpenFieldLayout = null;
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
        public int DisplayWidth;
        public int DisplayHeight;
        public Vector3 PlayerSpawnWorldPosition;
        public RuntimeDungeonTerrainVisualData TerrainVisual = new();
        public List<RuntimeDungeonObstacleSpawnData> ObstacleSpawns = new();

        public List<RuntimeDungeonEnvironmentSpawnData> EnvironmentSpawns = new();
        public List<RuntimeDungeonSceneObjectSpawnData> SceneObjects = new();
        public List<RuntimeDungeonMonsterSpawnData> MonsterSpawns = new();
    }

    public enum RuntimeDungeonTilemapLayer
    {
        Void,
        Ground,
        Decoration,
        Obstacle,
    }

    public enum RuntimeDungeonTilemapRole
    {
        Abyss,
        VoidWall,
        VoidTransition,
        GroundBase,
        Decoration,
        ObstacleTop,
        ObstacleWall,
        ObstacleTransition,
    }

    public sealed class RuntimeDungeonTerrainVisualData
    {
        public float CellWorldSize = 1f;
        public Vector2 WorldOrigin;
        public List<RuntimeDungeonRuleTilePlacement> Placements = new();
    }

    public sealed class RuntimeDungeonRuleTilePlacement
    {
        public RuntimeDungeonTilemapLayer Layer;
        public RuntimeDungeonTilemapRole Role;
        public string RuleTilePath;
        public Vector2Int Cell;
        public int HeightSteps;
    }

    public sealed class RuntimeDungeonObstacleSpawnData
    {
        public string SpritePath;
        public string SpriteName;
        public Vector4 SpriteUv;
        public bool HasSpriteUv;
        public Vector3 WorldPosition;
        public Vector2 VisualSortAnchor;
        public float SortAnchorWorldY;
        public int RotationQuarterTurns;
        public bool FlippedX;
        public List<Vector2Int> CollisionCells = new();
    }

    public sealed class RuntimeDungeonEnvironmentSpawnData
    {
        public string PrefabName;
        public string MaterialPath;
        public Vector3 WorldPosition;
        public Vector3 Size = Vector3.one;
        public float RotationDegrees;
        public bool ApplyCollider = true;
        public bool HideVisual;
        public bool IsDecoration;
    }

    public enum RuntimeDungeonSceneObjectType
    {
        Exit = 0,
        Treasure = 1,
    }

    public sealed class RuntimeDungeonSceneObjectSpawnData
    {
        public RuntimeDungeonSceneObjectType ObjectType;
        public string PrefabName;
        public int RegionId;
        public int TileIndex;
        public Vector2Int SourceCoordinate;
        public Vector2Int DisplayCoordinate;
        public Vector3 WorldPosition;
        public Vector3 Size = Vector3.one;
        public bool RequiresRoomClear;
        public bool ApplyCollider = true;
        public int TargetFloor;
        public byte InterestSize;
        public uint RandomSeed;
        public List<int> TreasureCandidateItemIds = new();
    }
    public sealed class RuntimeDungeonMonsterSpawnData
    {
        public int RegionId;
        public int TileIndex;
        public int Level;
        public int SquadId;
        public bool IsBoss;
        public string PrefabName;
        public Vector2Int SourceCoordinate;
        public Vector2Int DisplayCoordinate;
        public Vector3 WorldPosition;
    }


}
