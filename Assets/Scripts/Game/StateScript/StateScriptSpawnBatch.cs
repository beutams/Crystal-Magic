using System.Collections.Generic;
using Server;
using Unity.Entities;
using Unity.Mathematics;

// Managed snapshot owned by StateScriptManagedCommandSystem, never by a static timer.
// Copying entries prevents a later roster rebuild from changing an in-flight batch.
public sealed class StateScriptSpawnBatch
{
    public struct Entry
    {
        public string UnitName;
        public float3 Position;
        public NetworkEntitySpawnInfo Info;
        public bool HasInfo;
    }

    private readonly Entry[] _entries;
    private int _next;
    private float _remainingSeconds;
    public readonly StateScriptNodeDefinition Node;
    public int SpawnedCount;
    public int RemainingCount => _entries.Length - _next;

    public StateScriptSpawnBatch(List<Entry> entries, in StateScriptNodeDefinition node)
    {
        _entries = entries.ToArray();
        Node = node;
    }

    public static bool CanContinue(EntityManager manager, Entity entity, int graphIndex)
    {
        if (!manager.Exists(entity) ||
            !manager.HasComponent<UnitStateScriptComponent>(entity) ||
            manager.GetComponentData<UnitStateScriptComponent>(entity).IsStoppedForDeath != 0 ||
            manager.HasComponent<BattleSpectatorComponent>(entity) ||
            (manager.HasComponent<UnitDeathComponent>(entity) && manager.IsComponentEnabled<UnitDeathComponent>(entity)) ||
            (manager.HasComponent<DestroyEntityFlag>(entity) && manager.IsComponentEnabled<DestroyEntityFlag>(entity)) ||
            DungeonPatrolRuntimeUtility.IsEncounterDead(manager, entity) ||
            !manager.HasBuffer<StateScriptGraphStateElement>(entity))
            return false;
        DynamicBuffer<StateScriptGraphStateElement> graphs = manager.GetBuffer<StateScriptGraphStateElement>(entity, true);
        return (uint)graphIndex < (uint)graphs.Length && graphs[graphIndex].IsActive != 0;
    }

    public bool TryTakeNext(float deltaTime, out Entry entry)
    {
        entry = default;
        if (RemainingCount == 0)
            return false;
        if (math.isfinite(deltaTime))
            _remainingSeconds -= math.max(0f, deltaTime);
        if (_remainingSeconds > 0.00001f)
            return false;

        entry = _entries[_next++];
        // Deliberately do not catch up after a long frame by releasing the whole queue.
        _remainingSeconds = Node.SpawnIntervalSeconds;
        return true;
    }
}
