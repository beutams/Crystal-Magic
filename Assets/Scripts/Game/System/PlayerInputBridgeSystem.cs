using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(ClientInputSystemGroup))]
public partial class PlayerInputBridgeSystem : SystemBase
{
    private bool _wasSkillHeld;
    private bool _wasNextSkillChainHeld;
    private Entity _playerEntity;
    private GameSceneMode _playerSceneMode;

    protected override void OnUpdate()
    {
        InputState inputState = InputComponent.Instance.CurrentState;
        bool isSkillPressed = inputState.IsSkillHeld && !_wasSkillHeld;
        bool isNextSkillChainPressed = inputState.IsNextSkillChainHeld && !_wasNextSkillChainHeld;
        GameSceneMode sceneMode = GameWorldContextUtility.GetSceneMode(EntityManager);
        if (_playerEntity == Entity.Null ||
            !EntityManager.Exists(_playerEntity) ||
            !EntityManager.HasComponent<PlayerInputComponent>(_playerEntity) ||
            _playerSceneMode != sceneMode)
        {
            if (!GameRuntimeStateUtility.TryGetPlayerEntity(EntityManager, out _playerEntity))
                _playerEntity = Entity.Null;
            _playerSceneMode = sceneMode;
        }

        if (_playerEntity == Entity.Null)
        {
            _wasSkillHeld = inputState.IsSkillHeld;
            _wasNextSkillChainHeld = inputState.IsNextSkillChainHeld;
            return;
        }

        foreach ((RefRW<PlayerInputComponent> inputRef, Entity entity) in
                 SystemAPI.Query<RefRW<PlayerInputComponent>>()
                     .WithEntityAccess())
        {
            if (entity != _playerEntity)
                continue;

            PlayerInputComponent oldInput = inputRef.ValueRO;
            int chainCount = GameRuntimeStateUtility.TryGetPlayerCharacterData(EntityManager, entity, out CharacterData characterData)
                ? characterData.Skills?.Chains?.Length ?? 0
                : 0;
            int selectedChainIndex = chainCount > 0
                ? math.clamp(oldInput.SkillChainIndex, 0, chainCount - 1)
                : 0;
            if (isSkillPressed && inputState.SkillChainIndex >= 0)
                selectedChainIndex = math.clamp(inputState.SkillChainIndex, 0, chainCount > 0 ? chainCount - 1 : 0);
            if (isNextSkillChainPressed && chainCount > 0)
                selectedChainIndex = (selectedChainIndex + 1) % chainCount;

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
                IsNextSkillChainHeld = inputState.IsNextSkillChainHeld ? (byte)1 : (byte)0,
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
                                oldInput.IsNextSkillChainHeld != input.IsNextSkillChainHeld ||
                                oldInput.IsUsePropHeld != input.IsUsePropHeld ||
                                oldInput.PropIndex != input.PropIndex;
            input.NetworkDirty = inputChanged ? (byte)1 : oldInput.NetworkDirty;
            inputRef.ValueRW = input;

            if (oldInput.SkillChainIndex != input.SkillChainIndex)
                EventComponent.Instance.Publish(new CommonGameEvent(PlayerInputComponent.SkillChainChangedEventName));
            break;
        }

        _wasSkillHeld = inputState.IsSkillHeld;
        _wasNextSkillChainHeld = inputState.IsNextSkillChainHeld;
    }
}
