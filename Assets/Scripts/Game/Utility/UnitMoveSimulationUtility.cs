using Unity.Mathematics;

public static class UnitMoveSimulationUtility
{
    public static void ResolveDesiredVelocity(
        ref UnitMoveComponent move,
        in UnitModifierComponent modifier,
        float deltaTime)
    {
        if (move.HasFrameVelocity != 0)
        {
            move.Velocity = move.FrameVelocity;
            return;
        }

        float2 targetDirection = math.normalizesafe(move.Direction, float2.zero);
        float resolvedSpeed = move.CommandMoveSpeed >= 0f
            ? move.CommandMoveSpeed
            : UnitModifierResolver.GetMoveSpeed(in move, in modifier);
        float targetSpeed = resolvedSpeed * move.StateMoveMultiplier;
        float maxSpeed = math.abs(targetSpeed);
        float maxAcceleration = math.max(
            0f,
            UnitModifierResolver.GetMaxAcceleration(in move, in modifier));
        float2 targetVelocity = targetDirection * targetSpeed;
        if (move.StateMoveMultiplier <= 0f)
        {
            move.Velocity = float2.zero;
            return;
        }

        UpdateVelocity(ref move.Velocity, targetVelocity, maxAcceleration, maxSpeed, deltaTime);
    }

    public static void UpdateVelocity(
        ref float2 velocity,
        float2 targetVelocity,
        float maxAcceleration,
        float maxSpeed,
        float deltaTime)
    {
        float2 difference = targetVelocity - velocity;
        float differenceLength = math.length(difference);

        if (differenceLength > 0.0001f)
        {
            float step = maxAcceleration * deltaTime;
            velocity = step >= differenceLength
                ? targetVelocity
                : velocity + difference / differenceLength * step;
        }

        float velocityLength = math.length(velocity);
        if (velocityLength > maxSpeed && velocityLength > 0.0001f)
            velocity = velocity / velocityLength * maxSpeed;
    }

    public static void ClearFrameCommands(ref UnitMoveComponent move)
    {
        move.Direction = float2.zero;
        move.CommandMoveSpeed = -1f;
        move.FrameVelocity = float2.zero;
        move.HasFrameVelocity = 0;
    }
}
