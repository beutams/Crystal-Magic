using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[FactoryKey("SpawnUnit", 15, "Spawn Unit")]
public sealed class SpawnUnitActionNode : StateScriptActionNode
{
    private readonly SpawnUnitActionNodeData _data;
    private readonly StateScriptOutputPort _output;

    public SpawnUnitActionNode(SpawnUnitActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", Spawn);
        _output = AddOutput("Out");
    }

    private void Spawn()
    {
        if (Runtime == null || _data == null)
            return;

        int spawnedCount = !string.IsNullOrWhiteSpace(_data.VariableListKey)
            ? SpawnVariableList(_data.VariableListKey.Trim())
            : SpawnConfiguredUnits();
        if (spawnedCount > 0)
            _output.Pulse();
    }

    private int SpawnVariableList(string listKey)
    {
        EntityManager entityManager = Runtime.EntityManager;
        if (!entityManager.Exists(Runtime.Entity) ||
            !entityManager.HasComponent<UnitVariableComponent>(Runtime.Entity))
        {
            return 0;
        }

        if (!TryGetInt(entityManager, Runtime.Entity, $"{listKey}.count", out int count) || count <= 0)
            return 0;

        int spawnedCount = 0;
        for (int index = 0; index < count; index++)
        {
            string entryKey = $"{listKey}.{index}";
            if (!TryGetString(entityManager, Runtime.Entity, $"{entryKey}.unit", out string unitName) ||
                !TryGetFloat3(entityManager, Runtime.Entity, $"{entryKey}.position", out float3 position))
            {
                continue;
            }

            NetworkEntitySpawnInfo info = CreateSpawnInfo(unitName, position);
            if (TryGetBool(entityManager, Runtime.Entity, $"{entryKey}.hasMonsterData", out bool hasMonsterData) && hasMonsterData)
            {
                info.hasMonsterSpawnData = true;
                TryGetInt(entityManager, Runtime.Entity, $"{entryKey}.monsterSaveId", out info.monsterSaveId);
                TryGetInt(entityManager, Runtime.Entity, $"{entryKey}.monsterRegionId", out info.monsterRegionId);
                TryGetInt(entityManager, Runtime.Entity, $"{entryKey}.monsterSquadId", out info.monsterSquadId);
                TryGetBool(entityManager, Runtime.Entity, $"{entryKey}.monsterIsBoss", out info.monsterIsBoss);
            }

            if (TrySpawn(info))
                spawnedCount++;
        }

        return spawnedCount;
    }

    private int SpawnConfiguredUnits()
    {
        string[] candidates = GetCandidateUnitNames();
        if (candidates.Length == 0 || !TryGetSpawnerPosition(out float3 center))
            return 0;

        Vector3 offset = _data.CenterOffset;
        center += new float3(offset.x, offset.y, offset.z);
        float maxRadius = math.max(0f, _data.SpawnRadius);
        float minRadius = math.clamp(_data.MinSpawnRadius, 0f, maxRadius);
        int spawnedCount = 0;
        for (int index = 0; index < math.max(1, _data.Count); index++)
        {
            string unitName = candidates.Length == 1
                ? candidates[0]
                : candidates[UnityEngine.Random.Range(0, candidates.Length)];
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.right;

            float radius = Mathf.Sqrt(UnityEngine.Random.Range(minRadius * minRadius, maxRadius * maxRadius));
            float3 position = new(center.x + direction.x * radius, center.y + direction.y * radius, center.z);
            if (TrySpawn(CreateSpawnInfo(unitName, position)))
                spawnedCount++;
        }

        return spawnedCount;
    }

