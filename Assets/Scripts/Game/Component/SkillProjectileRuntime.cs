using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct SkillProjectileHitEntityElement : IBufferElementData
{
    public Entity Value;
    public float CooldownRemaining;
}

public static class SkillProjectileHitHistory
{
    public static void Advance(ref DynamicBuffer<SkillProjectileHitEntityElement> hits, float deltaTime)
    {
        for (int index = 0; index < hits.Length; index++)
        {
            SkillProjectileHitEntityElement hit = hits[index];
            hit.CooldownRemaining = math.max(0f, hit.CooldownRemaining - deltaTime);
            hits[index] = hit;
        }
    }

    public static bool Blocks(in DynamicBuffer<SkillProjectileHitEntityElement> hits, Entity entity, bool allowRepeat)
    {
        for (int index = 0; index < hits.Length; index++)
            if (hits[index].Value == entity)
                return !allowRepeat || hits[index].CooldownRemaining > 0f;
        return false;
    }

    public static void Record(ref DynamicBuffer<SkillProjectileHitEntityElement> hits, Entity entity, float interval)
    {
        SkillProjectileHitEntityElement hit = new() { Value = entity, CooldownRemaining = interval };
        for (int index = 0; index < hits.Length; index++)
        {
            if (hits[index].Value != entity)
                continue;
            hits[index] = hit;
            return;
        }
        hits.Add(hit);
    }
}

public struct SkillProjectileConditionInstructionElement : IBufferElementData
{
    public ExpressionInstruction Value;
}

public struct SkillProjectileConditionLiteralElement : IBufferElementData
{
    public UnitSourceValue Value;
}

public enum SkillProjectileConditionState : byte
{
    None,
    Valid,
    Invalid,
}

public struct SkillProjectilePayloadComponent : IComponentData
{
    public EffectRequestContext Context;
    public byte OwnsManagedContext;
    public SkillProjectileConditionState CollisionConditionState;
    public EffectDataListId OnCollisionEffectListId;
    public EffectDataListId OnDestroyEffectListId;
}

public struct SkillProjectileFrameResultComponent : IComponentData
{
    public Entity HitEntity;
    public float3 HitPosition;
    public float3 TerrainHitPosition;
    public byte HasTerrainHit;
    public byte HasHit;
    public byte ShouldDestroy;
    public byte TriggerDestroyEffects;
    public byte DestroyUsesHitContext;
}

public struct SkillProjectileVisualLinkComponent : IComponentData
{
    public Entity VisualEntity;
}

/// <summary>Sweeps a projectile circle through solid cells without treating void as a wall.</summary>
public static class ProjectileTerrainCollisionUtility
{
    public static bool TryCast(in DungeonNavigationMapComponent map,
        NativeArray<DungeonNavigationCollisionWord> words, float2 start, float2 end, float radius,
        out float fraction)
    {
        fraction = float.MaxValue;
        if (map.Width <= 0 || map.Height <= 0 || map.CellSize <= 0f || !words.IsCreated)
            return false;

        radius = math.max(0f, radius);
        float2 delta = end - start;
        if (!ClipBox(start, delta, map.WorldOrigin - radius,
                map.WorldOrigin + new float2(map.Width, map.Height) * map.CellSize + radius,
                out float enter, out float exit))
            return false;

        // Walk cells along the segment, including the circle's neighbouring cells.
        // This avoids scanning the whole rectangle of a long diagonal shot.
        int2 cell = (int2)math.floor((start + delta * enter - map.WorldOrigin) / map.CellSize);
        int2 step = (int2)math.sign(delta);
        float2 next = new(float.PositiveInfinity);
        float2 stride = new(float.PositiveInfinity);
        for (int axis = 0; axis < 2; axis++)
        {
            if (step[axis] == 0)
                continue;
            float boundary = map.WorldOrigin[axis] + (cell[axis] + (step[axis] > 0 ? 1 : 0)) * map.CellSize;
            next[axis] = (boundary - start[axis]) / delta[axis];
            stride[axis] = map.CellSize / math.abs(delta[axis]);
        }

        int reach = math.max(1, (int)math.ceil(radius / map.CellSize));
        while (true)
        {
            int2 min = math.max(int2.zero, cell - reach);
            int2 max = math.min(new int2(map.Width - 1, map.Height - 1), cell + reach);
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int index = y * map.Width + x;
                if ((uint)(index >> 6) >= (uint)words.Length ||
                    (words[index >> 6].ProjectileValue & (1UL << (index & 63))) == 0)
                    continue;
                float2 corner = map.WorldOrigin + new float2(x, y) * map.CellSize;
                if (TryHitCell(start, end, radius, corner, corner + map.CellSize, out float hit))
                    fraction = math.min(fraction, hit);
            }

            float advance = math.cmin(next);
            if (advance > exit || advance > fraction || !math.isfinite(advance))
                break;
            bool moveX = next.x <= next.y;
            bool moveY = next.y <= next.x;
            if (moveX) { cell.x += step.x; next.x += stride.x; }
            if (moveY) { cell.y += step.y; next.y += stride.y; }
        }
        return fraction <= 1f;
    }

    public static bool TryHitCell(float2 start, float2 end, float radius, float2 min, float2 max,
        out float fraction)
    {
        radius = math.max(0f, radius);
        fraction = float.MaxValue;
        float2 delta = end - start;
        // A rounded rectangle is two strips plus four circles. Expanding a box in
        // both axes alone would incorrectly block empty space beside its corners.
        if (ClipBox(start, delta, min - new float2(radius, 0f), max + new float2(radius, 0f), out float hit, out _))
            fraction = hit;
        if (ClipBox(start, delta, min - new float2(0f, radius), max + new float2(0f, radius), out hit, out _))
            fraction = math.min(fraction, hit);
        if (radius > 0f)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                float2 center = new((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y);
                if (TryHitCircle(start, end, center, radius, out hit))
                    fraction = math.min(fraction, hit);
            }
        }
        return fraction <= 1f;
    }

    public static bool TryHitCircle(float2 start, float2 end, float2 center, float radius, out float fraction)
    {
        fraction = 0f;
        float2 offset = start - center;
        float c = math.lengthsq(offset) - radius * radius;
        if (c <= 0f)
            return true;
        float2 delta = end - start;
        float a = math.lengthsq(delta);
        float b = math.dot(offset, delta);
        if (a <= 0.00000001f || b >= 0f)
            return false;
        float discriminant = b * b - a * c;
        if (discriminant < 0f)
            return false;
        // Stable form of the entering root, including very long movement segments.
        fraction = c / (-b + math.sqrt(discriminant));
        return fraction >= 0f && fraction <= 1f;
    }

    private static bool ClipBox(float2 start, float2 delta, float2 min, float2 max,
        out float enter, out float exit)
    {
        enter = 0f;
        exit = 1f;
        for (int axis = 0; axis < 2; axis++)
        {
            if (delta[axis] == 0f)
            {
                if (start[axis] < min[axis] || start[axis] > max[axis])
                    return false;
                continue;
            }
            float a = (min[axis] - start[axis]) / delta[axis];
            float b = (max[axis] - start[axis]) / delta[axis];
            enter = math.max(enter, math.min(a, b));
            exit = math.min(exit, math.max(a, b));
            if (enter > exit)
                return false;
        }
        return true;
    }
}
