using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Server;
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
        BattleNotReady = 10,
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

            return TryUsePropSlot(slotIndex, context, true, out failureReason);
        }

        public static bool TryUsePropSlot(int slotIndex, PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            return TryUsePropSlot(slotIndex, context, false, out failureReason);
        }

        private static bool TryUsePropSlot(
            int slotIndex,
            PropUseRequestContext context,
            bool queuePlayerOperation,
            out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;
            if (BattlePlayerStatusUtility.IsInputLocked(context.EntityManager, context.UserEntity))
            {
                failureReason = PropUseFailureReason.BattleNotReady;
                return false;
            }
            if (!IsBattleArea(context.EntityManager))
            {
                failureReason = PropUseFailureReason.NotInBattleArea;
                return false;
            }

            if (context.EntityManager.HasComponent<PlayerPropCooldownComponent>(context.UserEntity) &&
                context.EntityManager.GetComponentData<PlayerPropCooldownComponent>(context.UserEntity).SharedCooldownRemaining > 0f)
            {
                failureReason = PropUseFailureReason.SharedCooldownActive;
                return false;
            }

            if (!TryGetCharacterPropData(context, out CharacterPropData propData))
            {
                failureReason = PropUseFailureReason.PlayerNotFound;
                return false;
            }
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

            return TryUseResolvedPropSlot(
                propData,
                slotIndex,
                propSlot.ItemId,
                context,
                queuePlayerOperation,
                true,
                out failureReason);
        }

        public static bool TryUseShortcutSlot(int shortcutIndex, out PropUseFailureReason failureReason)
        {
            if (!TryBuildDefaultContext(out PropUseRequestContext context, out failureReason))
                return false;

            return TryUseShortcutSlot(shortcutIndex, context, true, out failureReason);
        }

        public static bool TryUseShortcutSlot(int shortcutIndex, PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            return TryUseShortcutSlot(shortcutIndex, context, false, out failureReason);
        }

        private static bool TryUseShortcutSlot(
            int shortcutIndex,
            PropUseRequestContext context,
            bool queuePlayerOperation,
            out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;
            if (!TryGetCharacterPropData(context, out CharacterPropData propData))
            {
                failureReason = PropUseFailureReason.PlayerNotFound;
                return false;
            }
            if (propData?.Slots == null || shortcutIndex < 0 || shortcutIndex >= propData.Slots.Count)
            {
                failureReason = PropUseFailureReason.InvalidPropSlot;
                return false;
            }

            return TryUsePropSlot(shortcutIndex, context, queuePlayerOperation, out failureReason);
        }

        public static bool TryUsePropItem(int itemId, out PropUseFailureReason failureReason)
        {
            if (!TryBuildDefaultContext(out PropUseRequestContext context, out failureReason))
                return false;

            return TryUsePropItem(itemId, context, true, out failureReason);
        }

        public static bool TryUsePropItem(int itemId, PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            return TryUsePropItem(itemId, context, false, out failureReason);
        }

        private static bool TryUsePropItem(
            int itemId,
            PropUseRequestContext context,
            bool queuePlayerOperation,
            out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;
            if (!TryGetCharacterPropData(context, out CharacterPropData propData))
            {
                failureReason = PropUseFailureReason.PlayerNotFound;
                return false;
            }
            int slotIndex = PropInventoryUtility.FindFirstPropSlot(propData, itemId);
            if (slotIndex < 0)
            {
                failureReason = PropUseFailureReason.ItemNotFound;
                return false;
            }

            return TryUseResolvedPropSlot(
                propData,
                slotIndex,
                itemId,
                context,
                queuePlayerOperation,
                true,
                out failureReason);
        }

        /// <summary>
        /// 在逻辑帧中执行输入队列里的道具事件。expectedItemId 防止同一槽位在等待期间
        /// 已换成别的道具时误用新道具；事件本身由输入桥按原顺序写入。
        /// </summary>
        public static bool TryApplyQueuedPropSlot(
            int slotIndex,
            int expectedItemId,
            PropUseRequestContext context,
            out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;
            if (!TryGetCharacterPropData(context, out CharacterPropData propData))
            {
                failureReason = PropUseFailureReason.PlayerNotFound;
                return false;
            }

            if (!PropInventoryUtility.TryGetSlot(propData, slotIndex, out CharacterPropSlotData propSlot))
            {
                failureReason = PropUseFailureReason.InvalidPropSlot;
                return false;
            }

            if (propSlot.ItemId != expectedItemId || propSlot.Quantity <= 0)
            {
                failureReason = PropUseFailureReason.ItemNotFound;
                return false;
            }

            return TryUseResolvedPropSlot(
                propData,
                slotIndex,
                expectedItemId,
                context,
                false,
                false,
                out failureReason);
        }

        public static bool TryBuildDefaultContext(out PropUseRequestContext context, out PropUseFailureReason failureReason)
        {
            context = default;
            failureReason = PropUseFailureReason.None;

            if (!GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
            {
                failureReason = PropUseFailureReason.PlayerNotFound;
                return false;
            }

            return TryBuildContext(entityManager, player, out context, out failureReason);
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
            bool queuePlayerOperation,
            bool emitInputEvent,
            out PropUseFailureReason failureReason)
        {
            failureReason = PropUseFailureReason.None;

            if (queuePlayerOperation)
            {
                GameWorldRole role = GameWorldContextUtility.Get(context.EntityManager).Role;
                if (role == GameWorldRole.Client &&
                    (!FrameManagerUtility.TryGet(context.EntityManager, out ClientFrameManager frame) || !frame.running))
                {
                    failureReason = PropUseFailureReason.BattleNotReady;
                    return false;
                }

                if (role != GameWorldRole.Client && role != GameWorldRole.Standalone)
                {
                    failureReason = PropUseFailureReason.BattleNotReady;
                    return false;
                }

                if (!InputComponent.Instance.QueuePropUse(slotIndex, itemId))
                {
                    failureReason = PropUseFailureReason.BattleNotReady;
                    return false;
                }
                return true;
            }

            if (emitInputEvent && context.EntityManager.HasComponent<PlayerInputComponent>(context.UserEntity))
            {
                PlayerInputComponent input = context.EntityManager.GetComponentData<PlayerInputComponent>(context.UserEntity);
                input.PropIndex = slotIndex;
                PlayerInputEventUtility.Append(context.EntityManager, context.UserEntity, PlayerInputOperationType.UseProp, input);
            }

            if (!IsBattleArea(context.EntityManager))
            {
                failureReason = PropUseFailureReason.NotInBattleArea;
                return false;
            }

            if (context.EntityManager.HasComponent<PlayerPropCooldownComponent>(context.UserEntity) &&
                context.EntityManager.GetComponentData<PlayerPropCooldownComponent>(context.UserEntity).SharedCooldownRemaining > 0f)
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

            PlayerPropCooldownComponent cooldown = new()
            {
                SharedCooldownRemaining = GetSharedCooldownSeconds(),
                NetworkDirty = 1,
            };
            if (context.EntityManager.HasComponent<PlayerPropCooldownComponent>(context.UserEntity))
                context.EntityManager.SetComponentData(context.UserEntity, cooldown);
            else
                context.EntityManager.AddComponentData(context.UserEntity, cooldown);

            EffectUtility.Enqueue(context.EntityManager, propData.EffectChain, skillContent);
            PlayerCharacterUtility.MarkChanged(context.EntityManager, context.UserEntity);
            return true;
        }

        private static bool TryGetCharacterPropData(PropUseRequestContext context, out CharacterPropData propData)
        {
            propData = null;
            return GameRuntimeStateUtility.TryGetPlayerCharacterData(
                       context.EntityManager,
                       context.UserEntity,
                       out CharacterData characterData) &&
                   (propData = characterData.Props) != null;
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

        private static bool IsBattleArea(EntityManager entityManager)
        {
            GameSceneMode sceneMode = GameWorldContextUtility.GetSceneMode(entityManager);
            return sceneMode == GameSceneMode.Training || sceneMode == GameSceneMode.Dungeon;
        }

        private static float GetSharedCooldownSeconds()
        {
            GameConfig config = ConfigComponent.Instance.Get<GameConfig>();
            return Mathf.Max(0f, config.BattlePropSharedCooldownSeconds);
        }
    }
}
