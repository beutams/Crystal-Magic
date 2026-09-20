using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(ClientInputSystemGroup))]
public partial class PlayerInputBridgeSystem : SystemBase
{
    private bool _wasSkillHeld;
    private bool _wasNextSkillChainHeld;

    protected override void OnUpdate()
    {
        InputState inputState = InputComponent.Instance.CurrentState;
        bool isSkillPressed = inputState.IsSkillHeld && !_wasSkillHeld;
        bool isNextSkillChainPressed = inputState.IsNextSkillChainHeld && !_wasNextSkillChainHeld;
        foreach ((RefRW<PlayerInputComponent> inputRef,
                  RefRW<PlayerSkillSelectionComponent> selectionRef,
                  Entity entity) in
                 SystemAPI.Query<RefRW<PlayerInputComponent>, RefRW<PlayerSkillSelectionComponent>>()
                     .WithEntityAccess())
        {
            PlayerInputComponent input = new()
            {
                Move = new float2(inputState.Move.x, inputState.Move.y),
                PointerWorldPosition = new float3(
                    inputState.PointerWorldPosition.x,
                    inputState.PointerWorldPosition.y,
                    inputState.PointerWorldPosition.z),
                IsPrimaryHeld = inputState.IsPrimaryHeld ? (byte)1 : (byte)0,
                IsInteractHeld = inputState.IsInteractHeld ? (byte)1 : (byte)0,
                IsInventoryHeld = inputState.IsInventoryHeld ? (byte)1 : (byte)0,
                IsPropertyHeld = inputState.IsPropertyHeld ? (byte)1 : (byte)0,
                IsEscapeHeld = inputState.IsEscapeHeld ? (byte)1 : (byte)0,
                IsSkillHeld = inputState.IsSkillHeld ? (byte)1 : (byte)0,
                SkillChainIndex = inputState.SkillChainIndex,
                IsNextSkillChainHeld = inputState.IsNextSkillChainHeld ? (byte)1 : (byte)0,
                IsUsePropHeld = inputState.IsUsePropHeld ? (byte)1 : (byte)0,
                PropIndex = inputState.PropIndex,
            };
            PlayerInputComponent oldInput = inputRef.ValueRO;
            bool inputChanged = !oldInput.Move.Equals(input.Move) ||
                                !oldInput.PointerWorldPosition.Equals(input.PointerWorldPosition) ||
                                oldInput.IsPrimaryHeld != input.IsPrimaryHeld ||
                                oldInput.IsInteractHeld != input.IsInteractHeld ||
                                oldInput.IsInventoryHeld != input.IsInventoryHeld ||
                                oldInput.IsPropertyHeld != input.IsPropertyHeld ||
                                oldInput.IsEscapeHeld != input.IsEscapeHeld ||
                                oldInput.IsSkillHeld != input.IsSkillHeld ||
                                oldInput.SkillChainIndex != input.SkillChainIndex ||
                                oldInput.IsNextSkillChainHeld != input.IsNextSkillChainHeld ||
                                oldInput.IsUsePropHeld != input.IsUsePropHeld ||
                                oldInput.PropIndex != input.PropIndex;
            input.NetworkDirty = inputChanged ? (byte)1 : oldInput.NetworkDirty;
            inputRef.ValueRW = input;

            PlayerSkillSelectionComponent selection = selectionRef.ValueRO;
            int previousChainIndex = selection.CurrentChainIndex;
            int chainCount = GameRuntimeStateUtility.TryGetPlayerCharacterData(EntityManager, entity, out CharacterData characterData)
                ? characterData.Skills?.Chains?.Length ?? 0
                : 0;
            if (isSkillPressed && input.SkillChainIndex >= 0)
                selection.CurrentChainIndex = Mathf.Clamp(input.SkillChainIndex, 0, chainCount > 0 ? chainCount - 1 : 0);
            if (isNextSkillChainPressed && chainCount > 0)
                selection.CurrentChainIndex = (selection.CurrentChainIndex + 1) % chainCount;

            if (selection.CurrentChainIndex != previousChainIndex)
            {
                selection.NetworkDirty = 1;
                selectionRef.ValueRW = selection;
                PlayerSkillRuntimeDataUtility.SetCurrentChain(EntityManager, entity, selection.CurrentChainIndex);
            }

            if (isSkillPressed || isNextSkillChainPressed)
                EventComponent.Instance.Publish(new CommonGameEvent(PlayerSkillSelectionComponent.ChangedEventName));
            break;
        }

        _wasSkillHeld = inputState.IsSkillHeld;
        _wasNextSkillChainHeld = inputState.IsNextSkillChainHeld;
    }
}
