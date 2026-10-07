using Unity.Mathematics;
using Unity.Physics;

// Player/enemy contacts constrain existing movement; they never create a yield
// velocity or move an idle unit to resolve penetration.
public static class UnitBlockingUtility
{
    public const float Skin = 0.005f;
    private const float Epsilon = 0.00001f;

    public static bool IsHostilePair(UnitFactionType a, UnitFactionType b) =>
        (a == UnitFactionType.Player && IsEnemy(b)) ||
        (b == UnitFactionType.Player && IsEnemy(a));

    private static bool IsEnemy(UnitFactionType faction) =>
        faction == UnitFactionType.Enemy || faction == UnitFactionType.Boss;

    public static bool Constrain(
        in RigidBody a, in RigidBody b, float deltaTime,
        ref float2 velocityA, ref float2 velocityB, bool writeA = true, bool writeB = true,
        bool preserveDirection = false)
    {
        if (deltaTime <= Epsilon || !a.Collider.IsCreated || !b.Collider.IsCreated)
            return false;

        float2 relativeVelocity = velocityA - velocityB;
        if (math.lengthsq(relativeVelocity) <= Epsilon * Epsilon)
            return false;

        Aabb sweptA = SweptBounds(in a, velocityA * deltaTime);
        Aabb sweptB = SweptBounds(in b, velocityB * deltaTime);
        if (!sweptA.Overlaps(sweptB))
            return false;

        float2 normal;
        float allowedClosingSpeed;
        ColliderDistanceInput distanceInput = new(a.Collider, Skin, a.WorldFromBody, a.Scale);
        if (b.CalculateDistance(distanceInput, out DistanceHit distance))
        {
            normal = math.normalizesafe(-distance.SurfaceNormal.xy);
            allowedClosingSpeed = math.max(0f, distance.Distance - Skin) / deltaTime;
        }
        else
        {
            ColliderCastInput cast = new(a.Collider, a.WorldFromBody.pos,
                a.WorldFromBody.pos + new float3(relativeVelocity * deltaTime, 0),
                a.WorldFromBody.rot, a.Scale);
            if (!b.CastCollider(cast, out ColliderCastHit hit))
                return false;
            normal = math.normalizesafe(-hit.SurfaceNormal.xy);
            allowedClosingSpeed = math.max(0f,
                math.dot(relativeVelocity, normal) * hit.Fraction - Skin / deltaTime);
        }

        float closingSpeed = math.dot(relativeVelocity, normal);
        float remove = closingSpeed - allowedClosingSpeed;
        if (remove <= Epsilon)
            return false;

        // Only remove components directed INTO the other body. Outward motion
        // remains unchanged, including a player being chased from behind.
        float inwardA = writeA ? math.max(0f, math.dot(velocityA, normal)) : 0f;
        float inwardB = writeB ? math.max(0f, math.dot(velocityB, -normal)) : 0f;
        float total = inwardA + inwardB;
        if (total <= Epsilon)
            return false;
        float fraction = math.min(1f, remove / total);
        if (preserveDirection)
        {
            // A post-physics correction may shorten the completed path, but must
            // not turn it sideways into a wall that physics has just resolved.
            if (inwardA > Epsilon) velocityA *= 1f - fraction;
            if (inwardB > Epsilon) velocityB *= 1f - fraction;
        }
        else
        {
            velocityA -= normal * (inwardA * fraction);
            velocityB += normal * (inwardB * fraction);
        }
        return true;
    }

    private static Aabb SweptBounds(in RigidBody body, float2 displacement)
    {
        Aabb bounds = body.CalculateAabb();
        float3 offset = new(displacement, 0);
        bounds.Min += math.min(float3.zero, offset) - Skin;
        bounds.Max += math.max(float3.zero, offset) + Skin;
        return bounds;
    }
}
