using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

public static class ClientPlayerPhysicsPredictionUtility
{
    public static void RestoreSimulationPosition(EntityManager manager, Entity player)
    {
        if (!manager.HasComponent<UnitMoveComponent>(player) ||
            !manager.HasComponent<LocalTransform>(player))
            return;
        UnitMoveComponent move = manager.GetComponentData<UnitMoveComponent>(player);
        if (move.HasPredictedPosition == 0)
            return;
        LocalTransform transform = manager.GetComponentData<LocalTransform>(player);
        transform.Position = move.PredictedPosition;
        manager.SetComponentData(player, transform);
    }

    public static void CaptureSimulationPosition(EntityManager manager, Entity player)
    {
        if (!manager.HasComponent<UnitMoveComponent>(player) ||
            !manager.HasComponent<LocalTransform>(player) ||
            !manager.HasComponent<PhysicsVelocity>(player))
            return;
        UnitMoveComponent move = manager.GetComponentData<UnitMoveComponent>(player);
        move.PredictedPosition = manager.GetComponentData<LocalTransform>(player).Position;
        move.HasPredictedPosition = 1;
        manager.SetComponentData(player, move);
    }
}
