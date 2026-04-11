using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CrystalMagic.Core;
using Unity.Entities.UniversalDelegates;
using UnityEngine.Windows;

/// <summary>
/// 玩家输入系统——仅负责将原始输入写入 UnitIntentComponent。
/// 不直接操作 UnitMoveComponent，由状态机决定如何使用意图。
/// </summary>
[UpdateBefore(typeof(UnitStateMachineSystem))]
partial struct PlayerInputSystem : ISystem
{
    private NativeReference<float2> _moveInput;
    private bool _subscribed;

    public void OnCreate(ref SystemState state)
    {
        _moveInput = new NativeReference<float2>(float2.zero, Allocator.Persistent);
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_subscribed && InputComponent.Instance != null)
            InputComponent.Instance.OnMove -= HandleMove;
        if (_moveInput.IsCreated)
            _moveInput.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_subscribed && InputComponent.Instance != null)
        {
            InputComponent.Instance.OnMove += HandleMove;
            _subscribed = true;
        }

        float2 input = _moveInput.Value;
        foreach (var (_, intent) in
            SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitIntentComponent>>())
        {
            intent.ValueRW.MoveDirection = input;
        }
    }

    private void HandleMove(Vector2 v)
    {
        float2 val = new float2(v.x, v.y);
        if (math.lengthsq(val) > 1f)
            val = math.normalize(val);
        _moveInput.Value = val;
    }
}
