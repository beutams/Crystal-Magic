using UnityEngine;

/// <summary>
/// 状态机工厂
/// 仅持有 AUnitState 的创建函数，由 StateMachineRegistry 在初始化时注册，
/// 由 UnitStateMachineSystem 在构建状态机图时调用。
/// ISource / ICompareType 的注册与创建已迁移至 ComparatorFactory。
/// </summary>
public class StateMachineFactory : GeneratedFactory<string, AUnitState>
{
    /// <summary>注册 AUnitState 子类，要求有无参构造。</summary>
    public void RegisterState<T>() where T : AUnitState, new()
        => Register(typeof(T).Name, static () => new T());
    public AUnitState CreateState(string typeName)
    {
        AUnitState state = Create(typeName);
        if (state == null)
        {
            Debug.LogError($"[StateMachineFactory] 未注册状态: {typeName}，请重新生成 StateMachineRegistry");
        }
        return state;
    }
    public int StateCount => Count;
}
