using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class UnitStateMachineAuthoring : MonoBehaviour
{
    [Tooltip("与 UnitDataTable.json 中 Name 字段一致，用于运行时查找状态机配置")]
    public string UnitName;

    class UnitStateMachineBaker : Baker<UnitStateMachineAuthoring>
    {
        public override void Bake(UnitStateMachineAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitStateMachineComponent
            {
                UnitName = authoring.UnitName,
            });
        }
    }
}

/// <summary>
/// 单位状态机托管组件（Managed IComponentData）
/// currentState 为 null 表示尚未初始化，系统首帧会调用 Builder 构建。
/// </summary>
public class UnitStateMachineComponent : IComponentData
{
    /// <summary>对应 UnitData.Name，运行时初始化时用于查表</summary>
    public string UnitName;

    /// <summary>当前执行的状态实例</summary>
    [System.NonSerialized] public AUnitState CurrentState;
    /// <summary>上一个状态（可用于混合/回退）</summary>
    [System.NonSerialized] public AUnitState PreviousState;

    /// <summary>Inspector 显示用（字段，由系统在切换时写入）</summary>
    public string CurrentStateName  = "None";
    public string PreviousStateName = "None";
    /// <summary>当前状态已持续的秒数</summary>
    public float StateTime;

    /// <summary>类型名 → 实例 的缓存，供外部通过名称查找状态</summary>
    [System.NonSerialized] public Dictionary<string, AUnitState> StateInstances;
}