using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(UnitMoveAuthoring))]
public sealed class UnitNavigationAuthoring : MonoBehaviour
{
    private sealed class UnitNavigationBaker : Baker<UnitNavigationAuthoring>
    {
        public override void Bake(UnitNavigationAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float clearanceRadius = ResolveColliderClearance(authoring);
            float waypointTolerance = 0.12f;
            UnitNavigationModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitNavigationModuleData>(authoring);
            if (data != null)
            {
                if (data.ClearanceRadius >= 0f)
                    clearanceRadius = data.ClearanceRadius;
                waypointTolerance = math.max(0.01f, data.WaypointTolerance);
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitNavigationComponent
            {
                ClearanceRadius = clearanceRadius,
                WaypointTolerance = waypointTolerance,
                LastDestinationCell = UnitNavigationComponent.InvalidCell,
                GridVersion = -1,
                PathDirty = 1,
            });
            AddBuffer<UnitNavigationPathElement>(entity);
        }

        private static float ResolveColliderClearance(UnitNavigationAuthoring authoring)
        {
            Collider collider = authoring.GetComponent<Collider>();
            if (collider != null)
            {
                Vector3 extents = collider.bounds.extents;
                float radius = Mathf.Max(extents.x, extents.y);
                if (radius > 0.01f)
                    return radius;
            }

            Collider2D collider2D = authoring.GetComponent<Collider2D>();
            if (collider2D != null)
            {
                Vector3 extents = collider2D.bounds.extents;
                float radius = Mathf.Max(extents.x, extents.y);
                if (radius > 0.01f)
                    return radius;
            }

            return 0.35f;
        }
    }
}

public struct UnitNavigationComponent : IComponentData
{
    public static int2 InvalidCell => new(int.MinValue, int.MinValue);

    public float3 Destination;
    public float StopDistance;
    public float ClearanceRadius;
    public float WaypointTolerance;
    public int2 LastDestinationCell;
    public int GridVersion;
    public int CurrentWaypointIndex;
    public byte HasDestination;
    public byte PathDirty;
    public byte PathFound;
}

[InternalBufferCapacity(16)]
public struct UnitNavigationPathElement : IBufferElementData
{
    public int2 Cell;
}

public static class UnitNavigationUtility
{
    public static void SetDestination(
        ref UnitNavigationComponent navigation,
        float3 destination,
        float stopDistance = 0f)
    {
        if (navigation.HasDestination == 0)
            navigation.PathDirty = 1;

        navigation.Destination = destination;
        navigation.StopDistance = math.max(0f, stopDistance);
        navigation.HasDestination = 1;
    }

    public static void Stop(ref UnitNavigationComponent navigation)
    {
        navigation.HasDestination = 0;
        navigation.PathDirty = 0;
        navigation.PathFound = 0;
        navigation.CurrentWaypointIndex = 0;
        navigation.LastDestinationCell = UnitNavigationComponent.InvalidCell;
    }
}

[UnitSourceProvider(typeof(UnitNavigationComponent), typeof(UnitNavigationAuthoring))]
public static class UnitNavigationSource
{
    [UnitSourceSet(0, "unit.navigation.setDestination", UnitValueCategory.Float3,
        ParameterNames = new[] { "Destination" })]
    [UnitSourceSet(1, "unit.navigation.stop", UnitValueCategory.Any,
        ParameterNames = new[] { "Ignored" })]
    public static bool TrySet(int operation, ref UnitNavigationComponent navigation, in UnitSourceArguments arguments)
    {
        switch (operation)
        {
            case 0 when arguments.TryGetFloat3(0, out float3 destination):
                UnitNavigationUtility.SetDestination(ref navigation, destination);
                return true;
            case 1 when arguments.Count == 1:
                UnitNavigationUtility.Stop(ref navigation);
                return true;
            default:
                return false;
        }
    }
}
