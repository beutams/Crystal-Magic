using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CrystalMagic.Core;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial struct PlayerInputSystem : ISystem
{
    private NativeReference<float2> _moveInput;
    private NativeReference<float2> _castTarget;
    private NativeReference<bool> _wantToCast;
    private NativeReference<bool> _wantToInteract;
    private NativeReference<bool> _wantToUseProp;
    private NativeReference<int> _requestedPropShortcutIndex;
    private bool _subscribed;

    public void OnCreate(ref SystemState state)
    {
        _moveInput = new NativeReference<float2>(float2.zero, Allocator.Persistent);
        _castTarget = new NativeReference<float2>(float2.zero, Allocator.Persistent);
        _wantToCast = new NativeReference<bool>(false, Allocator.Persistent);
        _wantToInteract = new NativeReference<bool>(false, Allocator.Persistent);
        _wantToUseProp = new NativeReference<bool>(false, Allocator.Persistent);
        _requestedPropShortcutIndex = new NativeReference<int>(-1, Allocator.Persistent);
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_subscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
        {
            inputComponent.OnMove -= HandleMove;
            inputComponent.OnMouseWorldPosition -= HandleMouseWorldPosition;
            inputComponent.OnMousePress -= HandleMousePress;
            inputComponent.OnInteract -= HandleInteract;
            inputComponent.OnUseProp -= HandleUseProp;
        }
        if (_moveInput.IsCreated)
            _moveInput.Dispose();
        if (_castTarget.IsCreated)
            _castTarget.Dispose();
        if (_wantToCast.IsCreated)
            _wantToCast.Dispose();
        if (_wantToInteract.IsCreated)
            _wantToInteract.Dispose();
        if (_wantToUseProp.IsCreated)
            _wantToUseProp.Dispose();
        if (_requestedPropShortcutIndex.IsCreated)
            _requestedPropShortcutIndex.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_subscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
        {
            inputComponent.OnMove += HandleMove;
            inputComponent.OnMouseWorldPosition += HandleMouseWorldPosition;
            inputComponent.OnMousePress += HandleMousePress;
            inputComponent.OnInteract += HandleInteract;
            inputComponent.OnUseProp += HandleUseProp;
            _subscribed = true;
        }

        bool playerInputLocked = GameGateComponent.TryGetInstance(out GameGateComponent gameGateComponent) && gameGateComponent.IsPlayerInputLocked;

        if (playerInputLocked)
        {
            _moveInput.Value = float2.zero;
            _castTarget.Value = float2.zero;
            _wantToCast.Value = false;
            _wantToInteract.Value = false;
            _wantToUseProp.Value = false;
            _requestedPropShortcutIndex.Value = -1;

            foreach (var (_, intent) in
                SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitIntentComponent>>())
            {
                UnitIntentComponent intentValue = intent.ValueRW;
                intentValue.ClearFrameIntent();
                intent.ValueRW = intentValue;
            }

            return;
        }

        float2 moveInput = _moveInput.Value;
        float2 castTarget = _castTarget.Value;
        bool wantToCast = _wantToCast.Value;
        bool wantToInteract = _wantToInteract.Value;
        bool wantToUseProp = _wantToUseProp.Value;
        int requestedPropShortcutIndex = _requestedPropShortcutIndex.Value;
        foreach (var (_, intent) in
            SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitIntentComponent>>())
        {
            UnitIntentComponent intentValue = intent.ValueRW;
            intentValue.ClearFrameIntent();
            intentValue.MoveDirection = moveInput;
            intentValue.WantToCast = wantToCast;
            intentValue.CastTargetPosition = castTarget;
            intentValue.WantToInteract = wantToInteract;
            intentValue.WantToUseProp = wantToUseProp;
            intentValue.RequestedPropShortcutIndex = requestedPropShortcutIndex;
            intent.ValueRW = intentValue;
        }

        _wantToCast.Value = false;
        _wantToInteract.Value = false;
        _wantToUseProp.Value = false;
        _requestedPropShortcutIndex.Value = -1;
    }

    private void HandleMove(Vector2 v)
    {
        float2 val = new float2(v.x, v.y);
        if (math.lengthsq(val) > 1f)
            val = math.normalize(val);
        _moveInput.Value = val;
    }

    private void HandleMouseWorldPosition(Vector3 v)
    {
        _castTarget.Value = new float2(v.x, v.y);
    }

    private void HandleMousePress()
    {
        _wantToCast.Value = true;
    }

    private void HandleInteract()
    {
        _wantToInteract.Value = true;
    }

    private void HandleUseProp(int shortcutIndex)
    {
        if (shortcutIndex < 0)
            return;

        _wantToUseProp.Value = true;
        _requestedPropShortcutIndex.Value = shortcutIndex;
    }
}
