using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.OpenField;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Resolves per-unit navigation requests into movement directions. The open set uses
/// the same binary-min-heap A* strategy as roy-t/AStar, adapted to this project's ECS
/// components, deterministic grid costs, and per-unit clearance radius.
/// </summary>
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateBefore(typeof(StateScriptSystem))]
public partial class UnitNavigationSystem : SystemBase
{
    private NavigationGrid _grid;
    private OpenFieldDungeonLayout _sourceLayout;
    private RuntimeDungeonSceneData _sourceSceneData;
    private int _gridVersion;

    protected override void OnUpdate()
    {
        RefreshGrid();

        foreach ((RefRW<UnitNavigationComponent> navigationRef,
                  RefRW<UnitMoveComponent> moveRef,
                  RefRO<LocalTransform> transformRef,
                  Entity entity) in
                 SystemAPI.Query<RefRW<UnitNavigationComponent>, RefRW<UnitMoveComponent>, RefRO<LocalTransform>>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            ref UnitNavigationComponent navigation = ref navigationRef.ValueRW;
            ref UnitMoveComponent move = ref moveRef.ValueRW;

            if (navigation.HasDestination == 0)
                continue;

            float2 position = transformRef.ValueRO.Position.xy;
            float2 target = navigation.Destination.xy;
            float stopDistance = math.max(0f, navigation.StopDistance);
            if (math.distancesq(position, target) <= stopDistance * stopDistance)
            {
                WriteDirection(ref move, float2.zero);
                continue;
            }

            if (_grid == null)
            {
                WriteDirection(ref move, math.normalizesafe(target - position, float2.zero));
                continue;
            }

            int2 startCell = _grid.WorldToCell(position);
            int2 destinationCell = _grid.WorldToCell(target);
            bool destinationCellChanged = !destinationCell.Equals(navigation.LastDestinationCell);
            if (destinationCellChanged || navigation.GridVersion != _gridVersion)
                navigation.PathDirty = 1;

            DynamicBuffer<UnitNavigationPathElement> path = EntityManager.GetBuffer<UnitNavigationPathElement>(entity);
            if (navigation.PathDirty != 0)
            {
                RebuildPath(ref navigation, path, startCell, destinationCell);
            }

            if (navigation.PathFound == 0 || path.Length == 0)
            {
                WriteDirection(ref move, float2.zero);
                continue;
            }

            int waypointIndex = math.clamp(navigation.CurrentWaypointIndex, 0, path.Length - 1);
            float tolerance = math.max(0.01f, navigation.WaypointTolerance);
            while (waypointIndex < path.Length - 1 &&
                   (startCell.Equals(path[waypointIndex].Cell) ||
                    math.distancesq(position, _grid.CellToWorld(path[waypointIndex].Cell)) <= tolerance * tolerance))
            {
                waypointIndex++;
            }

            navigation.CurrentWaypointIndex = waypointIndex;
            float2 waypoint = waypointIndex == path.Length - 1
                ? target
                : _grid.CellToWorld(path[waypointIndex].Cell);
            WriteDirection(ref move, math.normalizesafe(waypoint - position, float2.zero));
        }
    }

    private void RefreshGrid()
    {
        DungeonRuntimeMapComponent runtimeMap = null;
        foreach (DungeonRuntimeMapComponent candidate in SystemAPI.Query<DungeonRuntimeMapComponent>())
        {
            if (candidate?.OpenFieldLayout == null)
                continue;

            runtimeMap = candidate;
            break;
        }

        if (runtimeMap == null)
        {
            _grid = null;
            _sourceLayout = null;
            _sourceSceneData = null;
            return;
        }

        if (ReferenceEquals(_sourceLayout, runtimeMap.OpenFieldLayout) &&
            ReferenceEquals(_sourceSceneData, runtimeMap.SceneData))
        {
            return;
        }

        _sourceLayout = runtimeMap.OpenFieldLayout;
        _sourceSceneData = runtimeMap.SceneData;
        _grid = new NavigationGrid(_sourceLayout, _sourceSceneData);
        _gridVersion++;
    }

    private void RebuildPath(
        ref UnitNavigationComponent navigation,
        DynamicBuffer<UnitNavigationPathElement> path,
        int2 startCell,
        int2 destinationCell)
    {
        path.Clear();
        navigation.CurrentWaypointIndex = 0;
        navigation.LastDestinationCell = destinationCell;
        navigation.GridVersion = _gridVersion;
        navigation.PathDirty = 0;
        navigation.PathFound = 0;

        if (!_grid.TryFindPath(startCell, destinationCell, navigation.ClearanceRadius, path))
            return;

        navigation.PathFound = 1;
    }

    private static void WriteDirection(ref UnitMoveComponent move, float2 direction)
    {
        if (math.all(move.Direction == direction))
            return;

        move.Direction = direction;
    }

