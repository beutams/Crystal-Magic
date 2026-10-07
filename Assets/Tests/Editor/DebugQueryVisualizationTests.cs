using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class DebugQueryVisualizationTests
{
    private GameObject _root;
    private DebugComponent _debug;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Query debug test");
        _debug = _root.AddComponent<DebugComponent>();
        _debug.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        _debug.Cleanup();
        Object.DestroyImmediate(_root);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void BothSwitchesMustBeEnabledToCollectShapesAndHitLines(bool enabled, bool draw)
    {
        _debug.SetEnabled(enabled);
        Field("_drawHitMarkers").SetValue(_debug, draw);
        Report();
        Assert.That(_debug.DrawQueryVisualization, Is.EqualTo(enabled && draw));
        Assert.That(Count("_activeShapes"), Is.EqualTo(enabled && draw ? 1 : 0));
        Assert.That(Count("_activeLines"), Is.EqualTo(enabled && draw ? 3 : 0));
    }

    [Test]
    public void InspectorDrawToggleClearsExistingShapesAndLinesAndCanResume()
    {
        Report();
        Assert.That(Count("_activeLines"), Is.EqualTo(3));
        Field("_drawHitMarkers").SetValue(_debug, false);
        Tick();
        Report();
        Assert.That(_debug.IsEnabled, Is.True);
        Assert.That(Count("_activeShapes"), Is.Zero);
        Assert.That(Count("_activeLines"), Is.Zero);
        Field("_drawHitMarkers").SetValue(_debug, true);
        Tick();
        Report();
        Assert.That(Count("_activeShapes"), Is.EqualTo(1));
        Assert.That(Count("_activeLines"), Is.EqualTo(3));
    }

    [Test]
    public void GlobalSwitchImmediatelyClearsExistingDrawing()
    {
        Report();
        _debug.SetEnabled(false);
        Assert.That(Count("_activeShapes"), Is.Zero);
        Assert.That(Count("_activeLines"), Is.Zero);
    }

    [Test]
    public void DisabledObjectCannotCaptureOrRetainDrawing()
    {
        Report();
        _root.SetActive(false);
        Tick();
        Report();
        Assert.That(_debug.DrawQueryVisualization, Is.False);
        Assert.That(Count("_activeShapes"), Is.Zero);
        Assert.That(Count("_activeLines"), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ManagedQueriesOnlyReportWhenDrawingIsEnabled(bool draw)
    {
        Field("_drawHitMarkers").SetValue(_debug, draw);
        int shapes = 0, hits = 0;
        void OnShape(DebugQueryShapeRequest shape) => shapes++;
        void OnHit(float3 origin, float3 hit) => hits++;
        DebugQueryShapeReporter.ShapeReported += OnShape;
        DebugQueryShapeReporter.HitReported += OnHit;
        try
        {
            typeof(UnitQueryTree).GetMethod("ReportQuery", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[]
                {
                    UnitQueryShape.Circle(float3.zero, 1f),
                    new List<UnitQueryHit> { new() { Position = new float3(1f, 0f, 0f) } },
                });
            Assert.That(shapes, Is.EqualTo(draw ? 1 : 0));
            Assert.That(hits, Is.EqualTo(draw ? 1 : 0));
        }
        finally
        {
            DebugQueryShapeReporter.ShapeReported -= OnShape;
            DebugQueryShapeReporter.HitReported -= OnHit;
        }
    }

    private static FieldInfo Field(string name) => typeof(DebugComponent)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private int Count(string name) => ((ICollection)Field(name).GetValue(_debug)).Count;
    private void Tick() => typeof(DebugComponent).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_debug, null);
    private static void Report()
    {
        DebugQueryShapeReporter.ReportCircle(float3.zero, 1f);
        DebugQueryShapeReporter.ReportHit(float3.zero, new float3(1f, 0f, 0f));
    }
}
