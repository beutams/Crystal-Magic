using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitSourceInitializationSystem))]
public partial class WorldStateSystem : SystemBase
{
    private Entity _worldEntity;
    private InputComponent _inputComponent;
    private InputState _inputState;

    protected override void OnCreate()
    {
        if (!WorldStateUtility.TryGetEntity(EntityManager, out _worldEntity))
            _worldEntity = EntityManager.CreateEntity(typeof(WorldStateComponent), typeof(WorldInputComponent));

        if (!EntityManager.HasComponent<WorldVariableComponent>(_worldEntity))
            EntityManager.AddComponentObject(_worldEntity, new WorldVariableComponent());

        if (!EntityManager.HasComponent<WorldSkillDataComponent>(_worldEntity))
            EntityManager.AddComponentObject(_worldEntity, new WorldSkillDataComponent());
    }

    protected override void OnUpdate()
    {
        TryBindInputComponent();
        if (!EntityManager.Exists(_worldEntity) || !EntityManager.HasComponent<WorldInputComponent>(_worldEntity))
            return;

        EntityManager.SetComponentData(_worldEntity, new WorldInputComponent
        {
            Move = new float2(_inputState.Move.x, _inputState.Move.y),
            PointerWorldPosition = new float3(
                _inputState.PointerWorldPosition.x,
                _inputState.PointerWorldPosition.y,
                _inputState.PointerWorldPosition.z),
            IsPrimaryHeld = _inputState.IsPrimaryHeld,
            IsInteractHeld = _inputState.IsInteractHeld,
            IsInventoryHeld = _inputState.IsInventoryHeld,
            IsPropertyHeld = _inputState.IsPropertyHeld,
            IsEscapeHeld = _inputState.IsEscapeHeld,
            IsSkillHeld = _inputState.IsSkillHeld,
            SkillChainIndex = _inputState.SkillChainIndex,
            IsNextSkillChainHeld = _inputState.IsNextSkillChainHeld,
            IsUsePropHeld = _inputState.IsUsePropHeld,
            PropIndex = _inputState.PropIndex,
        });

        SynchronizeSkillData();
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

    private void SynchronizeSkillData()
    {
        if (!EntityManager.HasComponent<WorldSkillDataComponent>(_worldEntity) ||
            !SaveDataComponent.TryGetInstance(out SaveDataComponent saveDataComponent) ||
            !DataComponent.TryGetInstance(out DataComponent dataComponent))
        {
            return;
        }

        WorldSkillDataComponent worldSkillData = EntityManager.GetComponentObject<WorldSkillDataComponent>(_worldEntity);
        if (worldSkillData == null)
            return;

        int currentChainId = RuntimeDataComponent.Instance.GetSkillData().CurrentSkillChainIndex;
        worldSkillData.Synchronize(saveDataComponent.GetSkillData(), dataComponent, currentChainId);
    }
}
