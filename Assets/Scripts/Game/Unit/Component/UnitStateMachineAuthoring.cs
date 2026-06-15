using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitStateMachineAuthoring : MonoBehaviour
{
    class UnitStateMachineBaker : Baker<UnitStateMachineAuthoring>
    {
        public override void Bake(UnitStateMachineAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            UnitData data = UnitAuthoringUtility.ResolveUnitData(authoring);
            Transform root = authoring.transform.root != null
                ? authoring.transform.root
                : authoring.transform;
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddBuffer<UnitControlElement>(entity);
            AddComponent(entity, new UnitControlStateComponent
            {
                ActiveType = UnitControlType.None,
                RemainingTime = 0f,
                ActivePriority = 0,
                LockMove = 0,
                LockCast = 0,
                HasControl = 0,
                ActiveSourceEntity = Entity.Null,
            });
            AddComponent(entity, new UnitKnockbackComponent
            {
                Velocity = Unity.Mathematics.float2.zero,
                Damping = 0f,
            });
            AddComponentObject(entity, new UnitStateMachineComponent
            {
                UnitDataId = data?.Id ?? -1,
                UnitName = data?.Name ?? root.name,
            });
            AddComponent(entity, UnitAnimationComponent.CreateDefault(new Unity.Collections.FixedString128Bytes(root.name)));
            AddComponent(entity, new UnitAnimationFrameUvMinProperty
            {
                Value = new float4(0f, 0f, 0f, 0f),
            });
            AddComponent(entity, new UnitAnimationFrameUvSizeProperty
            {
                Value = new float4(1f, 1f, 0f, 0f),
            });
            AddComponent(entity, new UnitAnimationFrameWorldSizeProperty
            {
                Value = new float4(1f, 1f, 0f, 0f),
            });
            AddComponent(entity, new UnitAnimationFramePivotOffsetProperty
            {
                Value = new float4(0f, 0f, 0f, 0f),
            });
        }
    }
}

public class UnitStateMachineComponent : IComponentData
{
    public int UnitDataId;
    public string UnitName;
    [System.NonSerialized] public AUnitState InitialState;
    [System.NonSerialized] public AUnitState CurrentState;
    [System.NonSerialized] public AUnitState PreviousState;
    public string InitialStateName = "None";
    public string CurrentStateName = "None";
    public string PreviousStateName = "None";
    public float StateTime;
    [System.NonSerialized] public Dictionary<string, AUnitState> StateInstances;
}
