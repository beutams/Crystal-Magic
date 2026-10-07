using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GamePresentationSystemGroup))]
[UpdateBefore(typeof(UnitAnimationSystem))]
public partial struct SelfUnitAnimationSystem : ISystem
{
    private const int EquipmentShopUnitDataId = 4;
    private const int ItemShopUnitDataId = 5;
    private const int SkillShopUnitDataId = 6;
    private const int StashUnitDataId = 7;
    private const int TrainingPortalUnitDataId = 8;
    private const int DungeonExitUnitDataId = 32;
    private const int TreasureUnitDataId = 33;

    private static readonly FixedString64Bytes IdleAnimation = "Idle";
    private static readonly FixedString64Bytes LockedAnimation = "Locked";
    private static readonly FixedString64Bytes UnlockAnimation = "Unlock";
    private static readonly FixedString64Bytes CopperClosedAnimation = "CopperClosed";
    private static readonly FixedString64Bytes CopperOpenAnimation = "CopperOpen";
    private static readonly FixedString64Bytes CopperCloseAnimation = "CopperClose";
    private static readonly FixedString64Bytes SilverClosedAnimation = "SilverClosed";
    private static readonly FixedString64Bytes SilverOpenAnimation = "SilverOpen";
    private static readonly FixedString64Bytes GoldClosedAnimation = "GoldClosed";
    private static readonly FixedString64Bytes GoldOpenAnimation = "GoldOpen";

    private EntityQuery _interactionQuery;

    public void OnCreate(ref SystemState state)
    {
        _interactionQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<GameInteractionComponent>(),
            ComponentType.ReadOnly<InteractionTransactionElement>());
    }

    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<TreasureComponent> treasureLookup =
            SystemAPI.GetComponentLookup<TreasureComponent>(true);
        ComponentLookup<UnitInteractableComponent> interactableLookup =
            SystemAPI.GetComponentLookup<UnitInteractableComponent>(true);
        bool hasInteractionRuntime = !_interactionQuery.IsEmptyIgnoreFilter;
        DynamicBuffer<InteractionTransactionElement> interactions = default;
        if (hasInteractionRuntime)
            interactions = _interactionQuery.GetSingletonBuffer<InteractionTransactionElement>();

        foreach ((RefRW<UnitAnimationComponent> animationRef, RefRO<UnitStateScriptComponent> stateScriptRef, Entity entity) in
                 SystemAPI.Query<RefRW<UnitAnimationComponent>, RefRO<UnitStateScriptComponent>>().WithEntityAccess())
        {
            ref UnitAnimationComponent animation = ref animationRef.ValueRW;
            switch (stateScriptRef.ValueRO.UnitDataId)
            {
                case EquipmentShopUnitDataId:
                case ItemShopUnitDataId:
                case SkillShopUnitDataId:
                case TrainingPortalUnitDataId:
                    if (animation.AnimationName.Length == 0)
                        SetAnimation(ref animation, IdleAnimation);
                    break;
                case StashUnitDataId:
                    UpdateStash(
                        ref animation,
                        hasInteractionRuntime && IsInteractionProcessing(entity, in interactions));
                    break;
                case DungeonExitUnitDataId:
                    UpdateDungeonExit(entity, ref animation, in interactableLookup);
                    break;
                case TreasureUnitDataId:
                    if (treasureLookup.TryGetComponent(entity, out TreasureComponent treasure))
                        SetAnimation(ref animation, ResolveTreasureAnimation(in treasure));
                    break;
            }
        }
    }

    private static bool IsInteractionProcessing(
        Entity target,
        in DynamicBuffer<InteractionTransactionElement> interactions)
    {
        for (int index = 0; index < interactions.Length; index++)
        {
            InteractionTransactionElement interaction = interactions[index];
            if (interaction.Target == target && interaction.Phase == InteractionPhase.Processing)
                return true;
        }

        return false;
    }

    private static void UpdateStash(
        ref UnitAnimationComponent animation,
        bool isInteracting)
    {
        if (isInteracting)
        {
            SetAnimation(ref animation, CopperOpenAnimation);
            return;
        }

        if (animation.AnimationName.Length == 0)
        {
            SetAnimation(ref animation, CopperClosedAnimation);
            return;
        }

        if (animation.AnimationName.Equals(CopperOpenAnimation))
        {
            SetAnimation(ref animation, CopperCloseAnimation);
            return;
        }

        if (animation.AnimationName.Equals(CopperCloseAnimation) &&
            animation.CurrentClipLength > 0f &&
            animation.ElapsedSeconds >= animation.CurrentClipLength)
        {
            SetAnimation(ref animation, CopperClosedAnimation);
        }
    }

    private static void UpdateDungeonExit(
        Entity entity,
        ref UnitAnimationComponent animation,
        in ComponentLookup<UnitInteractableComponent> interactableLookup)
    {
        if (!interactableLookup.TryGetComponent(entity, out UnitInteractableComponent interactable))
            return;

        if (interactable.IsEnabled == 0)
        {
            SetAnimation(ref animation, LockedAnimation);
            return;
        }

        if (animation.AnimationName.Length == 0)
        {
            SetAnimation(ref animation, IdleAnimation);
            return;
        }

        if (animation.AnimationName.Equals(LockedAnimation))
        {
            SetAnimation(ref animation, UnlockAnimation);
            return;
        }

        if (animation.AnimationName.Equals(UnlockAnimation) &&
            animation.CurrentClipLength > 0f &&
            animation.ElapsedSeconds >= animation.CurrentClipLength)
        {
            SetAnimation(ref animation, IdleAnimation);
        }
    }

    private static FixedString64Bytes ResolveTreasureAnimation(in TreasureComponent treasure)
    {
        return treasure.Quality switch
        {
            DungeonTreasureQuality.Silver => treasure.IsOpened != 0
                ? SilverOpenAnimation
                : SilverClosedAnimation,
            DungeonTreasureQuality.Gold => treasure.IsOpened != 0
                ? GoldOpenAnimation
                : GoldClosedAnimation,
            _ => treasure.IsOpened != 0
                ? CopperOpenAnimation
                : CopperClosedAnimation,
        };
    }

    private static void SetAnimation(ref UnitAnimationComponent animation, in FixedString64Bytes animationName)
    {
        if (animation.AnimationName.Equals(animationName))
            return;

        uint sequence = animation.Sequence + 1u;
        animation.AnimationName = animationName;
        animation.Sequence = sequence == 0u ? 1u : sequence;
        animation.RequestedStartElapsedSeconds = 0f;
    }
}
