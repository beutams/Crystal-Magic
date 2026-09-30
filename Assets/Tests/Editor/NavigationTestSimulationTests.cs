using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Game.Testing;
using CrystalMagic.Game.Testing.Editor;
using CrystalMagic.ThirdParty.RVO2;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class NavigationTestSimulationTests
{
    private static NavigationTestSettings Simple(bool aStar = true, bool orca = true) => new()
    {
        Width = 16, Height = 12, UseAStar = aStar, UseOrca = orca,
        Agents = new List<NavigationTestAgent>
        {
            new() { Name = "A", Start = new Vector2(-5.5f, 0.5f), Destination = new Vector2(5.5f, 0.5f) },
            new() { Name = "B", Start = new Vector2(5.5f, 2.5f), Destination = new Vector2(-5.5f, 2.5f) },
        },
    };

    [Test]
    public void DefaultScenarioIsValid()
    {
        Assert.DoesNotThrow(() => NavigationTestSimulation.ValidateSettings(NavigationTestSettings.CreateDefault()));
    }

    [Test]
    public void SavedSceneLoadsWithTheCrossingLayout()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/NavigationTest.unity");
        try
        {
            var scenario = scene.GetRootGameObjects().Select(root => root.GetComponent<NavigationTestScenario>())
                .Single(value => value != null);
            Assert.That(scenario.Settings.Agents.Count, Is.EqualTo(8));
            Assert.That(scenario.Settings.Obstacles.Count, Is.EqualTo(2));
            Assert.That(scenario.Settings.Obstacles[0], Is.EqualTo(new RectInt(15, 3, 2, 7)));
            Assert.That(scenario.Settings.Obstacles[1], Is.EqualTo(new RectInt(15, 14, 2, 7)));
            NavigationTestSimulation.ValidateSettings(scenario.Settings);
            var snapshot = JsonUtility.FromJson<NavigationTestSettings>(JsonUtility.ToJson(scenario.Settings));
            Assert.That(snapshot.Obstacles, Is.EqualTo(scenario.Settings.Obstacles));
            NavigationTestSimulation.ValidateSettings(snapshot);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(15, 3, 0, 7)]
    [TestCase(15, 3, 2, 0)]
    [TestCase(-1, 3, 2, 7)]
    [TestCase(31, 3, 2, 7)]
    [TestCase(15, 23, 2, 7)]
    [TestCase(int.MaxValue, 3, 2, 7)]
    public void InvalidObstacleReportsItsIndexAndValuesWithoutCreatingAWorld(int x, int y, int width, int height)
    {
        var settings = NavigationTestSettings.CreateDefault();
        settings.Obstacles[1] = new RectInt(x, y, width, height);
        int count = World.All.Count;
        var exception = Assert.Throws<ArgumentException>(() => new NavigationTestSimulation(settings));
        Assert.That(exception.Message, Does.Contain("障碍 #2"));
        Assert.That(exception.Message, Does.Contain($"X={x}，Y={y}，宽={width}，高={height}"));
        Assert.That(World.All.Count, Is.EqualTo(count));
    }

    [Test]
    public void InvalidPositionsAreRejectedBeforeCreatingAWorld()
    {
        var settings = Simple();
        settings.Agents[0].Destination = new Vector2(100, 0);
        int count = World.All.Count;
        Assert.Throws<ArgumentException>(() => new NavigationTestSimulation(settings));
        Assert.That(World.All.Count, Is.EqualTo(count));
        settings.Agents[0].Destination = new Vector2(0.5f, 0.5f);
        settings.Obstacles.Add(new RectInt(8, 6, 1, 1));
        Assert.Throws<ArgumentException>(() => new NavigationTestSimulation(settings));
        Assert.That(World.All.Count, Is.EqualTo(count));
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void UnitsWaitThenStartTogetherWithoutChangingDefaultWorld(bool aStar, bool orca)
    {
        World previous = World.DefaultGameObjectInjectionWorld;
        using var simulation = new NavigationTestSimulation(Simple(aStar, orca));
        simulation.Step();
        Assert.That(simulation.Snapshot(0).Position.x, Is.EqualTo(-5.5f));
        Assert.That(simulation.Snapshot(1).Position.x, Is.EqualTo(5.5f));
        Assert.That(simulation.Snapshot(0).HasDestination, Is.False);
        simulation.Start();
        Assert.That(simulation.Snapshot(0).HasDestination, Is.True);
        Assert.That(simulation.Snapshot(1).HasDestination, Is.True);
        for (int i = 0; i < 30; i++) simulation.Step();
        Assert.That(simulation.Snapshot(0).Position.x, Is.GreaterThan(-5.4f));
        Assert.That(simulation.Snapshot(1).Position.x, Is.LessThan(5.4f));
        Assert.That(World.DefaultGameObjectInjectionWorld, Is.SameAs(previous));
    }

    [Test]
    public void AStarFindsAndFollowsADetourAroundTheWall()
    {
        var settings = Simple();
        settings.Agents.RemoveAt(1);
        settings.Obstacles.Add(new RectInt(7, 3, 2, 6));
        using var simulation = new NavigationTestSimulation(settings);
        simulation.Start(); simulation.Step();
        var path = new List<Vector3>();
        Assert.That(simulation.Snapshot(0, path).PathFound, Is.True);
        Assert.That(path.Any(p => Mathf.Abs(p.y) > 3), Is.True);
        for (int i = 0; i < 900 && !simulation.Snapshot(0).Arrived; i++) simulation.Step();
        Assert.That(simulation.Snapshot(0).Arrived, Is.True);
    }

    [Test]
    public void OrcaChangesVelocityBeforeHeadOnPhysicalContact()
    {
        Vector3 Run(bool orca)
        {
            var settings = Simple(false, orca);
            settings.Agents[0].Start = new Vector2(-1.5f, 0.5f);
            settings.Agents[0].Destination = new Vector2(4.5f, 0.5f);
            settings.Agents[1].Start = new Vector2(1.5f, 0.5f);
            settings.Agents[1].Destination = new Vector2(-4.5f, 0.5f);
            using var simulation = new NavigationTestSimulation(settings);
            simulation.Start();
            for (int i = 0; i < 12; i++) simulation.Step();
            return simulation.Snapshot(0).Velocity;
        }
        Assert.That(Vector3.Distance(Run(true), Run(false)), Is.GreaterThan(0.01f));
    }

    [Test]
    public void DisposingAndRestartingDoesNotLeakWorlds()
    {
        int count = World.All.Count;
        for (int i = 0; i < 3; i++)
        {
            var simulation = new NavigationTestSimulation(Simple());
            simulation.Start(); simulation.Step(); simulation.Dispose(); simulation.Dispose();
            Assert.Throws<ObjectDisposedException>(() => simulation.Step());
            Assert.That(World.All.Count, Is.EqualTo(count));
        }
    }

    private static NavigationTestSettings HeadOn()
    {
        var settings = Simple(false);
        settings.Agents[0].Start = new Vector2(-1.5f, 0.5f);
        settings.Agents[0].Destination = new Vector2(4.5f, 0.5f);
        settings.Agents[1].Start = new Vector2(1.5f, 0.5f);
        settings.Agents[1].Destination = new Vector2(-4.5f, 0.5f);
        return settings;
    }

    [Test]
    public void DiagnosticsContainTheActualPrePhysicsSolveAndCurrentPhysicalVelocity()
    {
        using var simulation = new NavigationTestSimulation(HeadOn());
        var lines = new List<UnitAvoidanceDebugConstraint>();
        simulation.CopyAvoidanceConstraints(0, lines);
        Assert.That(lines, Is.Empty);
        Assert.That(simulation.Snapshot(0).Avoidance.HasSample, Is.Zero);
        simulation.Start(); simulation.Step();
        var snapshot = simulation.Snapshot(0);
        var debug = snapshot.Avoidance;
        simulation.CopyAvoidanceConstraints(0, lines);
        Assert.That(debug.HasSample, Is.EqualTo(1));
        Assert.That(debug.ConstraintCount, Is.EqualTo(1));
        Assert.That(lines.Count, Is.EqualTo(1));
        Assert.That(simulation.GetAgentIndex(lines[0].Neighbor), Is.EqualTo(1));
        Assert.That(math.distance(debug.Position, new float2(-1.5f, 0.5f)), Is.LessThan(0.00001f));
        Assert.That(math.distance(lines[0].NeighborPosition, new float2(1.5f, 0.5f)), Is.LessThan(0.00001f));
        Assert.That(lines[0].Distance, Is.EqualTo(3).Within(0.00001f));
        Assert.That(snapshot.TargetVelocity.x, Is.EqualTo(4).Within(0.00001f));
        Assert.That(debug.PreferredVelocity.x, Is.EqualTo(30 * simulation.TimeStep).Within(0.00001f));
        Assert.That(snapshot.Velocity.x, Is.EqualTo(debug.ResolvedVelocity.x).Within(0.00001f));
        Assert.That(snapshot.PhysicalVelocity.x, Is.EqualTo(snapshot.Velocity.x).Within(0.0001f));
        Assert.That(snapshot.MeasuredVelocity.x, Is.EqualTo(snapshot.PhysicalVelocity.x).Within(0.001f));
        Assert.That(lines[0].SignedMargin(debug.ResolvedVelocity), Is.GreaterThanOrEqualTo(-0.0001f));
        simulation.Step();
        var next = simulation.Snapshot(0);
        Assert.That(math.distance(next.Avoidance.Position, new float2(snapshot.Position.x, snapshot.Position.y)), Is.LessThan(0.00001f));
        simulation.CopyAvoidanceConstraints(0, lines);
        Assert.That(lines.Count, Is.EqualTo(1), "Each sample replaces the previous constraints; it must not append stale lines.");
    }

    [Test]
    public void OptionalDiagnosticsDoNotChangeMovement()
    {
        using var captured = new NavigationTestSimulation(HeadOn());
        using var ordinary = new NavigationTestSimulation(HeadOn(), captureAvoidanceDebug: false);
        captured.Start(); ordinary.Start();
        for (int step = 0; step < 40; step++)
        {
            captured.Step(); ordinary.Step();
            for (int i = 0; i < captured.Count; i++)
            {
                var a = captured.Snapshot(i); var b = ordinary.Snapshot(i);
                Assert.That(Vector3.Distance(a.Position, b.Position), Is.LessThan(0.00001f));
                Assert.That(Vector3.Distance(a.Velocity, b.Velocity), Is.LessThan(0.00001f));
                Assert.That(b.Avoidance.HasSample, Is.Zero);
            }
        }
    }

    [TestCase(false, 12)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    public void ConstraintListRespectsOrcaAndNeighborLimits(bool orca, int maximumNeighbors)
    {
        var settings = HeadOn(); settings.UseOrca = orca; settings.MaxNeighbors = maximumNeighbors;
        settings.Agents.Add(new NavigationTestAgent { Name = "C", Start = new Vector2(-1.5f, 2.5f), Destination = new Vector2(4.5f, 2.5f) });
        using var simulation = new NavigationTestSimulation(settings);
        simulation.Start(); simulation.Step();
        var lines = new List<UnitAvoidanceDebugConstraint> { default };
        simulation.CopyAvoidanceConstraints(0, lines);
        int expected = orca ? Mathf.Min(maximumNeighbors, 2) : 0;
        Assert.That(lines.Count, Is.EqualTo(expected));
        Assert.That(simulation.Snapshot(0).Avoidance.HasSample, Is.EqualTo(orca ? 1 : 0));
        if (expected == 1) Assert.That(simulation.GetAgentIndex(lines[0].Neighbor), Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SolverTraceUsesTheSameResultIncludingOverlapFallback(bool overlap)
    {
        var self = new AgentData
        {
            Entity = new Entity { Index = 1, Version = 1 }, Position = float2.zero,
            PreferredVelocity = new float2(4, 0), Radius = 0.4f,
            MaxNeighbors = 12, NeighborDistance = 4, MaxSpeed = 4, TimeHorizon = 0.8f,
        };
        var other = self;
        other.Entity = new Entity { Index = 2, Version = 1 };
        other.Position = overlap ? float2.zero : new float2(3, 0);
        FixedList4096Bytes<AgentNeighbor> neighbors = default;
        float rangeSq = 16;
        OrcaSolver.InsertNeighbor(in self, in other, ref neighbors, ref rangeSq);
        float2 ordinary = OrcaSolver.ComputeNewVelocity(in self, in neighbors, 1f / 60);
        float2 captured = OrcaSolver.ComputeNewVelocity(in self, in neighbors, 1f / 60, out var lines, out int firstFailed);
        Assert.That(math.distance(ordinary, captured), Is.Zero);
        Assert.That(lines.Length, Is.EqualTo(1));
        Assert.That(firstFailed < lines.Length, Is.EqualTo(overlap));
        var line = new UnitAvoidanceDebugConstraint { Point = lines[0].Point, Direction = lines[0].Direction };
        Assert.That(line.SignedMargin(line.Point + line.AllowedNormal), Is.GreaterThan(0));
        Assert.That(line.SignedMargin(line.Point - line.AllowedNormal), Is.LessThan(0));
        Assert.That(math.all(math.isfinite(captured)), Is.True);
        Assert.That(math.length(captured), Is.LessThanOrEqualTo(self.MaxSpeed + 0.0001f));
    }

    [Test]
    public void FeasibleRegionClipsThePermittedSideAndHandlesEmptyIntersections()
    {
        var square = new List<Vector2> { new(-2, -2), new(2, -2), new(2, 2), new(-2, 2) };
        var line = new UnitAvoidanceDebugConstraint { Point = new float2(1, 0), Direction = new float2(0, 1) };
        var output = new List<Vector2>();
        NavigationTestWindow.ClipHalfPlane(square, in line, output);
        Assert.That(output.Count, Is.EqualTo(4));
        Assert.That(output.All(p => p.x <= 1), Is.True);
        Assert.That(output.Max(p => p.x), Is.EqualTo(1));
        Assert.That(output.Min(p => p.x), Is.EqualTo(-2));
        line.Point = new float2(-3, 0);
        NavigationTestWindow.ClipHalfPlane(output, in line, square);
        Assert.That(square, Is.Empty);
    }
}
