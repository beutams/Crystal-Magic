using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitStateMachineAuthoring : MonoBehaviour
{
/*
    [Tooltip("涓?UnitDataTable.json 涓?Name 瀛楁涓€鑷达紝鐢ㄤ簬杩愯鏃舵煡鎵剧姸鎬佹満閰嶇疆")]

*/
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

/// <summary>
/// 鍗曚綅鐘舵€佹満鎵樼缁勪欢锛圡anaged IComponentData锛?/// currentState 涓?null 琛ㄧず灏氭湭鍒濆鍖栵紝绯荤粺棣栧抚浼氳皟鐢?Builder 鏋勫缓銆?/// </summary>
