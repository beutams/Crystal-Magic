using System;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 地图生成的纯结果。单机和联机都只消费这份结果，再用各自的世界初始化入口落地实体。
    /// </summary>
    public sealed class DungeonMapPlan
    {
        public int themeKey;
        public int seed;
        public int dungeonFloor;
        public int attemptCount;
        public OpenFieldDungeonLayout layout;
        public RuntimeDungeonSceneData sceneData;
        public RuntimeDungeonFogData fogData;
    }

    /// <summary>
    /// 只负责根据主题和 Seed 产出地图计划，不创建 GameObject、Entity，也不读取存档。
    /// </summary>
    public static class DungeonMapPlanBuilder
    {
        public static bool TryBuild(
            int themeKey,
            int seed,
            int dungeonFloor,
            bool isBossFloor,
            out DungeonMapPlan plan,
            out string error)
        {
            plan = null;
            error = null;

            DungeonThemeData theme = DataComponent.Instance?.Get<DungeonThemeData>(themeKey);
            if (theme == null)
            {
                error = $"找不到主题 Key={themeKey} 的地牢配置。";
                return false;
            }

            try
            {
                theme.EnsureValid();
                if (!HasConfiguredExitSquad(theme.OpenField, isBossFloor))
                {
                    error = $"主题 '{theme.Name}' 没有可用于出口的{(isBossFloor ? "Boss" : "普通")}大队伍。";
                    return false;
                }

                DungeonConfig dungeonConfig = ConfigComponent.Instance?.Get<DungeonConfig>();
                OpenFieldDungeonTerrainConfig terrainConfig = theme.OpenField.Terrain.CloneValidated();
                terrainConfig.Width = Mathf.Max(8, dungeonConfig?.MapWidth ?? terrainConfig.Width);
                terrainConfig.Height = Mathf.Max(8, dungeonConfig?.MapHeight ?? terrainConfig.Height);
                terrainConfig.EnsureValid();

                OpenFieldDungeonLayout layout = OpenFieldDungeonTerrainGenerator.Generate(seed, terrainConfig);
                if (!OpenFieldDungeonAnchorGenerator.TryPlace(layout, seed, theme.OpenField.Anchors))
                {
                    error = $"主题 '{theme.Name}' 的锚点放置失败。";
                    return false;
                }

                if (!OpenFieldDungeonContentGenerator.TryPlace(layout, seed, theme.OpenField.Content))
                {
                    error = $"主题 '{theme.Name}' 的内容放置失败。";
                    return false;
                }

                RuntimeDungeonSceneData sceneData = OpenFieldDungeonSceneDataBuilder.Build(
                    layout,
                    theme,
                    dungeonConfig,
                    Mathf.Max(1, dungeonFloor),
                    isBossFloor);
                if (!HasValidExitGuard(sceneData, layout.ExitInterestPoint, isBossFloor))
                {
                    error = $"主题 '{theme.Name}' 的出口守卫无效。";
                    return false;
                }

                plan = new DungeonMapPlan
                {
                    themeKey = themeKey,
                    seed = seed,
                    dungeonFloor = Mathf.Max(1, dungeonFloor),
                    attemptCount = 1,
                    layout = layout,
                    sceneData = sceneData,
                    fogData = new RuntimeDungeonFogData(layout, sceneData),
                };
                return true;
            }
            catch (Exception exception)
            {
                error = $"构建主题 Key={themeKey} 的地图失败：{exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 联机没有“楼层”语义，主题 Key 与 Battle Seed 唯一确定本局地图。
        /// 场景数据仍保留旧的 Floor 字段，以兼容当前单机地图数据构建器。
        /// </summary>
        public static bool TryBuildBattle(int themeKey, int seed, out DungeonMapPlan plan, out string error)
        {
            return TryBuild(themeKey, seed, 1, false, out plan, out error);
        }

        private static bool HasConfiguredExitSquad(OpenFieldDungeonThemeData data, bool requiresBoss)
        {
            if (data?.EncounterPools == null)
                return false;

            foreach (OpenFieldDungeonEncounterPoolData pool in data.EncounterPools)
            {
                if (pool == null || pool.InterestSize != OpenFieldInterestSizeData.Large)
                    continue;

                foreach (OpenFieldDungeonSquadData squad in pool.Squads)
                {
                    if (squad == null || squad.IsBossSquad != requiresBoss || squad.Members == null || squad.Members.Count == 0)
                        continue;

                    foreach (OpenFieldDungeonSquadMemberData member in squad.Members)
                    {
                        if (member != null && !string.IsNullOrWhiteSpace(member.UnitName) && member.Cost > 0 && member.Weight > 0)
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool HasValidExitGuard(
            RuntimeDungeonSceneData sceneData,
            OpenFieldInterestPoint exitInterestPoint,
            bool requiresBoss)
        {
            if (sceneData == null || exitInterestPoint == null)
                return false;

            foreach (RuntimeDungeonMonsterSpawnData spawn in sceneData.MonsterSpawns)
            {
                if (spawn != null && spawn.RegionId == exitInterestPoint.EncounterId && spawn.IsBoss == requiresBoss)
                    return true;
            }

            foreach (RuntimeDungeonInterestPointSpawnData interestPointSpawn in sceneData.InterestPointSpawns)
            {
                if (interestPointSpawn == null || interestPointSpawn.EncounterId != exitInterestPoint.EncounterId)
                    continue;

                foreach (RuntimeDungeonMonsterSpawnData memberSpawn in interestPointSpawn.MemberSpawns)
                {
                    if (memberSpawn != null && memberSpawn.RegionId == exitInterestPoint.EncounterId && memberSpawn.IsBoss == requiresBoss)
                        return true;
                }
            }

            return false;
        }
    }
}