    private NetworkEntitySpawnInfo CreateSpawnInfo(string unitName, float3 position)
    {
        NetworkEntitySpawnInfo info = NetworkEntitySpawnUtility.CreateInfo(
            NetworkEntityPrefabType.Unit,
            unitName,
            new Vector3(position.x, position.y, position.z));
        EntityManager entityManager = Runtime.EntityManager;
        if (_data.CopyFactionFromSpawner &&
            entityManager.Exists(Runtime.Entity) &&
            entityManager.HasComponent<UnitFactionComponent>(Runtime.Entity))
        {
            info.hasFaction = true;
            info.faction = entityManager.GetComponentData<UnitFactionComponent>(Runtime.Entity).Value;
        }

        return info;
    }

    private bool TrySpawn(NetworkEntitySpawnInfo info)
    {
        EntityManager entityManager = Runtime.EntityManager;
        if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, info, out Entity entity))
            return false;

        if (_data.ShareVariablesWithSpawner)
        {
            if (!entityManager.HasComponent<UnitVariableComponent>(entity))
                entityManager.AddComponentData(entity, new UnitVariableComponent { Other = Entity.Null });
            if (!entityManager.HasBuffer<UnitVariableElement>(entity))
                entityManager.AddBuffer<UnitVariableElement>(entity);
            if (!entityManager.HasBuffer<UnitVariableConsumerElement>(entity))
                entityManager.AddBuffer<UnitVariableConsumerElement>(entity);

            UnitVariableSource.SetOther(entityManager, entity, Runtime.Entity);
        }

        if (_data.RestoreRuntimeState)
            GameRuntimeStateUtility.TryRestoreDungeonUnit(entityManager, entity);

        return true;
    }

    private string[] GetCandidateUnitNames()
    {
        List<string> names = new();
        if (_data.CandidateUnitNames != null)
        {
            for (int index = 0; index < _data.CandidateUnitNames.Length; index++)
            {
                string candidate = _data.CandidateUnitNames[index];
                if (!string.IsNullOrWhiteSpace(candidate))
                    names.Add(candidate.Trim());
            }
        }

        if (names.Count == 0 && !string.IsNullOrWhiteSpace(_data.UnitName))
            names.Add(_data.UnitName.Trim());
        return names.ToArray();
    }

    private bool TryGetSpawnerPosition(out float3 position)
    {
        EntityManager entityManager = Runtime.EntityManager;
        if (entityManager.Exists(Runtime.Entity) && entityManager.HasComponent<LocalTransform>(Runtime.Entity))
        {
            position = entityManager.GetComponentData<LocalTransform>(Runtime.Entity).Position;
            return true;
        }

        position = float3.zero;
        return false;
    }

    private static bool TryGetValue(
        EntityManager entityManager,
        Entity entity,
        string key,
        out UnitSourceValue value)
    {
        return UnitVariableSource.TryGetValue(entityManager, entity, key, out value);
    }

    private static bool TryGetInt(EntityManager entityManager, Entity entity, string key, out int value)
    {
        value = 0;
        return TryGetValue(entityManager, entity, key, out UnitSourceValue source) &&
               source.TryGetNumber(out float number) &&
               math.abs(number - math.round(number)) <= 0.0001f &&
               (value = (int)math.round(number)) >= 0;
    }

    private static bool TryGetString(EntityManager entityManager, Entity entity, string key, out string value)
    {
        value = string.Empty;
        if (!TryGetValue(entityManager, entity, key, out UnitSourceValue source) ||
            !source.TryGetString(out FixedString128Bytes fixedValue))
        {
            return false;
        }

        value = fixedValue.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetFloat3(EntityManager entityManager, Entity entity, string key, out float3 value)
    {
        value = float3.zero;
        return TryGetValue(entityManager, entity, key, out UnitSourceValue source) && source.TryGetFloat3(out value);
    }

    private static bool TryGetBool(EntityManager entityManager, Entity entity, string key, out bool value)
    {
        value = false;
        if (!TryGetValue(entityManager, entity, key, out UnitSourceValue source) || source.Type != UnitValueType.Bool)
            return false;

        value = source.Bool != 0;
        return true;
    }
}
