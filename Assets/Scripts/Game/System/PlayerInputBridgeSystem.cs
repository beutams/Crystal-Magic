using CrystalMagic.Core;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(ClientInputSystemGroup))]
public partial class PlayerInputBridgeSystem : SystemBase
{
    private InputComponent _inputComponent;
    private InputState _inputState;
    private bool _wasSkillHeld;
    private bool _wasNextSkillChainHeld;

    protected override void OnUpdate()
    {
        TryBindInputComponent();
        if (_inputComponent == null)
            return;

        bool isNetworkClient = FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager _);
        bool isSkillPressed = _inputState.IsSkillHeld && !_wasSkillHeld;
        bool isNextSkillChainPressed = _inputState.IsNextSkillChainHeld && !_wasNextSkillChainHeld;
        bool hasPlayer = false;
        foreach ((RefRW<PlayerInputComponent> inputRef,
                  RefRO<UnitFactionComponent> factionRef,
                  Entity entity) in
                 SystemAPI.Query<RefRW<PlayerInputComponent>, RefRO<UnitFactionComponent>>()
                     .WithEntityAccess())
        {
            if (!UnitFactionUtility.IsPlayer(factionRef.ValueRO.Value))
                continue;

            if (isNetworkClient &&
                !EntityManager.HasComponent<NetworkPlayerComponent>(entity))
            {
                continue;
            }

            hasPlayer = true;
            PlayerInputComponent input = new()
            {
                Move = new float2(_inputState.Move.x, _inputState.Move.y),
                PointerWorldPosition = new float3(
                    _inputState.PointerWorldPosition.x,
                    _inputState.PointerWorldPosition.y,
                    _inputState.PointerWorldPosition.z),
                IsPrimaryHeld = _inputState.IsPrimaryHeld ? (byte)1 : (byte)0,
                IsInteractHeld = _inputState.IsInteractHeld ? (byte)1 : (byte)0,
                IsInventoryHeld = _inputState.IsInventoryHeld ? (byte)1 : (byte)0,
                IsPropertyHeld = _inputState.IsPropertyHeld ? (byte)1 : (byte)0,
                IsEscapeHeld = _inputState.IsEscapeHeld ? (byte)1 : (byte)0,
                IsSkillHeld = _inputState.IsSkillHeld ? (byte)1 : (byte)0,
                SkillChainIndex = _inputState.SkillChainIndex,
                IsNextSkillChainHeld = _inputState.IsNextSkillChainHeld ? (byte)1 : (byte)0,
                IsUsePropHeld = _inputState.IsUsePropHeld ? (byte)1 : (byte)0,
                PropIndex = _inputState.PropIndex,
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

            if (!EntityManager.HasComponent<PlayerSkillSelectionComponent>(entity))
                continue;

            PlayerSkillSelectionComponent selection =
                EntityManager.GetComponentData<PlayerSkillSelectionComponent>(entity);
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
                EntityManager.SetComponentData(entity, selection);
                PlayerSkillRuntimeDataUtility.SetCurrentChain(EntityManager, entity, selection.CurrentChainIndex);
            }

            if (isSkillPressed || isNextSkillChainPressed)
                EventComponent.Instance.Publish(new CommonGameEvent(PlayerSkillSelectionComponent.ChangedEventName));
            break;
        }

        if (hasPlayer)
        {
            _wasSkillHeld = _inputState.IsSkillHeld;
            _wasNextSkillChainHeld = _inputState.IsNextSkillChainHeld;
        }
    }

    protected override void OnDestroy()
    {
        if (_inputComponent != null)
            _inputComponent.OnInputStateChanged -= HandleInputStateChanged;
    }

    private void TryBindInputComponent()
    {
        if (_inputComponent != null || !InputComponent.TryGetInstance(out InputComponent inputComponent))
            return;

        _inputComponent = inputComponent;
        _inputState = _inputComponent.CurrentState;
        _inputComponent.OnInputStateChanged += HandleInputStateChanged;
    }

    private void HandleInputStateChanged(InputState inputState)
    {
        _inputState = inputState;
    }
}
