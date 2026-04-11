using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitMoveAuthoring : MonoBehaviour
{
    public string UnitName;

    class UnitMoveBaker : Baker<UnitMoveAuthoring>
    {
        public override void Bake(UnitMoveAuthoring authoring)
        {
            float baseSpeed = 5f;
            float baseAccel = 30f;
            if (!string.IsNullOrEmpty(authoring.UnitName))
            {
                UnitData data = EditorComponents.Data.Find<UnitData>(r => r.Name == authoring.UnitName);
                if (data != null)
                {
                    baseSpeed = data.BaseMoveSpeed;
                    baseAccel = data.BaseMaxAcceleration;
                }
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitMoveComponent
            {
                BaseMoveSpeed       = baseSpeed,
                BaseMaxAcceleration = baseAccel,
                SpeedFactor         = 1f,
                SpeedBonus          = 0f,
                AccelInput          = float2.zero,
                Velocity            = float2.zero,
            });
        }
    }
}

/// <summary>
/// 单位移动组件——有此组件即为可移动单位。
///
/// AccelInput：状态机每帧写入的加速方向（归一化）。
/// MoveSystem 每帧：
///   targetVel = AccelInput * MaxSpeed
///   Velocity  → 向 targetVel 以 MaxAcceleration 逼近
///   PhysicsVelocity.Linear = Velocity
/// </summary>
public struct UnitMoveComponent : IComponentData
{
    // ── 配置（Baker 写入，Buff 可改）──────────────
    public float BaseMoveSpeed;
    public float BaseMaxAcceleration;
    public float SpeedFactor;
    public float SpeedBonus;

    // ── 运行时状态 ────────────────────────────────
    /// <summary>加速方向意图
    public float2 AccelInput;
    /// <summary>当前速度向量
    public float2 Velocity;

    public float RealMoveSpeed       => BaseMoveSpeed       * SpeedFactor + SpeedBonus;
    public float RealMaxAcceleration => BaseMaxAcceleration  * SpeedFactor + SpeedBonus;
}