    private sealed class NavigationGrid
    {
        private static readonly int2[] s_neighborOffsets =
        {
            new(-1, 0), new(1, 0), new(0, -1), new(0, 1),
            new(-1, -1), new(-1, 1), new(1, -1), new(1, 1),
        };

        private readonly int _width;
        private readonly int _height;
        private readonly float _cellSize;
        private readonly float2 _origin;
        private readonly bool[] _walkable;
        private readonly int[] _cost;
        private readonly int[] _parent;
        private readonly int[] _seenStamp;
        private readonly int[] _closedStamp;
        private readonly List<OpenNode> _open = new();
        private readonly List<int> _reversePath = new();
        private int _searchStamp;

        public NavigationGrid(OpenFieldDungeonLayout layout, RuntimeDungeonSceneData sceneData)
        {
            _width = layout.Width;
            _height = layout.Height;
            _cellSize = math.max(0.01f, sceneData?.CellWorldSize ?? 1f);
            Vector2 sourceOrigin = sceneData?.TerrainVisual?.WorldOrigin ??
                                   new Vector2(-_width * _cellSize * 0.5f, -_height * _cellSize * 0.5f);
            _origin = new float2(sourceOrigin.x, sourceOrigin.y);
            _walkable = new bool[_width * _height];
            _cost = new int[_walkable.Length];
            _parent = new int[_walkable.Length];
            _seenStamp = new int[_walkable.Length];
            _closedStamp = new int[_walkable.Length];

            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                _walkable[ToIndex(x, y)] = layout.IsWalkable(x, y);

            if (sceneData?.ObstacleSpawns == null)
                return;

            for (int obstacleIndex = 0; obstacleIndex < sceneData.ObstacleSpawns.Count; obstacleIndex++)
            {
                List<Vector2Int> cells = sceneData.ObstacleSpawns[obstacleIndex]?.CollisionCells;
                if (cells == null)
                    continue;

                for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    Vector2Int cell = cells[cellIndex];
                    if (IsInside(cell.x, cell.y))
                        _walkable[ToIndex(cell.x, cell.y)] = false;
                }
            }
        }

        public int2 WorldToCell(float2 worldPosition)
        {
            int2 cell = (int2)math.floor((worldPosition - _origin) / _cellSize);
            return math.clamp(cell, int2.zero, new int2(_width - 1, _height - 1));
        }

        public float2 CellToWorld(int2 cell)
        {
            return _origin + (new float2(cell.x, cell.y) + 0.5f) * _cellSize;
        }

        public bool TryFindPath(
            int2 requestedStart,
            int2 requestedGoal,
            float clearanceRadius,
            DynamicBuffer<UnitNavigationPathElement> result)
        {
            if (!TryFindNearestWalkable(requestedStart, clearanceRadius, out int2 start) ||
                !TryFindNearestWalkable(requestedGoal, clearanceRadius, out int2 goal))
            {
                return false;
            }

            if (start.Equals(goal))
            {
                result.Add(new UnitNavigationPathElement { Cell = goal });
                return true;
            }

            BeginSearch();
            int startIndex = ToIndex(start.x, start.y);
            int goalIndex = ToIndex(goal.x, goal.y);
            _seenStamp[startIndex] = _searchStamp;
            _cost[startIndex] = 0;
            _parent[startIndex] = -1;
            Push(new OpenNode(startIndex, 0, Heuristic(start, goal)));

            while (_open.Count > 0)
            {
                OpenNode current = Pop();
                if (_closedStamp[current.Index] == _searchStamp ||
                    _seenStamp[current.Index] != _searchStamp ||
                    _cost[current.Index] != current.Cost)
                {
                    continue;
                }

                if (current.Index == goalIndex)
                {
                    ReconstructPath(startIndex, goalIndex, result);
                    return result.Length > 0;
                }

                _closedStamp[current.Index] = _searchStamp;
                int2 currentCell = ToCell(current.Index);
                for (int neighborIndex = 0; neighborIndex < s_neighborOffsets.Length; neighborIndex++)
                {
                    int2 offset = s_neighborOffsets[neighborIndex];
                    int2 neighbor = currentCell + offset;
                    if (!CanOccupy(neighbor, clearanceRadius))
                        continue;

                    bool diagonal = offset.x != 0 && offset.y != 0;
                    if (diagonal &&
                        (!CanOccupy(currentCell + new int2(offset.x, 0), clearanceRadius) ||
                         !CanOccupy(currentCell + new int2(0, offset.y), clearanceRadius)))
                    {
                        continue;
                    }

                    int nextIndex = ToIndex(neighbor.x, neighbor.y);
                    if (_closedStamp[nextIndex] == _searchStamp)
                        continue;

                    int newCost = current.Cost + (diagonal ? 14 : 10);
                    if (_seenStamp[nextIndex] == _searchStamp && newCost >= _cost[nextIndex])
                        continue;

                    _seenStamp[nextIndex] = _searchStamp;
                    _cost[nextIndex] = newCost;
                    _parent[nextIndex] = current.Index;
                    Push(new OpenNode(nextIndex, newCost, Heuristic(neighbor, goal)));
                }
            }

            return false;
        }

