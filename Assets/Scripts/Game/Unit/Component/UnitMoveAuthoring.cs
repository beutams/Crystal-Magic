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
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

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
                StateSpeedFactor    = 1f,
                AccelInput          = float2.zero,
                Velocity            = float2.zero,
            });
        }
    }
}

public struct UnitMoveComponent : IComponentData
{
    /// <summary>
    /// 单位配置中的基础最大移动速度，不包含 Buff 或状态倍率。
    /// </summary>
    public float BaseMoveSpeed;

    /// <summary>
    /// 单位配置中的基础加速度，决定当前速度追向目标速度的快慢。
    /// </summary>
    public float BaseMaxAcceleration;

    /// <summary>
    /// Buff 系统写入的移动速度倍率，默认值为 1。
    /// </summary>
    public float SpeedFactor;

    /// <summary>
    /// Buff 系统写入的移动速度加成，默认值为 0。
    /// </summary>
    public float SpeedBonus;

    /// <summary>
    /// 当前状态写入的临时速度倍率，例如施法减速，默认值为 1。
    /// </summary>
    public float StateSpeedFactor;

    /// <summary>
    /// 当前状态写入的纯移动输入方向，不要在这里混入速度倍率。
    /// </summary>
    public float2 AccelInput;

    /// <summary>
    /// 当前平滑后的移动速度，由 UnitMoveJob 计算并写入 PhysicsVelocity。
    /// </summary>
    public float2 Velocity;

    /// <summary>
    /// 移动模拟实际使用的最终最大速度。
    /// </summary>
    public float RealMoveSpeed => (BaseMoveSpeed * SpeedFactor + SpeedBonus) * StateSpeedFactor;

    /// <summary>
    /// 移动模拟实际使用的最终最大加速度。
    /// </summary>
    public float RealMaxAcceleration => (BaseMaxAcceleration * SpeedFactor + SpeedBonus) * StateSpeedFactor;
}
