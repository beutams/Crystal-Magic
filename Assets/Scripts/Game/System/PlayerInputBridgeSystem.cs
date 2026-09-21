using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientInputSystemGroup))]
public partial class PlayerInputBridgeSystem : SystemBase
{
    private bool _wasSkillHeld;

    protected override void OnUpdate()
    {
        InputState inputState = InputComponent.Instance.CurrentState;
        bool isSkillPressed = inputState.IsSkillHeld && !_wasSkillHeld;
        foreach (RefRW<PlayerInputComponent> inputRef in
                 SystemAPI.Query<RefRW<PlayerInputComponent>>())
        {
            PlayerInputComponent oldInput = inputRef.ValueRO;
            int selectedChainIndex = oldInput.SkillChainIndex;
            if (isSkillPressed && inputState.SkillChainIndex >= 0)
                selectedChainIndex = inputState.SkillChainIndex;

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
                SkillChainIndex = selectedChainIndex,
                IsUsePropHeld = inputState.IsUsePropHeld ? (byte)1 : (byte)0,
                PropIndex = inputState.PropIndex,
            };
            bool inputChanged = !oldInput.Move.Equals(input.Move) ||
                                !oldInput.PointerWorldPosition.Equals(input.PointerWorldPosition) ||
                                oldInput.IsPrimaryHeld != input.IsPrimaryHeld ||
                                oldInput.IsInteractHeld != input.IsInteractHeld ||
                                oldInput.IsInventoryHeld != input.IsInventoryHeld ||
                                oldInput.IsPropertyHeld != input.IsPropertyHeld ||
                                oldInput.IsEscapeHeld != input.IsEscapeHeld ||
                                oldInput.IsSkillHeld != input.IsSkillHeld ||
                                oldInput.SkillChainIndex != input.SkillChainIndex ||
                                oldInput.IsUsePropHeld != input.IsUsePropHeld ||
                                oldInput.PropIndex != input.PropIndex;
            input.NetworkDirty = inputChanged ? (byte)1 : oldInput.NetworkDirty;
            inputRef.ValueRW = input;

            if (oldInput.SkillChainIndex != input.SkillChainIndex)
                EventComponent.Instance.Publish(new CommonGameEvent(PlayerInputComponent.SkillChainChangedEventName));
            break;
        }

        _wasSkillHeld = inputState.IsSkillHeld;
    }
}