        private void BeginSearch()
        {
            _open.Clear();
            _reversePath.Clear();
            if (_searchStamp == int.MaxValue)
            {
                Array.Clear(_seenStamp, 0, _seenStamp.Length);
                Array.Clear(_closedStamp, 0, _closedStamp.Length);
                _searchStamp = 1;
            }
            else
            {
                _searchStamp++;
            }
        }

        private bool TryFindNearestWalkable(int2 requested, float clearanceRadius, out int2 result)
        {
            if (CanOccupy(requested, clearanceRadius))
            {
                result = requested;
                return true;
            }

            int maxRadius = math.min(8, math.max(_width, _height));
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    if (math.max(math.abs(x), math.abs(y)) != radius)
                        continue;

                    int2 candidate = requested + new int2(x, y);
                    if (!CanOccupy(candidate, clearanceRadius))
                        continue;

                    result = candidate;
                    return true;
                }
            }

            result = default;
            return false;
        }

        private bool CanOccupy(int2 cell, float clearanceRadius)
        {
            if (!IsInside(cell.x, cell.y) || !_walkable[ToIndex(cell.x, cell.y)])
                return false;

            float radius = math.max(0f, clearanceRadius);
            if (radius <= 0f)
                return true;

            int radiusInCells = math.max(1, (int)math.ceil(radius / _cellSize));
            float radiusSquared = radius * radius;
            for (int y = -radiusInCells; y <= radiusInCells; y++)
            for (int x = -radiusInCells; x <= radiusInCells; x++)
            {
                int checkX = cell.x + x;
                int checkY = cell.y + y;
                if (IsInside(checkX, checkY) && _walkable[ToIndex(checkX, checkY)])
                    continue;

                float edgeX = math.max(math.abs(x) * _cellSize - _cellSize * 0.5f, 0f);
                float edgeY = math.max(math.abs(y) * _cellSize - _cellSize * 0.5f, 0f);
                if (edgeX * edgeX + edgeY * edgeY < radiusSquared)
                    return false;
            }

            return true;
        }

        private void ReconstructPath(
            int startIndex,
            int goalIndex,
            DynamicBuffer<UnitNavigationPathElement> result)
        {
            _reversePath.Clear();
            int current = goalIndex;
            while (current != startIndex && current >= 0)
            {
                _reversePath.Add(current);
                current = _parent[current];
            }

            for (int index = _reversePath.Count - 1; index >= 0; index--)
                result.Add(new UnitNavigationPathElement { Cell = ToCell(_reversePath[index]) });
        }

        private static int Heuristic(int2 from, int2 to)
        {
            int2 delta = math.abs(to - from);
            int diagonal = math.min(delta.x, delta.y);
            int straight = math.max(delta.x, delta.y) - diagonal;
            return diagonal * 14 + straight * 10;
        }

        private bool IsInside(int x, int y)
        {
            return x >= 0 && x < _width && y >= 0 && y < _height;
        }

        private int ToIndex(int x, int y)
        {
            return y * _width + x;
        }

        private int2 ToCell(int index)
        {
            return new int2(index % _width, index / _width);
        }

        private void Push(OpenNode node)
        {
            int index = _open.Count;
            _open.Add(node);
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (!IsHigherPriority(_open[index], _open[parent]))
                    break;

                (_open[index], _open[parent]) = (_open[parent], _open[index]);
                index = parent;
            }
        }

        private OpenNode Pop()
        {
            OpenNode result = _open[0];
            int lastIndex = _open.Count - 1;
            OpenNode tail = _open[lastIndex];
            _open.RemoveAt(lastIndex);
            if (_open.Count == 0)
                return result;

            _open[0] = tail;
            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= _open.Count)
                    break;

                int right = left + 1;
                int best = right < _open.Count && IsHigherPriority(_open[right], _open[left]) ? right : left;
                if (!IsHigherPriority(_open[best], _open[index]))
                    break;

                (_open[index], _open[best]) = (_open[best], _open[index]);
                index = best;
            }

            return result;
        }

        private static bool IsHigherPriority(OpenNode left, OpenNode right)
        {
            int leftTotal = left.Cost + left.Heuristic;
            int rightTotal = right.Cost + right.Heuristic;
            if (leftTotal != rightTotal)
                return leftTotal < rightTotal;
            if (left.Heuristic != right.Heuristic)
                return left.Heuristic < right.Heuristic;
            return left.Index < right.Index;
        }

        private readonly struct OpenNode
        {
            public OpenNode(int index, int cost, int heuristic)
            {
                Index = index;
                Cost = cost;
                Heuristic = heuristic;
            }

            public int Index { get; }
            public int Cost { get; }
            public int Heuristic { get; }
        }
    }
}
