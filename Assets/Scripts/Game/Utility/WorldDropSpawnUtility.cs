using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Unit
{
    public static class WorldDropSpawnUtility
    {
        private static readonly FixedString128Bytes DropPrefabName = "Drop";
        private static bool s_loggedMissingDropPrefab;

        public static bool CanSpawnDrop(EntityManager entityManager)
        {
            if (EntitySpawnRegistryUtility.TryGetDropPrefab(entityManager, DropPrefabName, out _))
                return true;

            LogMissingDropPrefabOnce();
            return false;
        }

        public static bool TrySpawnDrop(EntityManager entityManager, DropRewardType dropType, int itemId, int amount, float3 position)
        {
            return TrySpawnDropInternal(
                entityManager,
                dropType,
                itemId,
                amount,
                position,
                position,
                0f,
                0f,
                false);
        }

        public static bool TrySpawnScatteredDrop(
            EntityManager entityManager,
            DropRewardType dropType,
            int itemId,
            int amount,
            float3 startPosition,
            float3 targetPosition,
            float durationSeconds,
            float arcHeight)
        {
            return TrySpawnDropInternal(
                entityManager,
                dropType,
                itemId,
                amount,
                startPosition,
                targetPosition,
                math.max(0.0001f, durationSeconds),
                math.max(0f, arcHeight),
                true);
        }

        private static bool TrySpawnDropInternal(
            EntityManager entityManager,
            DropRewardType dropType,
            int itemId,
            int amount,
            float3 startPosition,
            float3 targetPosition,
            float durationSeconds,
            float arcHeight,
            bool hasScatter)
        {
            if (amount <= 0)
                return false;

            UnitInteractionData interactionData = UnitInteractionData.CreateDrop(dropType, itemId, amount);
            NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                NetworkEntityPrefabType.Drop,
                DropPrefabName.ToString(),
                new Vector3(startPosition.x, startPosition.y, startPosition.z));
            entityInfo.hasInteractableData = true;
            entityInfo.interactionKind = interactionData.Kind;
            entityInfo.interactionDataId = interactionData.DataId;
            entityInfo.interactionAmount = interactionData.Amount;
            entityInfo.interactionVariant = interactionData.Variant;
            float interactionRange = math.max(0f, ConfigComponent.Instance.Get<GameConfig>().InteractionRange);
            entityInfo.interactionRangeSq = interactionRange * interactionRange;
            entityInfo.interactionEnabled = !hasScatter;
            entityInfo.hasDropScatter = hasScatter;
            entityInfo.dropScatterStartX = startPosition.x;
            entityInfo.dropScatterStartY = startPosition.y;
            entityInfo.dropScatterStartZ = startPosition.z;
            entityInfo.dropScatterTargetX = targetPosition.x;
            entityInfo.dropScatterTargetY = targetPosition.y;
            entityInfo.dropScatterTargetZ = targetPosition.z;
            entityInfo.dropScatterDurationSeconds = durationSeconds;
            entityInfo.dropScatterElapsedSeconds = 0f;
            entityInfo.dropScatterArcHeight = arcHeight;
            entityInfo.dropScatterLanded = !hasScatter;
            if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out _))
            {
                LogMissingDropPrefabOnce();
                return false;
            }

            return true;
        }

        private static void LogMissingDropPrefabOnce()
        {
            if (s_loggedMissingDropPrefab)
                return;

            s_loggedMissingDropPrefab = true;
            Debug.LogWarning("[WorldDropSpawnUtility] Could not instantiate drop prefab 'Drop' from EntitySpawnRegistry. Make sure EntitySpawnRegistryAuthoring is baked and the prefab exists under the drop prefab directory.");
        }
    }
}
