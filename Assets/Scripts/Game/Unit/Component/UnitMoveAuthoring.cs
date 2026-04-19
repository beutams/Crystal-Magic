using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitMoveAuthoring : MonoBehaviour
{
    class UnitMoveBaker : Baker<UnitMoveAuthoring>
    {
        public override void Bake(UnitMoveAuthoring authoring)
        {
            float baseSpeed = 5f;
            float baseAccel = 30f;
            UnitData data = UnitAuthoringUtility.ResolveUnitData(authoring);
            if (data != null)
            {
                baseSpeed = data.BaseMoveSpeed;
                baseAccel = data.BaseMaxAcceleration;
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

public struct UnitMoveComponent : IComponentData
{
    public float BaseMoveSpeed;
    public float BaseMaxAcceleration;
    public float SpeedFactor;
    public float SpeedBonus;
    public float2 AccelInput;
    public float2 Velocity;

    public float RealMoveSpeed => BaseMoveSpeed * SpeedFactor + SpeedBonus;
    public float RealMaxAcceleration => BaseMaxAcceleration * SpeedFactor + SpeedBonus;
}

/// <summary>
/// 鍗曚綅绉诲姩缁勪欢鈥斺€旀湁姝ょ粍浠跺嵆涓哄彲绉诲姩鍗曚綅銆?
///
/// AccelInput锛氱姸鎬佹満姣忓抚鍐欏叆鐨勫姞閫熸柟鍚戯紙褰掍竴鍖栵級銆?
/// MoveSystem 姣忓抚锛?
///   targetVel = AccelInput * MaxSpeed
///   Velocity  鈫?鍚?targetVel 浠?MaxAcceleration 閫艰繎
///   PhysicsVelocity.Linear = Velocity
/// </summary>
