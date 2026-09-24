using CrystalMagic.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves navigation requests against the dungeon collision bitset. Dirty paths are
/// rebuilt by a single Burst job that reuses one fixed-size A* scratch area; following
/// already-built paths runs in parallel for all units.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateBefore(typeof(StateScriptSystem))]
public partial struct UnitNavigationSystem : ISystem
{
    private EntityQuery _mapQuery;
    private NativeArray<int> _cost;
    private NativeArray<int> _parent;
    private NativeArray<int> _seenStamp;
    private NativeArray<int> _closedStamp;
    private NativeArray<int> _heapPosition;
    private NativeArray<NavigationOpenNode> _heap;
    private NativeArray<int> _searchStamp;
    private int _scratchCellCount;

    public void OnCreate(ref SystemState state)
    {
        _mapQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<DungeonNavigationMapComponent>(),
            ComponentType.ReadOnly<DungeonNavigationCollisionWord>());
        _searchStamp = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        state.RequireForUpdate<UnitNavigationComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_mapQuery.IsEmptyIgnoreFilter)
        {
            state.Dependency = new NavigationDirectFollowJob().ScheduleParallel(state.Dependency);
            return;
        }

        Entity mapEntity = _mapQuery.GetSingletonEntity();
        DungeonNavigationMapComponent map =
            state.EntityManager.GetComponentData<DungeonNavigationMapComponent>(mapEntity);
        DynamicBuffer<DungeonNavigationCollisionWord> collisionBuffer =
            state.EntityManager.GetBuffer<DungeonNavigationCollisionWord>(mapEntity, true);
        int requiredWordCount = DungeonNavigationMapUtility.GetRequiredWordCount(map.CellCount);
        if (map.Width <= 0 || map.Height <= 0 || map.CellSize <= 0f ||
            collisionBuffer.Length < requiredWordCount)
        {
            state.Dependency = new NavigationDirectFollowJob().ScheduleParallel(state.Dependency);
            return;
        }

        EnsureScratchCapacity(ref state, map.CellCount);
        NativeArray<DungeonNavigationCollisionWord> collisionWords = collisionBuffer.AsNativeArray();
        JobHandle pathfindHandle = new NavigationPathfindJob
        {
            Map = map,
            CollisionWords = collisionWords,
            Cost = _cost,
            Parent = _parent,
            SeenStamp = _seenStamp,
            ClosedStamp = _closedStamp,
            HeapPosition = _heapPosition,
            Heap = _heap,
            SearchStamp = _searchStamp,
        }.Schedule(state.Dependency);

        state.Dependency = new NavigationFollowJob
        {
            Map = map,
        }.ScheduleParallel(pathfindHandle);
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        DisposeScratch();
        if (_searchStamp.IsCreated)
            _searchStamp.Dispose();
    }

    private void EnsureScratchCapacity(ref SystemState state, int cellCount)
    {
        if (_scratchCellCount == cellCount && _cost.IsCreated)
            return;

        state.Dependency.Complete();
        DisposeScratch();
        _scratchCellCount = cellCount;
        _cost = new NativeArray<int>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _parent = new NativeArray<int>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _seenStamp = new NativeArray<int>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _closedStamp = new NativeArray<int>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _heapPosition = new NativeArray<int>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _heap = new NativeArray<NavigationOpenNode>(
            cellCount,
            Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);
        _searchStamp[0] = 0;
    }

    private void DisposeScratch()
    {
        if (_cost.IsCreated)
            _cost.Dispose();
        if (_parent.IsCreated)
            _parent.Dispose();
        if (_seenStamp.IsCreated)
            _seenStamp.Dispose();
        if (_closedStamp.IsCreated)
            _closedStamp.Dispose();
        if (_heapPosition.IsCreated)
            _heapPosition.Dispose();
        if (_heap.IsCreated)
            _heap.Dispose();
        _scratchCellCount = 0;
    }

    [BurstCompile]
    [WithNone(typeof(UnitDeathComponent), typeof(BattleSpectatorComponent))]
    private partial struct NavigationPathfindJob : IJobEntity
    {
        public DungeonNavigationMapComponent Map;

        [ReadOnly]
        public NativeArray<DungeonNavigationCollisionWord> CollisionWords;

        public NativeArray<int> Cost;
        public NativeArray<int> Parent;
        public NativeArray<int> SeenStamp;
        public NativeArray<int> ClosedStamp;
        public NativeArray<int> HeapPosition;
        public NativeArray<NavigationOpenNode> Heap;
        public NativeArray<int> SearchStamp;

        private void Execute(
            ref UnitNavigationComponent navigation,
            in LocalTransform transform,
            DynamicBuffer<UnitNavigationPathElement> path)
        {
            if (navigation.HasDestination == 0)
                return;

            float2 position = transform.Position.xy;
            float2 target = navigation.Destination.xy;
            float stopDistance = math.max(0f, navigation.StopDistance);
            if (math.distancesq(position, target) <= stopDistance * stopDistance)
                return;

            int2 startCell = DungeonNavigationMapUtility.WorldToCell(in Map, position);
            int2 destinationCell = DungeonNavigationMapUtility.WorldToCell(in Map, target);
            if (!destinationCell.Equals(navigation.LastDestinationCell) || navigation.GridVersion != Map.Version)
                navigation.PathDirty = 1;

            if (navigation.PathDirty == 0)
                return;

            path.Clear();
            navigation.CurrentWaypointIndex = 0;
            navigation.LastDestinationCell = destinationCell;
            navigation.GridVersion = Map.Version;
            navigation.PathDirty = 0;
            navigation.PathFound = 0;

            if (TryFindPath(startCell, destinationCell, navigation.ClearanceRadius, path))
                navigation.PathFound = 1;
        }

        private bool TryFindPath(
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

            int startIndex = DungeonNavigationMapUtility.ToIndex(in Map, start);
            int goalIndex = DungeonNavigationMapUtility.ToIndex(in Map, goal);
            if (startIndex == goalIndex)
            {
                result.Add(new UnitNavigationPathElement { CellIndex = goalIndex });
                return true;
            }

            int searchStamp = BeginSearch();
            int heapCount = 0;
            SeenStamp[startIndex] = searchStamp;
            Cost[startIndex] = 0;
            Parent[startIndex] = -1;
            Push(
                new NavigationOpenNode
                {
                    Index = startIndex,
                    Cost = 0,
                    Heuristic = Heuristic(start, goal),
                },
                ref heapCount);

            while (heapCount > 0)
            {
                NavigationOpenNode current = Pop(ref heapCount);
                if (current.Index == goalIndex)
                    return ReconstructPath(startIndex, goalIndex, result);

                ClosedStamp[current.Index] = searchStamp;
                int2 currentCell = DungeonNavigationMapUtility.ToCell(in Map, current.Index);
                for (int neighborIndex = 0; neighborIndex < 8; neighborIndex++)
                {
                    int2 offset = GetNeighborOffset(neighborIndex);
                    int2 neighbor = currentCell + offset;
                    if (!DungeonNavigationMapUtility.CanOccupy(
                            in Map,
                            CollisionWords,
                            neighbor,
                            clearanceRadius))
                    {
                        continue;
                    }

                    bool diagonal = offset.x != 0 && offset.y != 0;
                    if (diagonal &&
                        (!DungeonNavigationMapUtility.CanOccupy(
                             in Map,
                             CollisionWords,
                             currentCell + new int2(offset.x, 0),
                             clearanceRadius) ||
                         !DungeonNavigationMapUtility.CanOccupy(
                             in Map,
                             CollisionWords,
                             currentCell + new int2(0, offset.y),
                             clearanceRadius)))
                    {
                        continue;
                    }

                    int nextIndex = DungeonNavigationMapUtility.ToIndex(in Map, neighbor);
                    if (ClosedStamp[nextIndex] == searchStamp)
                        continue;

                    int newCost = current.Cost + (diagonal ? 14 : 10);
                    bool wasSeen = SeenStamp[nextIndex] == searchStamp;
                    if (wasSeen && newCost >= Cost[nextIndex])
                        continue;

                    SeenStamp[nextIndex] = searchStamp;
                    Cost[nextIndex] = newCost;
                    Parent[nextIndex] = current.Index;
                    NavigationOpenNode openNode = new()
                    {
                        Index = nextIndex,
                        Cost = newCost,
                        Heuristic = Heuristic(neighbor, goal),
                    };
                    if (wasSeen)
                        Update(openNode);
                    else
                        Push(openNode, ref heapCount);
                }
            }

            return false;
        }

        private int BeginSearch()
        {
            int nextStamp = SearchStamp[0];
            if (nextStamp == int.MaxValue)
            {
                for (int index = 0; index < SeenStamp.Length; index++)
                {
                    SeenStamp[index] = 0;
                    ClosedStamp[index] = 0;
                }

                nextStamp = 1;
            }
            else
            {
                nextStamp++;
            }

            SearchStamp[0] = nextStamp;
            return nextStamp;
        }

        private bool TryFindNearestWalkable(int2 requested, float clearanceRadius, out int2 result)
        {
            if (DungeonNavigationMapUtility.CanOccupy(
                    in Map,
                    CollisionWords,
                    requested,
                    clearanceRadius))
            {
                result = requested;
                return true;
            }

            int maxRadius = math.min(8, math.max(Map.Width, Map.Height));
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (math.max(math.abs(x), math.abs(y)) != radius)
                            continue;

                        int2 candidate = requested + new int2(x, y);
                        if (!DungeonNavigationMapUtility.CanOccupy(
                                in Map,
                                CollisionWords,
                                candidate,
                                clearanceRadius))
                        {
                            continue;
                        }

                        result = candidate;
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

        private bool ReconstructPath(
            int startIndex,
            int goalIndex,
            DynamicBuffer<UnitNavigationPathElement> result)
        {
            int current = goalIndex;
            int remaining = Parent.Length;
            while (current != startIndex && current >= 0 && remaining-- > 0)
            {
                result.Add(new UnitNavigationPathElement { CellIndex = current });
                current = Parent[current];
            }

            if (current != startIndex)
            {
                result.Clear();
                return false;
            }

            int left = 0;
            int right = result.Length - 1;
            while (left < right)
            {
                UnitNavigationPathElement temporary = result[left];
                result[left] = result[right];
                result[right] = temporary;
                left++;
                right--;
            }

            return result.Length > 0;
        }

        private void Push(NavigationOpenNode node, ref int heapCount)
        {
            int index = heapCount++;
            Heap[index] = node;
            HeapPosition[node.Index] = index;
            BubbleUp(index);
        }

        private void Update(NavigationOpenNode node)
        {
            int index = HeapPosition[node.Index];
            Heap[index] = node;
            BubbleUp(index);
        }

        private NavigationOpenNode Pop(ref int heapCount)
        {
            NavigationOpenNode result = Heap[0];
            HeapPosition[result.Index] = -1;
            heapCount--;
            if (heapCount == 0)
                return result;

            Heap[0] = Heap[heapCount];
            HeapPosition[Heap[0].Index] = 0;
            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= heapCount)
                    break;

                int right = left + 1;
                int best = right < heapCount && IsHigherPriority(Heap[right], Heap[left]) ? right : left;
                if (!IsHigherPriority(Heap[best], Heap[index]))
                    break;

                SwapHeapEntries(index, best);
                index = best;
            }

            return result;
        }

        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (!IsHigherPriority(Heap[index], Heap[parent]))
                    break;

                SwapHeapEntries(index, parent);
                index = parent;
            }
        }

        private void SwapHeapEntries(int leftIndex, int rightIndex)
        {
            NavigationOpenNode temporary = Heap[leftIndex];
            Heap[leftIndex] = Heap[rightIndex];
            Heap[rightIndex] = temporary;
            HeapPosition[Heap[leftIndex].Index] = leftIndex;
            HeapPosition[Heap[rightIndex].Index] = rightIndex;
        }

        private static int2 GetNeighborOffset(int index)
        {
            return index switch
            {
                0 => new int2(-1, 0),
                1 => new int2(1, 0),
                2 => new int2(0, -1),
                3 => new int2(0, 1),
                4 => new int2(-1, -1),
                5 => new int2(-1, 1),
                6 => new int2(1, -1),
                _ => new int2(1, 1),
            };
        }

        private static int Heuristic(int2 from, int2 to)
        {
            int2 delta = math.abs(to - from);
            int diagonal = math.min(delta.x, delta.y);
            int straight = math.max(delta.x, delta.y) - diagonal;
            return diagonal * 14 + straight * 10;
        }

        private static bool IsHigherPriority(NavigationOpenNode left, NavigationOpenNode right)
        {
            int leftTotal = left.Cost + left.Heuristic;
            int rightTotal = right.Cost + right.Heuristic;
            if (leftTotal != rightTotal)
                return leftTotal < rightTotal;
            if (left.Heuristic != right.Heuristic)
                return left.Heuristic < right.Heuristic;
            return left.Index < right.Index;
        }
    }

    [BurstCompile]
    [WithNone(typeof(UnitDeathComponent), typeof(BattleSpectatorComponent))]
    private partial struct NavigationFollowJob : IJobEntity
    {
        public DungeonNavigationMapComponent Map;

        private void Execute(
            ref UnitNavigationComponent navigation,
            ref UnitMoveComponent move,
            in LocalTransform transform,
            in DynamicBuffer<UnitNavigationPathElement> path)
        {
            if (navigation.HasDestination == 0)
                return;

            float2 position = transform.Position.xy;
            float2 target = navigation.Destination.xy;
            float stopDistance = math.max(0f, navigation.StopDistance);
            if (math.distancesq(position, target) <= stopDistance * stopDistance)
            {
                WriteDirection(ref move, float2.zero);
                return;
            }

            if (navigation.PathFound == 0 || path.Length == 0)
            {
                WriteDirection(ref move, float2.zero);
                return;
            }

            int waypointIndex = math.clamp(navigation.CurrentWaypointIndex, 0, path.Length - 1);
            int startIndex = DungeonNavigationMapUtility.ToIndex(
                in Map,
                DungeonNavigationMapUtility.WorldToCell(in Map, position));
            float tolerance = math.max(0.01f, navigation.WaypointTolerance);
            while (waypointIndex < path.Length - 1)
            {
                int cellIndex = path[waypointIndex].CellIndex;
                if ((uint)cellIndex >= (uint)Map.CellCount)
                    break;

                int2 cell = DungeonNavigationMapUtility.ToCell(in Map, cellIndex);
                if (startIndex != cellIndex &&
                    math.distancesq(position, DungeonNavigationMapUtility.CellToWorld(in Map, cell)) >
                    tolerance * tolerance)
                {
                    break;
                }

                waypointIndex++;
            }

            navigation.CurrentWaypointIndex = waypointIndex;
            int waypointCellIndex = path[waypointIndex].CellIndex;
            if ((uint)waypointCellIndex >= (uint)Map.CellCount)
            {
                navigation.PathDirty = 1;
                navigation.PathFound = 0;
                WriteDirection(ref move, float2.zero);
                return;
            }

            float2 waypoint = waypointIndex == path.Length - 1
                ? target
                : DungeonNavigationMapUtility.CellToWorld(
                    in Map,
                    DungeonNavigationMapUtility.ToCell(in Map, waypointCellIndex));
            WriteDirection(ref move, math.normalizesafe(waypoint - position, float2.zero));
        }
    }

    [BurstCompile]
    [WithNone(typeof(UnitDeathComponent), typeof(BattleSpectatorComponent))]
    private partial struct NavigationDirectFollowJob : IJobEntity
    {
        private void Execute(
            in UnitNavigationComponent navigation,
            ref UnitMoveComponent move,
            in LocalTransform transform)
        {
            if (navigation.HasDestination == 0)
                return;

            float2 position = transform.Position.xy;
            float2 target = navigation.Destination.xy;
            float stopDistance = math.max(0f, navigation.StopDistance);
            float2 direction = math.distancesq(position, target) <= stopDistance * stopDistance
                ? float2.zero
                : math.normalizesafe(target - position, float2.zero);
            WriteDirection(ref move, direction);
        }
    }

    private struct NavigationOpenNode
    {
        public int Index;
        public int Cost;
        public int Heuristic;
    }

    private static void WriteDirection(ref UnitMoveComponent move, float2 direction)
    {
        if (!math.all(move.Direction == direction))
            move.Direction = direction;
    }
}
