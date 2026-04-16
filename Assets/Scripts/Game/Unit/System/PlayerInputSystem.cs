using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CrystalMagic.Core;

/// <summary>
/// 玩家输入系统——仅负责将原始输入写入 UnitIntentComponent。
/// 不直接操作 UnitMoveComponent，由状态机决定如何使用意图。
/// </summary>
[UpdateBefore(typeof(UnitStateMachineSystem))]
partial struct PlayerInputSystem : ISystem
{
    private NativeReference<float2> _moveInput;
    private NativeReference<float2> _castTarget;
    private NativeReference<bool> _hasCastTarget;
    private NativeReference<bool> _wantToCast;
    private bool _subscribed;

    public void OnCreate(ref SystemState state)
    {
        _moveInput = new NativeReference<float2>(float2.zero, Allocator.Persistent);
        _castTarget = new NativeReference<float2>(float2.zero, Allocator.Persistent);
        _hasCastTarget = new NativeReference<bool>(false, Allocator.Persistent);
        _wantToCast = new NativeReference<bool>(false, Allocator.Persistent);
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_subscribed && InputComponent.Instance != null)
        {
            InputComponent.Instance.OnMove -= HandleMove;
            InputComponent.Instance.OnMouseWorldPosition -= HandleMouseWorldPosition;
            InputComponent.Instance.OnMousePress -= HandleMousePress;
        }
        if (_moveInput.IsCreated)
            _moveInput.Dispose();
        if (_castTarget.IsCreated)
            _castTarget.Dispose();
        if (_hasCastTarget.IsCreated)
            _hasCastTarget.Dispose();
        if (_wantToCast.IsCreated)
            _wantToCast.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_subscribed && InputComponent.Instance != null)
        {
            InputComponent.Instance.OnMove += HandleMove;
            InputComponent.Instance.OnMouseWorldPosition += HandleMouseWorldPosition;
            InputComponent.Instance.OnMousePress += HandleMousePress;
            _subscribed = true;
        }

        float2 moveInput = _moveInput.Value;
        float2 castTarget = _castTarget.Value;
        bool hasCastTarget = _hasCastTarget.Value;
        bool wantToCast = _wantToCast.Value;
        foreach (var (_, intent) in
            SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitIntentComponent>>())
        {
            intent.ValueRW.MoveDirection = moveInput;
            intent.ValueRW.WantToCast = wantToCast;
            intent.ValueRW.HasCastTarget = hasCastTarget;
            intent.ValueRW.CastTargetPosition = castTarget;
        }

        _wantToCast.Value = false;
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
        _hasCastTarget.Value = true;
    }

    private void HandleMousePress()
    {
        _wantToCast.Value = true;
    }
}
