using System;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.OpenField;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class ProjectileTerrainCollisionTests
{
    [TestCase(0f, .5f, 10f, .5f, .25f, .175f)]
    [TestCase(10f, .5f, 0f, .5f, .25f, .675f)]
    [TestCase(2.5f, -2f, 2.5f, 8f, .25f, .175f)]
    [TestCase(2.5f, 8f, 2.5f, -2f, .25f, .675f)]
    [TestCase(0f, .5f, 10f, .5f, 0f, .2f)]
    [TestCase(2.5f, .5f, 2.5f, .5f, .25f, 0f)]
    [TestCase(1.8f, .5f, 10f, .5f, .25f, 0f)]
    public void SweepFindsFirstContact(float sx, float sy, float ex, float ey, float radius, float expected)
    {
        Assert.That(ProjectileTerrainCollisionUtility.TryHitCell(new float2(sx, sy), new float2(ex, ey), radius,
            new float2(2, 0), new float2(3, 1), out float fraction), Is.True);
        Assert.That(fraction, Is.EqualTo(expected).Within(.00001f));
    }

    [TestCase(-.49f, -.49f, -.49f, -.49f)]
    [TestCase(-2f, 1.51f, 2f, 1.51f)]
    [TestCase(-2f, -1f, -3f, -1f)]
    public void EmptySpaceBesideMountainAndCornersDoesNotBlock(float sx, float sy, float ex, float ey)
    {
        Assert.That(ProjectileTerrainCollisionUtility.TryHitCell(new float2(sx, sy), new float2(ex, ey), .5f,
            float2.zero, new float2(1f), out _), Is.False);
    }

    [Test]
    public void DiagonalContactUsesRoundedCorner()
    {
        Assert.That(ProjectileTerrainCollisionUtility.TryHitCell(new float2(-2f), new float2(2f), .5f,
            float2.zero, new float2(1f), out float fraction), Is.True);
        Assert.That(fraction, Is.EqualTo((2f - .5f / math.sqrt(2f)) / 4f).Within(.00001f));
    }

    [Test]
    public void CircleTargetReturnsEntryBeforeCenterAndRejectsTargetsBeyondMovement()
    {
        Assert.That(ProjectileTerrainCollisionUtility.TryHitCircle(float2.zero, new float2(10, 0), new float2(3, 0), .5f,
            out float fraction), Is.True);
        Assert.That(fraction, Is.EqualTo(.25f).Within(.00001f));
        Assert.That(ProjectileTerrainCollisionUtility.TryHitCircle(float2.zero, new float2(10, 0), new float2(11, 0), .5f,
            out _), Is.False);
    }

    [Test]
    public void LongDiagonalChecksCrossedCellsAndIgnoresOutOfBounds()
    {
        var map = new DungeonNavigationMapComponent { Width = 10, Height = 10, CellSize = 1f };
        using var words = new NativeArray<DungeonNavigationCollisionWord>(new[]
        {
            new DungeonNavigationCollisionWord { ProjectileValue = 1UL << 55 }, default,
        }, Allocator.Temp);
        Assert.That(ProjectileTerrainCollisionUtility.TryCast(in map, words, new float2(-2f), new float2(12f), .5f,
            out float fraction), Is.True);
        Assert.That(fraction, Is.EqualTo((7f - .5f / math.sqrt(2f)) / 14f).Within(.00001f));
        Assert.That(ProjectileTerrainCollisionUtility.TryCast(in map, words, new float2(-10, -5), new float2(20, -5), .5f,
            out _), Is.False);
    }

    [Test]
    public void GroundAndVoidPassWhileMountainBlocksInGeneratedMap()
    {
        var layout = (OpenFieldDungeonLayout)Activator.CreateInstance(typeof(OpenFieldDungeonLayout),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 3, 1, 123 }, null);
        MethodInfo set = typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain", BindingFlags.Instance | BindingFlags.NonPublic);
        var cells = new[] { OpenFieldTerrainCell.Ground, OpenFieldTerrainCell.Void, OpenFieldTerrainCell.Obstacle };
        for (int x = 0; x < 3; x++) set.Invoke(layout, new object[] { x, 0, .5f, cells[x], 1 });
        using var world = new World("Projectile terrain mask");
        EntityManager manager = world.EntityManager;
        Entity mapEntity = manager.CreateEntity();
        typeof(GameRuntimeStateUtility).GetMethod("SetDungeonNavigationMap", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { manager, mapEntity, layout, null });
        var words = manager.GetBuffer<DungeonNavigationCollisionWord>(mapEntity, true).AsNativeArray();
        var map = manager.GetComponentData<DungeonNavigationMapComponent>(mapEntity);
        Assert.That(words[0].Value, Is.EqualTo(6UL));
        Assert.That(words[0].ProjectileValue, Is.EqualTo(4UL));
        Assert.That(ProjectileTerrainCollisionUtility.TryCast(in map, words, new float2(-1, 0), new float2(0, 0), .1f, out _), Is.False);
        Assert.That(ProjectileTerrainCollisionUtility.TryCast(in map, words, new float2(-1, 0), new float2(1, 0), .1f, out float fraction), Is.True);
        Assert.That(fraction, Is.EqualTo(.7f).Within(.00001f));
    }

    [TestCase(false, false, 6f, true, false, true)]
    [TestCase(true, false, 6f, true, false, true)]
    [TestCase(false, true, 1.5f, true, true, true)]
    [TestCase(true, true, 1.5f, true, true, true)]
    [TestCase(false, false, 1.5f, true, true, false)]
    [TestCase(true, false, 6f, false, true, false)]
    [TestCase(false, false, -1f, false, false, false)]
    public void CollisionJobUsesSameTerrainRulesForSimulationAndPrediction(bool client, bool pierce,
        float targetX, bool mountain, bool expectedHit, bool expectedTerrain)
    {
        using var world = new World("Projectile terrain collision", client ? WorldFlags.GameClient : WorldFlags.Game);
        EntityManager manager = world.EntityManager;
        GameSingletonUtility.Create(manager, client ? GameWorldRole.Client : GameWorldRole.Standalone, GameSceneMode.Dungeon);
        Entity terrain = manager.CreateEntity(typeof(DungeonNavigationMapComponent));
        manager.SetComponentData(terrain, new DungeonNavigationMapComponent { Width = 8, Height = 2, CellSize = 1f });
        manager.AddBuffer<DungeonNavigationCollisionWord>(terrain)
            .Add(new DungeonNavigationCollisionWord { Value = 1UL << 3, ProjectileValue = mountain ? 1UL << 3 : 0 });

        Entity target = manager.CreateEntity();
        Entity tree = manager.CreateEntity();
        manager.AddBuffer<UnitQueryNode>(tree).Add(new UnitQueryNode
        {
            Min = float2.zero, Max = new float2(8, 2), FirstChildIndex = -1, Count = targetX < 0 ? 0 : 1,
        });
        var entries = manager.AddBuffer<UnitQueryEntry>(tree);
        if (targetX >= 0) entries.Add(new UnitQueryEntry { Entity = target, Position = new float3(targetX, .5f, 0), Faction = UnitFactionType.Enemy });
        manager.SetComponentData(manager.CreateEntity(typeof(UnitQuerySingleton)), new UnitQuerySingleton { TreeEntity = tree });
        Entity projectile = manager.CreateEntity(typeof(LocalTransform), typeof(SkillProjectileComponent),
            typeof(SkillProjectilePayloadComponent), typeof(SkillProjectileFrameResultComponent));
        manager.SetComponentData(projectile, LocalTransform.FromPosition(new float3(.5f, .5f, 0)));
        manager.SetComponentData(projectile, new SkillProjectileComponent
        {
            Speed = 12f, Direction = new float3(1, 0, 0), MaxRange = 10f, HitRadius = .25f, CanPierce = pierce ? (byte)1 : (byte)0,
        });
        manager.AddBuffer<SkillProjectileHitEntityElement>(projectile);
        manager.AddBuffer<SkillProjectileConditionInstructionElement>(projectile);
        manager.AddBuffer<SkillProjectileConditionLiteralElement>(projectile);
        if (client) manager.AddComponent<ClientPredictedProjectileComponent>(projectile);
        world.SetTime(new TimeData(.5d, .5f));
        var group = world.GetOrCreateSystemManaged<UnitExecutionSystemGroup>();
        group.AddSystemToUpdateList(client ? world.GetOrCreateSystem<ClientPredictedProjectileSystem>() : world.GetOrCreateSystem<SkillProjectileSystem>());
        group.Update();
        manager.CompleteAllTrackedJobs();
        var result = manager.GetComponentData<SkillProjectileFrameResultComponent>(projectile);
        Assert.That(result.HasHit != 0, Is.EqualTo(expectedHit));
        Assert.That(result.HasTerrainHit != 0, Is.EqualTo(expectedTerrain));
        Assert.That(result.ShouldDestroy != 0, Is.EqualTo(expectedTerrain || expectedHit && !pierce));
        if (expectedTerrain)
        {
            Assert.That(result.TerrainHitPosition.x, Is.EqualTo(2.75f).Within(.00001f));
            Assert.That(result.DestroyUsesHitContext, Is.Zero);
        }
    }
}
