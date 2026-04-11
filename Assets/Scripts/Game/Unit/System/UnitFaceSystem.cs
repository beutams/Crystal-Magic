/*using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// 单位转向系统（仅负责朝向）。
/// 读取 UnitFaceComponent.faceInput，将 LocalTransform.Rotation slerp 到目标方向。
/// 与 UnitMoveSystem 完全解耦，可独立挂载。
/// </summary>
[BurstCompile]
partial struct UnitFaceSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new UnitFaceJob { deltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct UnitFaceJob : IJobEntity
{
    public float deltaTime;

    public void Execute(
        in UnitFaceComponent unitFace,
        ref LocalTransform localTransform)
    {
        // 无转向意图时保持原朝向
        if (math.lengthsq(unitFace.faceInput) < 0.001f) return;

        // 2D：绕 Z 轴旋转，atan2 得出朝向角（精灵默认朝右 +X 时无需偏移）
        float angle = math.atan2(unitFace.faceInput.y, unitFace.faceInput.x);
        quaternion targetRot = quaternion.RotateZ(angle);

        localTransform.Rotation = math.slerp(
            localTransform.Rotation,
            targetRot,
            math.saturate(deltaTime * unitFace.rotateSpeed)
        );
    }
}
*/