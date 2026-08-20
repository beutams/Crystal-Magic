using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game
{
    public enum PropUseFailureReason
    {
        None = 0,
        NotInBattleArea = 1,
        SharedCooldownActive = 2,
        PlayerNotFound = 3,
        InvalidPropSlot = 4,
        ItemNotFound = 5,
        ItemNotUsable = 6,
        MissingUseData = 7,
        TargetMissing = 8,
        ConsumeFailed = 9,
    }

    public struct PropUseRequestContext
    {
        public EntityManager EntityManager;
        public Entity UserEntity;
        public bool HasTargetEntity;
        public Entity TargetEntity;
        public bool HasTargetPosition;
        public Vector3 TargetPosition;
    }

    public static class PropUseUtility
    {
        public static bool TryUsePropSlot(int slotIndex, out PropUseFailureReason failureReason)
        {
            if (!TryBuildDefaultContext(out PropUseRequestContext context, out failureReason))
                return false;

            return TryUsePropSlot(slotIndex, context, out failureReason);
        }

        public static bool TryUsePropSlot(int slotIndex, PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;
            if (!IsBattleArea())
            {
                failureReason = PropUseFailureReason.NotInBattleArea;
                return false;
            }

            RuntimePropData runtimeItemData = RuntimeDataComponent.Instance.GetPropData();
            if (runtimeItemData != null && runtimeItemData.SharedCooldownRemaining > 0f)
            {
                failureReason = PropUseFailureReason.SharedCooldownActive;
                return false;
            }

            CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();
            if (!PropInventoryUtility.TryGetSlot(propData, slotIndex, out CharacterPropSlotData propSlot))
            {
                failureReason = PropUseFailureReason.InvalidPropSlot;
                return false;
            }

            if (propSlot.ItemId < 0 || propSlot.Quantity <= 0)
            {
                failureReason = PropUseFailureReason.ItemNotFound;
                return false;
            }

            return TryUseResolvedPropSlot(propData, slotIndex, propSlot.ItemId, context, out failureReason);
        }

        public static bool TryUseShortcutSlot(int shortcutIndex, out PropUseFailureReason failureReason)
        {
            if (!TryBuildDefaultContext(out PropUseRequestContext context, out failureReason))
                return false;

            return TryUseShortcutSlot(shortcutIndex, context, out failureReason);
        }

        public static bool TryUseShortcutSlot(int shortcutIndex, PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;
            CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();
            if (!PropInventoryUtility.TryGetShortcutPropSlot(propData, shortcutIndex, out int propSlotIndex))
            {
                failureReason = PropUseFailureReason.InvalidPropSlot;
                return false;
            }

            return TryUsePropSlot(propSlotIndex, context, out failureReason);
        }

        public static bool TryUsePropItem(int itemId, out PropUseFailureReason failureReason)
        {
            if (!TryBuildDefaultContext(out PropUseRequestContext context, out failureReason))
                return false;

            return TryUsePropItem(itemId, context, out failureReason);
        }

        public static bool TryUsePropItem(int itemId, PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;
            CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();
            int slotIndex = PropInventoryUtility.FindFirstPropSlot(propData, itemId);
            if (slotIndex < 0)
            {
                failureReason = PropUseFailureReason.ItemNotFound;
                return false;
            }

            return TryUseResolvedPropSlot(propData, slotIndex, itemId, context, out failureReason);
        }

        public static bool TryBindShortcutSlot(int shortcutIndex, int propSlotIndex)
        {
            CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();
            if (!PropInventoryUtility.TryBindShortcut(propData, shortcutIndex, propSlotIndex))
                return false;

            SaveDataComponent.Instance.NotifyCharacterPropDataChanged();
            return true;
        }

        public static bool TryBuildDefaultContext(out PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            context = default;
            failureReason = PropUseFailureReason.None;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                failureReason = PropUseFailureReason.PlayerNotFound;
                return false;
            }

            EntityManager entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitFactionComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
                    continue;

                return TryBuildContext(entityManager, entity, out context, out failureReason);
            }

            failureReason = PropUseFailureReason.PlayerNotFound;
            return false;
        }

        public static bool TryBuildContext(
            EntityManager entityManager,
            Entity userEntity,
            out PropUseRequestContext context,
            out PropUseFailureReason failureReason)
        {
            context = default;
            failureReason = PropUseFailureReason.None;
            if (userEntity == Entity.Null || !entityManager.Exists(userEntity))
            {
                failureReason = PropUseFailureReason.PlayerNotFound;
                return false;
            }

            context = new PropUseRequestContext
            {
                EntityManager = entityManager,
                UserEntity = userEntity,
            };

            return true;
        }

        private static bool TryUseResolvedPropSlot(
            CharacterPropData characterPropData,
            int slotIndex,
            int itemId,
            PropUseRequestContext context,
            out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;

            if (!IsBattleArea())
            {
                failureReason = PropUseFailureReason.NotInBattleArea;
                return false;
            }

            RuntimePropData runtimeItemData = RuntimeDataComponent.Instance.GetPropData();
            if (runtimeItemData != null && runtimeItemData.SharedCooldownRemaining > 0f)
            {
                failureReason = PropUseFailureReason.SharedCooldownActive;
                return false;
            }

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData == null)
            {
                failureReason = PropUseFailureReason.ItemNotFound;
                return false;
            }

            if (itemData.ItemType != ItemType.Prop || itemData.ExtraId < 0)
            {
                failureReason = PropUseFailureReason.ItemNotUsable;
                return false;
            }

            PropData propData = DataComponent.Instance.Get<PropData>(itemData.ExtraId);
            if (propData == null)
            {
                failureReason = PropUseFailureReason.MissingUseData;
                return false;
            }

            if (!TryBuildSkillContent(propData, context, out SkillContent skillContent))
            {
                failureReason = PropUseFailureReason.TargetMissing;
                return false;
            }

            if (!PropInventoryUtility.TryConsumePropSlot(characterPropData, slotIndex, itemId, 1))
            {
                failureReason = PropUseFailureReason.ConsumeFailed;
                return false;
            }

            SkillExecutor.ExecuteEffects(propData.EffectChain, skillContent);
            SaveDataComponent.Instance.NotifyCharacterPropDataChanged();
            EventComponent.Instance.Publish(new CommonGameEvent(
                GameplayEventNames.PropUsed,
                new GameplayEventReference(context.UserEntity, UnitValue.FromFloat(GetSharedCooldownSeconds()))));
            return true;
        }

        private static bool TryBuildSkillContent(PropData propData, PropUseRequestContext context, out SkillContent skillContent)
        {
            skillContent = null;
            if (context.UserEntity == Entity.Null || !context.EntityManager.Exists(context.UserEntity))
                return false;

            float3 userPosition = float3.zero;
            if (context.EntityManager.HasComponent<LocalTransform>(context.UserEntity))
                userPosition = context.EntityManager.GetComponentData<LocalTransform>(context.UserEntity).Position;

            SkillContent content = new SkillContent
            {
                TriggerSource = SkillTriggerSource.Script,
                HookType = SkillHookType.None,
                EntityManager = context.EntityManager,
                HasOriginEntity = true,
                OriginEntity = context.UserEntity,
                HasPosition = true,
                Position = new Vector3(userPosition.x, userPosition.y, userPosition.z),
            };

            switch (propData.TargetType)
            {
                case PropTargetType.Self:
                    content.HasTargetEntity = true;
                    content.TargetEntity = context.UserEntity;
                    break;

                case PropTargetType.CurrentTarget:
                    if (!context.HasTargetEntity ||
                        context.TargetEntity == Entity.Null ||
                        !context.EntityManager.Exists(context.TargetEntity))
                    {
                        return false;
                    }

                    content.HasTargetEntity = true;
                    content.TargetEntity = context.TargetEntity;
                    if (context.EntityManager.HasComponent<LocalTransform>(context.TargetEntity))
                    {
                        float3 targetPosition = context.EntityManager.GetComponentData<LocalTransform>(context.TargetEntity).Position;
                        content.HasPosition = true;
                        content.Position = new Vector3(targetPosition.x, targetPosition.y, targetPosition.z);
                    }
                    break;

                case PropTargetType.TargetPosition:
                    if (context.HasTargetEntity &&
                        context.TargetEntity != Entity.Null &&
                        context.EntityManager.Exists(context.TargetEntity))
                    {
                        content.HasTargetEntity = true;
                        content.TargetEntity = context.TargetEntity;
                    }

                    if (context.HasTargetPosition)
                    {
                        content.HasPosition = true;
                        content.Position = context.TargetPosition;
                    }
                    else if (context.HasTargetEntity &&
                             context.TargetEntity != Entity.Null &&
                             context.EntityManager.Exists(context.TargetEntity) &&
                             context.EntityManager.HasComponent<LocalTransform>(context.TargetEntity))
                    {
                        float3 targetPosition = context.EntityManager.GetComponentData<LocalTransform>(context.TargetEntity).Position;
                        content.HasPosition = true;
                        content.Position = new Vector3(targetPosition.x, targetPosition.y, targetPosition.z);
                    }
                    else
                    {
                        return false;
                    }
                    break;
            }

            skillContent = content;
            return true;
        }

        private static bool IsBattleArea()
        {
            SaveAreaType areaType = SaveDataComponent.Instance.GetLocationData()?.AreaType ?? SaveAreaType.Town;
            return areaType == SaveAreaType.Training || areaType == SaveAreaType.Dungeon;
        }

        private static float GetSharedCooldownSeconds()
        {
            GameConfig config = ConfigComponent.Instance.Get<GameConfig>();
            return Mathf.Max(0f, config.BattlePropSharedCooldownSeconds);
        }
    }
}
