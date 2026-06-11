using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(NPCInteractPromptSystem))]
partial struct NPCInteractInputSystem : ISystem
{
    private NativeReference<bool> _interactRequested;
    private bool _subscribed;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NPCInteractionState>();
        _interactRequested = new NativeReference<bool>(false, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_subscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
        {
            inputComponent.OnInteract -= HandleInteract;
        }

        if (_interactRequested.IsCreated)
        {
            _interactRequested.Dispose();
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_subscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
        {
            inputComponent.OnInteract += HandleInteract;
            _subscribed = true;
        }

        bool playerInputLocked =
            GameGateComponent.TryGetInstance(out GameGateComponent gameGateComponent) &&
            gameGateComponent.IsPlayerInputLocked;

        if (playerInputLocked)
        {
            _interactRequested.Value = false;
            RefRW<NPCInteractionRequest> lockedRequest = SystemAPI.GetSingletonRW<NPCInteractionRequest>();
            lockedRequest.ValueRW.Target = Entity.Null;
            lockedRequest.ValueRW.HasRequest = 0;
            return;
        }

        if (!_interactRequested.Value)
            return;

        _interactRequested.Value = false;

        Entity target = SystemAPI.GetSingleton<NPCInteractionState>().CurrentTarget;
        if (target == Entity.Null)
            return;

        RefRW<NPCInteractionRequest> request = SystemAPI.GetSingletonRW<NPCInteractionRequest>();
        request.ValueRW.Target = target;
        request.ValueRW.HasRequest = 1;
    }

    private void HandleInteract()
    {
        _interactRequested.Value = true;
    }
}
