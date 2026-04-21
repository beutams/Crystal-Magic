using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitStateMachineAuthoring : MonoBehaviour
{
    class UnitStateMachineBaker : Baker<UnitStateMachineAuthoring>
    {
        public override void Bake(UnitStateMachineAuthoring authoring)
        {
            UnitData data = UnitAuthoringUtility.ResolveUnitData(authoring);
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitStateMachineComponent
            {
                UnitDataId = data?.Id ?? 0,
                UnitName = data?.Name ?? authoring.transform.root.name,
            });
        }
    }
}

public class UnitStateMachineComponent : IComponentData
{
    public int UnitDataId;
    public string UnitName;
    [System.NonSerialized] public AUnitState CurrentState;
    [System.NonSerialized] public AUnitState PreviousState;
    public string CurrentStateName = "None";
    public string PreviousStateName = "None";
    public float StateTime;
    [System.NonSerialized] public Dictionary<string, AUnitState> StateInstances;
}
