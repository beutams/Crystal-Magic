using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace CrystalMagic.Game.Testing
{
    /// <summary>Owns a private, manually stepped ECS World. Never changes the application's World or PlayerLoop.</summary>
    public sealed class NavigationTestSimulation : IDisposable
    {
        private World world;
        private SystemHandle navigation, query, avoidance, movement;
        private PhysicsSystemGroup physics;
        private readonly List<BlobAssetReference<Collider>> colliders = new();
        private readonly NavigationTestSettings settings;
        private readonly Entity[] agents;
        private readonly Vector3[] targetVelocities;
        private readonly bool captureAvoidanceDebug;
        public DungeonNavigationMapComponent Map { get; private set; }
        public double Elapsed { get; private set; }
        public bool Started { get; private set; }
        public bool IsCreated => world != null && world.IsCreated;
        public int Count => agents.Length;
        public float TimeStep => settings.FixedDeltaTime;
        public bool UsesAStar => settings.UseAStar;
        public bool UsesOrca => settings.UseOrca;
        public NavigationTestAgent GetAgent(int index) => settings.Agents[index];
        public int GetAgentIndex(Entity entity) => Array.IndexOf(agents, entity);

        public NavigationTestSimulation(NavigationTestSettings source, bool captureAvoidanceDebug = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            settings = JsonUtility.FromJson<NavigationTestSettings>(JsonUtility.ToJson(source));
            ValidateSettings(settings);
            agents = new Entity[settings.Agents.Count];
            targetVelocities = new Vector3[agents.Length];
            this.captureAvoidanceDebug = captureAvoidanceDebug;
            try
            {
                world = new World("ORCA + A* Test", WorldFlags.Game);
                Map = new DungeonNavigationMapComponent
                {
                    Width = settings.Width, Height = settings.Height, CellSize = settings.CellSize,
                    WorldOrigin = -new float2(settings.Width, settings.Height) * settings.CellSize * 0.5f,
                    Version = 1,
                };
                CreateMap();
                InstallSystems();
                for (int i = 0; i < agents.Length; i++) agents[i] = CreateAgent(settings.Agents[i]);
            }
            catch { Dispose(); throw; }
        }

        public static void ValidateSettings(NavigationTestSettings value)
        {
            if (value == null || value.Width < 4 || value.Width > 128 || value.Height < 4 || value.Height > 128)
                throw new ArgumentException("地图宽高必须在 4～128 格之间。");
            if (!Finite(value.CellSize) || value.CellSize < 0.1f || value.CellSize > 10 ||
                !Finite(value.FixedDeltaTime) || value.FixedDeltaTime < 0.005f || value.FixedDeltaTime > 0.05f ||
                !Finite(value.StopDistance) || value.StopDistance < 0.01f ||
                !Finite(value.NeighborDistance) || value.NeighborDistance < 0 || value.NeighborDistance > 1024 ||
                !Finite(value.TimeHorizon) || value.TimeHorizon < 0.01f || value.TimeHorizon > 60 ||
                !Finite(value.RadiusPadding) || value.RadiusPadding < 0 || value.RadiusPadding > 10 || value.MaxNeighbors < 0 || value.MaxNeighbors > 32)
                throw new ArgumentException("地图、时间步长或 ORCA 参数无效。");
            if (value.Agents == null || value.Agents.Count == 0 || value.Agents.Count > 256)
                throw new ArgumentException("请添加 1～256 个测试单位。");
            if (value.Obstacles == null || value.Obstacles.Count > 512)
                throw new ArgumentException("障碍配置无效（最多 512 块）。");
            for (int i = 0; i < value.Obstacles.Count; i++)
            {
                RectInt area = value.Obstacles[i];
                if (area.width <= 0 || area.height <= 0 || area.x < 0 || area.y < 0 ||
                    (long)area.x + area.width > value.Width || (long)area.y + area.height > value.Height)
                    throw new ArgumentException($"障碍 #{i + 1} 无效：X={area.x}，Y={area.y}，宽={area.width}，高={area.height}。" +
                        $"请在“地图、障碍和寻路参数”中修改：坐标使用左下角格坐标，宽高须大于 0，整个矩形须在 {value.Width}×{value.Height} 格地图内。");
            }
            foreach (var agent in value.Agents)
            {
                if (agent == null || !Finite(agent.Radius) || agent.Radius < 0.05f ||
                    !Finite(agent.Speed) || agent.Speed <= 0 || agent.Speed > 100 ||
                    !Finite(agent.Acceleration) || agent.Acceleration <= 0 || agent.Acceleration > 10000)
                    throw new ArgumentException("单位半径、速度、加速度必须是有效正数。");
                CheckPosition(value, agent.Start, agent.Radius, agent.Name + " 起点");
                CheckPosition(value, agent.Destination, agent.Radius, agent.Name + " 目标点");
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void CheckPosition(NavigationTestSettings data, Vector2 point, float radius, string label)
        {
            Vector2 half = new Vector2(data.Width, data.Height) * data.CellSize * 0.5f;
            if (!Finite(point.x) || !Finite(point.y) || Mathf.Abs(point.x) + radius >= half.x ||
                Mathf.Abs(point.y) + radius >= half.y)
                throw new ArgumentException(label + " 超出地图边界。");
            foreach (RectInt area in data.Obstacles)
            {
                Vector2 min = -half + new Vector2(area.xMin, area.yMin) * data.CellSize;
                Vector2 max = -half + new Vector2(area.xMax, area.yMax) * data.CellSize;
                Vector2 nearest = new(Mathf.Clamp(point.x, min.x, max.x), Mathf.Clamp(point.y, min.y, max.y));
                if ((nearest - point).sqrMagnitude <= radius * radius)
                    throw new ArgumentException(label + " 与障碍重叠，请移动圆形单位/目标标记。");
            }
        }

        private void InstallSystems()
        {
            // Register the package's own complete physics pipeline, including its create-order dependencies.
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default)
                .Where(t => t.Assembly == typeof(PhysicsSystemGroup).Assembly)
                .Concat(new[] { typeof(FixedStepSimulationSystemGroup), typeof(TransformSystemGroup),
                    typeof(BeginFixedStepSimulationEntityCommandBufferSystem), typeof(EndFixedStepSimulationEntityCommandBufferSystem) })
                .Distinct().ToArray();
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            physics = world.GetExistingSystemManaged<PhysicsSystemGroup>();
            var step = PhysicsStep.Default;
            step.Gravity = float3.zero;
            world.EntityManager.AddComponentData(world.EntityManager.CreateEntity(), step);
            navigation = world.GetOrCreateSystem<UnitNavigationSystem>();
            query = world.GetOrCreateSystem<UnitQueryBuildSystem>();
            avoidance = world.GetOrCreateSystem<UnitAvoidanceSystem>();
            movement = world.GetOrCreateSystem<UnitMoveSystem>();
        }

        private void CreateMap()
        {
            using var storage = new NativeArray<DungeonNavigationCollisionWord>(
                DungeonNavigationMapUtility.GetRequiredWordCount(Map.CellCount), Allocator.Temp);
            var words = storage;
            foreach (RectInt area in settings.Obstacles)
            {
                for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    int index = y * Map.Width + x;
                    var word = words[index >> 6]; word.Value |= 1UL << (index & 63); words[index >> 6] = word;
                }
                CreateWall(Map.WorldOrigin + new float2(area.center.x, area.center.y) * Map.CellSize,
                    new float2(area.width, area.height) * Map.CellSize);
            }
            float2 half = new float2(Map.Width, Map.Height) * Map.CellSize * 0.5f;
            CreateWall(new float2(-half.x - 0.5f, 0), new float2(1, half.y * 2 + 2));
            CreateWall(new float2(half.x + 0.5f, 0), new float2(1, half.y * 2 + 2));
            CreateWall(new float2(0, -half.y - 0.5f), new float2(half.x * 2, 1));
            CreateWall(new float2(0, half.y + 0.5f), new float2(half.x * 2, 1));
            // No navigation map selects the production system's existing direct-follow branch.
            if (settings.UseAStar)
            {
                Entity map = world.EntityManager.CreateEntity();
                world.EntityManager.AddComponentData(map, Map);
                world.EntityManager.AddBuffer<DungeonNavigationCollisionWord>(map).CopyFrom(words);
            }
        }

        private void CreateWall(float2 center, float2 size)
        {
            var blob = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero, Size = new float3(size, 4), Orientation = quaternion.identity, BevelRadius = 0,
            }, CollisionFilter.Default, new Unity.Physics.Material { Friction = 0, Restitution = 0 });
            colliders.Add(blob);
            Entity entity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(new float3(center, 0)));
            world.EntityManager.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(new float3(center, 0)) });
            world.EntityManager.AddComponentData(entity, new PhysicsCollider { Value = blob });
            world.EntityManager.AddSharedComponent(entity, new PhysicsWorldIndex());
        }

        private Entity CreateAgent(NavigationTestAgent data)
        {
            EntityManager manager = world.EntityManager;
            Entity entity = manager.CreateEntity();
            float3 position = new(data.Start.x, data.Start.y, 0);
            manager.AddComponentData(entity, LocalTransform.FromPosition(position));
            manager.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            manager.AddComponentData(entity, new UnitFactionComponent { Value = UnitFactionType.Enemy });
            manager.AddComponentData(entity, new UnitMoveComponent
            {
                BaseMoveSpeed = data.Speed, BaseMaxAcceleration = data.Acceleration,
                StateMoveMultiplier = 1, CommandMoveSpeed = -1, LastObservedPosition = position,
            });
            manager.AddComponentData(entity, new UnitNavigationComponent
            {
                ClearanceRadius = data.Radius, WaypointTolerance = 0.12f,
                LastDestinationCell = UnitNavigationComponent.InvalidCell, GridVersion = -1, PathDirty = 1,
            });
            manager.AddBuffer<UnitNavigationPathElement>(entity);
            if (settings.UseOrca)
            {
                manager.AddComponentData(entity, new UnitAvoidanceComponent
                {
                    NeighborDistance = settings.NeighborDistance, MaxNeighbors = settings.MaxNeighbors,
                    TimeHorizon = settings.TimeHorizon, RadiusPadding = settings.RadiusPadding,
                });
                if (captureAvoidanceDebug)
                {
                    manager.AddComponent<UnitAvoidanceDebugData>(entity);
                    manager.AddBuffer<UnitAvoidanceDebugConstraint>(entity);
                }
            }
            var blob = Unity.Physics.SphereCollider.Create(new SphereGeometry { Center = float3.zero, Radius = data.Radius },
                CollisionFilter.Default, new Unity.Physics.Material { Friction = 0, Restitution = 0 });
            colliders.Add(blob);
            manager.AddComponentData(entity, new PhysicsCollider { Value = blob });
            var mass = PhysicsMass.CreateDynamic(blob.Value.MassProperties, 1);
            mass.InverseInertia = float3.zero;
            manager.AddComponentData(entity, mass);
            manager.AddComponentData(entity, new PhysicsVelocity());
            manager.AddComponentData(entity, new PhysicsGravityFactor { Value = 0 });
            manager.AddSharedComponent(entity, new PhysicsWorldIndex());
            return entity;
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (Started) return;
            // All destinations are committed before the first navigation/physics tick.
            for (int i = 0; i < agents.Length; i++)
            {
                var nav = world.EntityManager.GetComponentData<UnitNavigationComponent>(agents[i]);
                Vector2 target = settings.Agents[i].Destination;
                UnitNavigationUtility.SetDestination(ref nav, new float3(target.x, target.y, 0), settings.StopDistance);
                world.EntityManager.SetComponentData(agents[i], nav);
            }
            Started = true;
        }

        public void Step()
        {
            ThrowIfDisposed();
            if (!Started) return;
            Elapsed += settings.FixedDeltaTime;
            world.SetTime(new TimeData(Elapsed, settings.FixedDeltaTime));
            navigation.Update(world.Unmanaged);
            // UnitMove clears frame commands. Preserve the actual navigation request
            // before that happens, including when ORCA is disabled for comparison.
            world.EntityManager.CompleteAllTrackedJobs();
            for (int i = 0; i < agents.Length; i++)
            {
                var move = world.EntityManager.GetComponentData<UnitMoveComponent>(agents[i]);
                var modifiers = UnitModifierComponent.CreateIdentity();
                float speed = move.CommandMoveSpeed >= 0 ? move.CommandMoveSpeed :
                    UnitModifierResolver.GetMoveSpeed(in move, in modifiers);
                float2 target = math.normalizesafe(move.Direction) * speed * move.StateMoveMultiplier;
                targetVelocities[i] = new Vector3(target.x, target.y, 0);
            }
            query.Update(world.Unmanaged);
            if (settings.UseOrca) avoidance.Update(world.Unmanaged);
            movement.Update(world.Unmanaged);
            physics.Update();
            world.EntityManager.CompleteAllTrackedJobs();
            // This World is not in PlayerLoop, so advance its frame allocator explicitly.
            world.Unmanaged.ResetUpdateAllocator();
        }

        public NavigationTestSnapshot Snapshot(int index, List<Vector3> path = null)
        {
            ThrowIfDisposed();
            Entity entity = agents[index];
            var manager = world.EntityManager;
            var nav = manager.GetComponentData<UnitNavigationComponent>(entity);
            var move = manager.GetComponentData<UnitMoveComponent>(entity);
            float3 position = manager.GetComponentData<LocalTransform>(entity).Position;
            var cells = manager.GetBuffer<UnitNavigationPathElement>(entity, true);
            int waypointIndex = math.clamp(nav.CurrentWaypointIndex, 0, cells.Length);
            Vector3 destination = settings.Agents[index].Destination;
            bool hasWaypoint = nav.HasDestination != 0 && (!UsesAStar || waypointIndex < cells.Length);
            Vector3 waypoint = destination;
            if (UsesAStar && hasWaypoint && waypointIndex < cells.Length - 1)
            {
                float2 point = DungeonNavigationMapUtility.CellToWorld(Map,
                    DungeonNavigationMapUtility.ToCell(Map, cells[waypointIndex].CellIndex));
                waypoint = new Vector3(point.x, point.y, 0);
            }
            if (path != null)
            {
                path.Clear();
                for (int i = waypointIndex; i < cells.Length; i++)
                {
                    float2 point = DungeonNavigationMapUtility.CellToWorld(Map,
                        DungeonNavigationMapUtility.ToCell(Map, cells[i].CellIndex));
                    path.Add(i == cells.Length - 1 ? destination : new Vector3(point.x, point.y, 0));
                }
            }
            return new NavigationTestSnapshot
            {
                Position = position, Velocity = new Vector3(move.Velocity.x, move.Velocity.y, 0),
                PhysicalVelocity = manager.GetComponentData<PhysicsVelocity>(entity).Linear,
                MeasuredVelocity = Elapsed > 0 ? (Vector3)(position - move.LastObservedPosition) / TimeStep : Vector3.zero,
                TargetVelocity = targetVelocities[index], Destination = destination, Waypoint = waypoint,
                HasWaypoint = hasWaypoint, WaypointIndex = waypointIndex, PathLength = cells.Length,
                StopDistance = nav.StopDistance,
                Avoidance = manager.HasComponent<UnitAvoidanceDebugData>(entity)
                    ? manager.GetComponentData<UnitAvoidanceDebugData>(entity) : default,
                Arrived = math.distancesq(position.xy, new float2(settings.Agents[index].Destination.x,
                    settings.Agents[index].Destination.y)) <= settings.StopDistance * settings.StopDistance,
                PathFound = nav.PathFound != 0, HasDestination = nav.HasDestination != 0,
            };
        }

        public void CopyAvoidanceConstraints(int index, List<UnitAvoidanceDebugConstraint> destination)
        {
            ThrowIfDisposed();
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            Entity entity = agents[index];
            if (!world.EntityManager.HasBuffer<UnitAvoidanceDebugConstraint>(entity)) return;
            var constraints = world.EntityManager.GetBuffer<UnitAvoidanceDebugConstraint>(entity, true);
            for (int i = 0; i < constraints.Length; i++) destination.Add(constraints[i]);
        }

        private void ThrowIfDisposed()
        {
            if (!IsCreated) throw new ObjectDisposedException(nameof(NavigationTestSimulation));
        }

        public void Dispose()
        {
            try
            {
                if (world != null && world.IsCreated)
                {
                    world.EntityManager.CompleteAllTrackedJobs();
                    world.Dispose();
                }
            }
            finally
            {
                world = null;
                foreach (var blob in colliders) if (blob.IsCreated) blob.Dispose();
                colliders.Clear();
            }
        }
    }

    public struct NavigationTestSnapshot
    {
        public Vector3 Position, Velocity;
        public Vector3 PhysicalVelocity, MeasuredVelocity, TargetVelocity, Destination, Waypoint;
        public int WaypointIndex, PathLength;
        public float StopDistance;
        public UnitAvoidanceDebugData Avoidance;
        public bool Arrived, PathFound, HasDestination, HasWaypoint;
    }
}
