using System.Collections.Generic;
using System.Text.RegularExpressions;
using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

public sealed class SceneLoadTimingTests
{
    private readonly List<string> _logs = new();

    [SetUp]
    public void SetUp()
    {
        SceneLoadTiming.Finish("Test reset");
        _logs.Clear();
        Application.logMessageReceived += Capture;
    }

    [TearDown]
    public void TearDown()
    {
        Application.logMessageReceived -= Capture;
        SceneLoadTiming.Finish("Test cleanup");
    }

    private void Capture(string message, string stack, LogType type)
    {
        if (message.StartsWith("[SceneLoadTiming]"))
            _logs.Add(message);
    }

    [Test]
    public void NestedStagesShareFlowIdAndRecordDurations()
    {
        SceneLoadTiming.Begin("TownScene");
        using (SceneLoadTiming.Measure("outer"))
        using (SceneLoadTiming.Measure("inner")) { }
        SceneLoadTiming.Finish();

        string id = Regex.Match(_logs[0], @"^\[SceneLoadTiming\]\[([^\]]+)\]").Groups[1].Value;
        Assert.That(id, Is.Not.Empty);
        Assert.That(_logs.TrueForAll(line => line.Contains($"[{id}]")), Is.True);
        Assert.That(_logs.Exists(line => line.Contains("[END]") && line.Contains("inner Duration=")), Is.True);
        Assert.That(_logs.Exists(line => line.Contains("[END]") && line.Contains("outer Duration=")), Is.True);
        Assert.That(_logs[_logs.Count - 1], Does.Contain("[FLOW COMPLETE]"));
    }

    [Test]
    public void FailureReportsActiveStageAndDoesNotReportSuccess()
    {
        SceneLoadTiming.Begin("TownScene");
        using (SceneLoadTiming.Measure("Wait TownSubScene"))
            SceneLoadTiming.Finish("Timeout");
        Assert.That(_logs.Exists(line => line.Contains("[INTERRUPTED]") && line.Contains("Wait TownSubScene Duration=")), Is.True);
        Assert.That(_logs.Exists(line => line.Contains("[FLOW FAILED]") && line.Contains("Timeout")), Is.True);
        Assert.That(_logs.Exists(line => line.Contains("[FLOW COMPLETE]")), Is.False);
    }

    [Test]
    public void DisposingPreviousFlowStageDoesNotAffectNewFlow()
    {
        SceneLoadTiming.Begin("first");
        SceneLoadTiming.Stage previous = SceneLoadTiming.Measure("previous");
        SceneLoadTiming.Begin("second");
        int count = _logs.Count;
        previous.Dispose();
        previous.Dispose();
        Assert.That(_logs.Count, Is.EqualTo(count));
        using (SceneLoadTiming.Measure("current")) { }
        SceneLoadTiming.Finish();
        Assert.That(_logs.Exists(line => line.Contains("current Duration=")), Is.True);
    }

    [Test]
    public void NoActiveFlowProducesNoTimingLogs()
    {
        using (SceneLoadTiming.Measure("idle")) { }
        SceneLoadTiming.Mark("WAIT", "idle");
        SceneLoadTiming.Finish();
        Assert.That(_logs, Is.Empty);
    }

    [Test]
    public void MissingSceneReportsRegistrationWait()
    {
        using World world = new("Scene diagnostic test");
        Assert.That(SubSceneLoadTiming.DescribeScene(world, Entity.Null), Is.EqualTo("AwaitingSceneRegistration"));
    }

    [Test]
    public void UnrequestedSceneWithoutSectionsDoesNotReadMissingBuffer()
    {
        using World world = new("Scene diagnostic test");
        Entity scene = world.EntityManager.CreateEntity(typeof(SceneReference));
        Assert.That(SubSceneLoadTiming.DescribeScene(world, scene), Is.EqualTo("NotRequested"));
    }

    [Test]
    public void RequestedSceneWithoutSectionsReportsImportAndHeaderWithoutChangingRequest()
    {
        using World world = new("Scene diagnostic test");
        EntityManager manager = world.EntityManager;
        Entity scene = manager.CreateEntity(typeof(SceneReference), typeof(RequestSceneLoaded));
        manager.SetComponentData(scene, new RequestSceneLoaded { LoadFlags = SceneLoadFlags.BlockOnImport });
        Assert.That(SubSceneLoadTiming.DescribeScene(world, scene), Does.Contain("ImportAndHeader Flags=BlockOnImport"));
        Assert.That(manager.GetComponentData<RequestSceneLoaded>(scene).LoadFlags, Is.EqualTo(SceneLoadFlags.BlockOnImport));
        Assert.That(manager.HasBuffer<ResolvedSectionEntity>(scene), Is.False);
    }

    [Test]
    public void EmptyResolvedHeaderReportsFailure()
    {
        using World world = new("Scene diagnostic test");
        EntityManager manager = world.EntityManager;
        Entity scene = manager.CreateEntity(typeof(SceneReference), typeof(RequestSceneLoaded));
        manager.AddBuffer<ResolvedSectionEntity>(scene);
        Assert.That(SubSceneLoadTiming.DescribeScene(world, scene), Does.Contain("FailedLoadingSceneHeader"));
    }

    [Test]
    public void ResolvedHeaderReportsPendingSections()
    {
        using World world = new("Scene diagnostic test");
        EntityManager manager = world.EntityManager;
        Entity section = manager.CreateEntity(typeof(RequestSceneLoaded));
        Entity scene = manager.CreateEntity(typeof(SceneReference), typeof(RequestSceneLoaded));
        manager.AddBuffer<ResolvedSectionEntity>(scene).Add(new ResolvedSectionEntity { SectionEntity = section });
        string description = SubSceneLoadTiming.DescribeScene(world, scene);
        Assert.That(description, Does.Contain("Loading"));
        Assert.That(description, Does.Contain("Sections=1 Loaded=0 Loading=0 Pending=1 Failed=0"));
    }
}
