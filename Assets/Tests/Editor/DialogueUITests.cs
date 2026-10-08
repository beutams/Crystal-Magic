using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class DialogueUITests
{
    private const string PrefabPath = "Assets/Res/UI/DialogueUI.prefab";

    [Test]
    public void LineCompletesOnlyAfterItsLastCharacter()
    {
        var playback = new DialoguePlayback("你好，冒险者！", 10f);
        Assert.That(playback.IsCompleted, Is.False);
        Assert.That(playback.VisibleCharacters, Is.Zero);
        playback.Advance(0.25f);
        Assert.That(playback.VisibleCharacters, Is.EqualTo(2));
        playback.Advance(0.44f);
        Assert.That(playback.VisibleCharacters, Is.EqualTo(6));
        Assert.That(playback.IsCompleted, Is.False);
        playback.Advance(0.01f);
        Assert.That(playback.VisibleCharacters, Is.EqualTo(7));
        Assert.That(playback.IsCompleted, Is.True);
    }

    [Test]
    public void CombiningMarksAndSurrogatePairsAreNotSplit()
    {
        var playback = new DialoguePlayback("A\U0001f600e\u0301中", 1f);
        playback.Advance(2f);
        Assert.That(playback.RevealedElements, Is.EqualTo(2));
        Assert.That(playback.VisibleCharacters, Is.EqualTo(2));
        playback.Advance(1f);
        Assert.That(playback.VisibleCharacters, Is.EqualTo(4));
        Assert.That(playback.IsCompleted, Is.False);
        playback.Advance(1f);
        Assert.That(playback.IsCompleted, Is.True);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidSpeedCannotDeadlockAnInteraction(float speed)
    {
        var playback = new DialoguePlayback("文字", speed);
        playback.Advance(10f);
        Assert.That(playback.IsCompleted, Is.True);
    }

    [Test]
    public void CancellationIsNotSuccessfulCompletion()
    {
        var playback = new DialoguePlayback("尚未完成", 10f);
        playback.Advance(0.1f);
        playback.Cancel();
        playback.Advance(999f);
        Assert.That(playback.IsCancelled, Is.True);
        Assert.That(playback.IsCompleted, Is.False);
        Assert.That(playback.VisibleCharacters, Is.EqualTo(1));
    }

    [Test]
    public void EmptyTextCompletesAndNewlinesAreNormalized()
    {
        Assert.That(new DialoguePlayback(null, 20f).IsCompleted, Is.True);
        Assert.That(new DialoguePlayback("", 20f).IsCompleted, Is.True);
        Assert.That(new DialoguePlayback("甲\r\n乙\r丙", 20f).Text, Is.EqualTo("甲\n乙\n丙"));
    }

    [Test]
    public void ServerRunnerWaitsAndSessionKeepsItsOwningWorld()
    {
        using var world = new World("Dialogue server test");
        Entity context = world.EntityManager.CreateEntity(typeof(GameWorldContextComponent));
        world.EntityManager.SetComponentData(context, new GameWorldContextComponent { Role = GameWorldRole.Server });
        var node = new NPCDialogueInteractionNodeData { ContentKey = "1234", CharactersPerSecond = 10f };
        var session = new NPCInteractionSession(Entity.Null, null, null, world: world);
        var runner = new NPCDialogueInteractionNodeRunner(node);
        session.CurrentRunner = runner;
        runner.Enter(session);
        Assert.That(session.World, Is.SameAs(world));
        Assert.That(runner.IsCompleted(session), Is.False);
        runner.Update(session, 0.39f);
        Assert.That(runner.IsCompleted(session), Is.False);
        runner.Update(session, 0.01f);
        Assert.That(runner.IsCompleted(session), Is.False, "Finishing typing must not skip the reading wait.");
        runner.Update(session, 1.19f);
        Assert.That(runner.IsCompleted(session), Is.False);
        runner.Update(session, 0.01f);
        Assert.That(runner.IsCompleted(session), Is.True);
        session.Cancel();
        Assert.That(session.IsActive, Is.False);
        Assert.That(runner.IsCompleted(session), Is.False);
    }

    [Test]
    public void ExistingDialogueJsonGetsDefaultsAndCustomFieldsRoundTrip()
    {
        NPCDialogueInteractionNodeData defaults = (NPCDialogueInteractionNodeData)JsonConvert.DeserializeObject<NPCInteractionNodeData>(
            "{\"Type\":\"Dialogue\",\"ContentKey\":\"你好\"}");
        Assert.That(defaults.CharactersPerSecond, Is.EqualTo(20f));
        Assert.That(defaults.ExecutionTargets, Is.EqualTo(GameWorldExecutionTarget.All));
        Assert.That(defaults.LingerSeconds, Is.EqualTo(1.2f));
        Assert.That(defaults.LockCamera, Is.True);
        Assert.That(defaults.CameraFollowSmooth, Is.EqualTo(8f));
        defaults.SpeakerAnchor = NPCDialogueAnchor.Actor;
        defaults.CharactersPerSecond = 35f;
        defaults.WorldYOffset = 2f;
        defaults.LingerSeconds = 3f;
        defaults.LockCamera = false;
        defaults.CameraFollowSmooth = 5f;
        var restored = (NPCDialogueInteractionNodeData)JsonConvert.DeserializeObject<NPCInteractionNodeData>(JsonConvert.SerializeObject(defaults));
        Assert.That(restored.SpeakerAnchor, Is.EqualTo(NPCDialogueAnchor.Actor));
        Assert.That(restored.CharactersPerSecond, Is.EqualTo(35f));
        Assert.That(restored.WorldYOffset, Is.EqualTo(2f));
        Assert.That(restored.LingerSeconds, Is.EqualTo(3f));
        Assert.That(restored.LockCamera, Is.False);
        Assert.That(restored.CameraFollowSmooth, Is.EqualTo(5f));
    }

    [Test]
    public void FullTextRemainsVisibleUntilReadingWaitCompletes()
    {
        var playback = new DialoguePlayback("你好", 10f, 1.2f);
        playback.Advance(0.2f);
        Assert.That(playback.IsTypingCompleted, Is.True);
        Assert.That(playback.IsCompleted, Is.False);
        playback.Advance(0f); // Pause does not advance the wait.
        playback.Advance(1.19f);
        Assert.That(playback.VisibleCharacters, Is.EqualTo(2));
        Assert.That(playback.IsCompleted, Is.False);
        playback.Advance(0.01f);
        Assert.That(playback.IsCompleted, Is.True);
    }

    [Test]
    public void ASlowTypingFrameCannotConsumeTheReadingWaitBeforeTextIsRendered()
    {
        var playback = new DialoguePlayback("短句", 20f, 1f);
        playback.Advance(100f);
        Assert.That(playback.IsTypingCompleted, Is.True);
        Assert.That(playback.IsCompleted, Is.False);
        playback.Advance(1f);
        Assert.That(playback.IsCompleted, Is.True);
        Assert.That(new DialoguePlayback("", 20f, 10f).IsCompleted, Is.True);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidWaitCannotDeadlockAnInteraction(float wait)
    {
        var playback = new DialoguePlayback("字", 20f, wait);
        playback.Advance(1f);
        playback.Advance(100f);
        Assert.That(playback.IsCompleted, Is.True);
    }

    [Test]
    public void CameraTracksTheExplicitWorldAndReturnsToStaticCameraPosition()
    {
        using var world = new World("Dialogue camera target world");
        using var otherWorld = new World("Unrelated default world");
        Entity target = CameraTarget(world, new Vector3(10, 2, 0));
        Entity unrelated = CameraTarget(otherWorld, new Vector3(100, 100, 0));
        // Entity handles may differ across worlds after allocator reuse. The position
        // assertions below verify explicit-world resolution without relying on that layout.
        using var camera = new CameraScope();
        World.DefaultGameObjectInjectionWorld = otherWorld;
        using IDisposable lease = camera.Component.AcquireFollowTarget(world, target, 0f);
        camera.Tick();
        Assert.That(camera.Position, Is.EqualTo(new Vector3(10, 2, -10)));
        SetCameraTarget(world, target, new Vector3(12, 3, 0));
        camera.Tick();
        Assert.That(camera.Position, Is.EqualTo(new Vector3(12, 3, -10)));
        lease.Dispose();
        camera.Tick();
        Assert.That(camera.Position, Is.EqualTo(new Vector3(0, 0, -10)));
    }

    [Test]
    public void CameraResumesCurrentPlayerPositionAndUsesPresentedPosition()
    {
        using var world = new World("Dialogue player follow");
        Entity player = CameraTarget(world, new Vector3(1, 0, 0));
        Entity npc = CameraTarget(world, new Vector3(20, 0, 0));
        world.EntityManager.AddComponent<NetworkPlayerComponent>(player);
        world.EntityManager.AddComponentData(player, new ClientPlayerMovePresentationComponent
            { Initialized = 1, CurrentPosition = new float3(3, 0, 0) });
        using var camera = new CameraScope();
        camera.FollowPlayer(world, player);
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(3));
        using IDisposable lease = camera.Component.AcquireFollowTarget(world, npc, 0f);
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(20));
        world.EntityManager.SetComponentData(player, new ClientPlayerMovePresentationComponent
            { Initialized = 1, CurrentPosition = new float3(4, 0, 0) });
        lease.Dispose();
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(4), "Restore live player follow, not the old camera snapshot.");
        Assert.That(camera.Scene.FollowPlayerTag, Is.True);
    }

    [Test]
    public void ConsecutiveLinesDoNotMoveTheCameraBetweenReleaseAndAcquire()
    {
        using var world = new World("Dialogue handoff");
        Entity first = CameraTarget(world, new Vector3(10, 0, 0));
        Entity second = CameraTarget(world, new Vector3(20, 0, 0));
        using var camera = new CameraScope();
        IDisposable previous = camera.Component.AcquireFollowTarget(world, first, 0f);
        camera.Tick();
        previous.Dispose();
        using IDisposable next = camera.Component.AcquireFollowTarget(world, second, 8f);
        Assert.That(camera.Position.x, Is.EqualTo(10));
        previous.Dispose(); // Delayed cleanup must not release the new line.
        camera.Tick(0.1f);
        Assert.That(camera.Position.x, Is.InRange(10.01f, 19.99f));
        next.Dispose();
        camera.Tick(10f);
        Assert.That(camera.Position.x, Is.EqualTo(0).Within(0.0001f));
    }

    [Test]
    public void ReturningToAMovingPlayerRestoresOriginalFollowSpeedInFiniteTime()
    {
        using var world = new World("Dialogue moving player return");
        Entity player = CameraTarget(world, Vector3.zero);
        world.EntityManager.AddComponent<NetworkPlayerComponent>(player);
        Entity npc = CameraTarget(world, new Vector3(20, 0, 0));
        using var camera = new CameraScope();
        camera.FollowPlayer(world, player);
        IDisposable lease = camera.Component.AcquireFollowTarget(world, npc, 8f);
        camera.Tick(10f);
        lease.Dispose();
        for (int frame = 1; frame <= 6; frame++)
        {
            SetCameraTarget(world, player, new Vector3(frame, 0, 0));
            camera.Tick(0.1f);
            if (frame == 1)
                Assert.That(camera.Position.x, Is.InRange(1.01f, 19.99f), "Return should blend, not snap.");
        }
        Assert.That(camera.Position.x, Is.EqualTo(6), "Original instant follow must resume even if the player never stops.");
    }

    [Test]
    public void ReleasingAnOlderCameraOwnerCannotReleaseTheNewSpeaker()
    {
        using var world = new World("Dialogue overlapping owners");
        Entity first = CameraTarget(world, new Vector3(10, 0, 0));
        Entity second = CameraTarget(world, new Vector3(20, 0, 0));
        using var camera = new CameraScope();
        IDisposable previous = camera.Component.AcquireFollowTarget(world, first, 0f);
        using IDisposable next = camera.Component.AcquireFollowTarget(world, second, 0f);
        previous.Dispose();
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(20));
        next.Dispose();
        camera.Tick();
        Assert.That(camera.Position.x, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DestroyedSpeakerAutomaticallyReleasesCamera(bool pendingDestroy)
    {
        using var world = new World("Dialogue destroyed target");
        Entity target = CameraTarget(world, new Vector3(10, 0, 0));
        using var camera = new CameraScope();
        using IDisposable lease = camera.Component.AcquireFollowTarget(world, target, 0f);
        camera.Tick();
        if (pendingDestroy)
            world.EntityManager.AddComponent<DestroyEntityFlag>(target);
        else
            world.EntityManager.DestroyEntity(target);
        camera.Tick();
        Assert.That(camera.Position.x, Is.Zero);
    }

    [Test]
    public void DisposedWorldAutomaticallyReleasesCamera()
    {
        var world = new World("Dialogue disposed world");
        using var camera = new CameraScope();
        try
        {
            Entity target = CameraTarget(world, new Vector3(10, 0, 0));
            using IDisposable lease = camera.Component.AcquireFollowTarget(world, target, 0f);
            camera.Tick();
            world.Dispose();
            camera.Tick();
            Assert.That(camera.Position.x, Is.Zero);
        }
        finally { if (world.IsCreated) world.Dispose(); }
    }

    [Test]
    public void CameraLockStillRespectsMapBounds()
    {
        using var world = new World("Dialogue bounds");
        Entity target = CameraTarget(world, new Vector3(100, 100, 0));
        using var camera = new CameraScope();
        camera.Component.SetWorldBounds(1, new Rect(-10, -10, 20, 20));
        using IDisposable lease = camera.Component.AcquireFollowTarget(world, target, 0f);
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(8).Within(0.001f));
        Assert.That(camera.Position.y, Is.EqualTo(8).Within(0.001f));
    }

    [Test]
    public void ClosingDuringReadingWaitCancelsTheNodeAndReleasesCameraExactlyOnce()
    {
        using var world = new World("Dialogue close during wait");
        Entity target = CameraTarget(world, new Vector3(10, 0, 0));
        using var camera = new CameraScope();
        using IDisposable lease = camera.Component.AcquireFollowTarget(world, target, 0f);
        var playback = new DialoguePlayback("台词", 20f, 2f);
        playback.Advance(1f);
        int closed = 0;
        var model = new DialogueUIModel();
        model.SetOpenData(new DialogueUIOpenData
        {
            Playback = playback,
            OnClosed = () => { closed++; lease.Dispose(); },
        });
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(10));
        model.ClosePresentation(); // Same path as UI OnClose / scene cleanup.
        model.Dispose();
        camera.Tick();
        Assert.That(playback.IsCancelled, Is.True);
        Assert.That(playback.IsCompleted, Is.False);
        Assert.That(closed, Is.EqualTo(1));
        Assert.That(camera.Position.x, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RunnerReleasesOwnedCameraOnBothExitAndCancellation(bool cancel)
    {
        using var world = new World("Dialogue runner release");
        Entity target = CameraTarget(world, new Vector3(10, 0, 0));
        using var camera = new CameraScope();
        var runner = new NPCDialogueInteractionNodeRunner(new NPCDialogueInteractionNodeData());
        var playback = new DialoguePlayback("台词", 20f, 1f);
        typeof(NPCDialogueInteractionNodeRunner).GetField("_playback", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, playback);
        typeof(NPCDialogueInteractionNodeRunner).GetField("_cameraLock", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(runner, camera.Component.AcquireFollowTarget(world, target, 0f));
        var session = new NPCInteractionSession(target, null, null, world: world) { CurrentRunner = runner };
        camera.Tick();
        if (cancel) session.Cancel();
        else runner.Exit(session);
        camera.Tick();
        Assert.That(camera.Position.x, Is.Zero);
        Assert.That(playback.IsCancelled, Is.EqualTo(cancel));
    }

    [Test]
    public void CameraSceneChangeInvalidatesOldLeasesWithoutAffectingNewOnes()
    {
        using var world = new World("Dialogue scene camera change");
        Entity target = CameraTarget(world, new Vector3(10, 0, 0));
        using var camera = new CameraScope();
        IDisposable old = camera.Component.AcquireFollowTarget(world, target, 0f);
        camera.Tick();
        camera.Component.Unregister(camera.Scene);
        camera.Position = new Vector3(30, 0, -10);
        camera.Component.Register(camera.Scene);
        using IDisposable current = camera.Component.AcquireFollowTarget(world, target, 0f);
        old.Dispose();
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(10));
        current.Dispose();
        camera.Tick();
        Assert.That(camera.Position.x, Is.EqualTo(30));
    }

    private static Entity CameraTarget(World world, Vector3 position)
    {
        Entity target = world.EntityManager.CreateEntity(typeof(LocalToWorld));
        SetCameraTarget(world, target, position);
        return target;
    }

    private static void SetCameraTarget(World world, Entity target, Vector3 position)
        => world.EntityManager.SetComponentData(target, new LocalToWorld { Value = float4x4.Translate(position) });

    private sealed class CameraScope : IDisposable
    {
        private readonly FieldInfo _instance = typeof(Singleton<CameraComponent>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object _previous;
        private readonly World _previousWorld = World.DefaultGameObjectInjectionWorld;
        private readonly GameObject _root;
        private readonly Camera _camera;
        public readonly CameraComponent Component;
        public readonly SceneCamera Scene;
        public Vector3 Position { get => _camera.transform.position; set => _camera.transform.position = value; }

        public CameraScope()
        {
            _previous = _instance.GetValue(null);
            _root = new GameObject("Dialogue camera test scope");
            _root.SetActive(false);
            Component = _root.AddComponent<CameraComponent>();
            _instance.SetValue(null, Component);
            _camera = _root.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 2f;
            _camera.aspect = 1f;
            Position = new Vector3(0, 0, -10);
            Scene = _root.AddComponent<SceneCamera>();
            typeof(SceneCamera).GetField("_registerOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Scene, false);
            typeof(SceneCamera).GetField("_followPlayerTag", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Scene, false);
            typeof(SceneCamera).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Scene, null);
            Component.Register(Scene);
        }

        public void FollowPlayer(World world, Entity player)
        {
            World.DefaultGameObjectInjectionWorld = world;
            typeof(SceneCamera).GetField("_followPlayerTag", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Scene, true);
            typeof(CameraComponent).GetField("_followQueryWorld", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Component, world);
            typeof(CameraComponent).GetField("_followTargetEntity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Component, player);
        }

        public void Tick(float dt = 1f) => typeof(CameraComponent).GetMethod("ApplyFollow", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Component, new object[] { _camera, dt });

        public void Dispose()
        {
            Component.Cleanup();
            Object.DestroyImmediate(_root);
            _instance.SetValue(null, _previous);
            World.DefaultGameObjectInjectionWorld = _previousWorld;
        }
    }

    [Test]
    public void PrefabAndBindingsHaveNoMissingScriptsOrRaycastBlockers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<DialogueUI>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<DialogueUI>().CanCloseByEscape, Is.False);
        Assert.That(prefab.GetComponentsInChildren<Component>(true).All(component => component != null), Is.True);
        var data = new DialogueUIData();
        data.Bind(prefab.transform);
        Assert.That(data.Bubble_Label.TextMeshProUGUI.font, Is.Not.Null);
        Assert.That(data.Bubble.GameObject.GetComponent<DialogueBubbleGraphic>(), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
        Assert.That(File.ReadAllText("Assets/Res/Config/ui_config.json"), Does.Contain("\"DialogueUI\""));
    }

    [Test]
    public void LayoutDoesNotJumpDuringTypingAndCompletionKeepsTextVisible()
    {
        using var events = new EventScope();
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var view = instance.GetComponent<DialogueUI>();
        var model = new DialogueUIModel();
        try
        {
            model.SetOpenData(new DialogueUIOpenData { Speaker = "商人", Playback = new DialoguePlayback("这里有新的法术。准备好了，就去训练场试试吧！", 10f) });
            view.EnsureInitialized();
            view.BindModel(model);
            view.OnOpen();
            model.SetAnchor(new Vector2(40, 50), true);
            var data = new DialogueUIData();
            data.Bind(instance.transform);
            Vector2 originalSize = data.Bubble.RectTransform.sizeDelta;
            model.Advance(0.2f);
            Assert.That(data.Bubble_Label.TextMeshProUGUI.maxVisibleCharacters, Is.EqualTo(5));
            Assert.That(data.Bubble.RectTransform.sizeDelta, Is.EqualTo(originalSize));
            model.Advance(10f);
            Assert.That(model.Playback.IsCompleted, Is.True);
            Assert.That(data.Bubble.GameObject.activeSelf, Is.True);
            Assert.That(data.Bubble.RectTransform.sizeDelta, Is.EqualTo(originalSize));
            Assert.That(data.Bubble.RectTransform.anchoredPosition, Is.EqualTo(new Vector2(40f, 74f)));
        }
        finally
        {
            view.OnClose();
            Object.DestroyImmediate(instance);
            model.Dispose();
        }
    }

    [Test]
    public void ClosingUnfinishedUIOnlyCancelsItsOwnLine()
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var model = new DialogueUIModel();
        var playback = new DialoguePlayback("未完成", 20f);
        model.SetOpenData(new DialogueUIOpenData { Playback = playback });
        var controller = new DialogueUIController(instance.GetComponent<DialogueUI>(), model);
        try
        {
            typeof(DialogueUIController).GetMethod("OnClose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            Assert.That(playback.IsCancelled, Is.True);
            Assert.That(playback.IsCompleted, Is.False);
        }
        finally { Object.DestroyImmediate(instance); controller.Dispose(); model.Dispose(); }
    }

    [TestCase(160f, 72f)]
    [TestCase(468f, 220f)]
    public void BubbleMeshIsFiniteAndIncludesTail(float width, float height)
    {
        var root = new GameObject("Dialogue graphic test", typeof(RectTransform), typeof(CanvasRenderer), typeof(DialogueBubbleGraphic));
        try
        {
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
            using var mesh = new VertexHelper();
            typeof(DialogueBubbleGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null)
                .Invoke(root.GetComponent<DialogueBubbleGraphic>(), new object[] { mesh });
            var vertices = new List<UIVertex>();
            mesh.GetUIVertexStream(vertices);
            Assert.That(vertices.Count, Is.GreaterThan(100));
            Assert.That(vertices.All(v => !float.IsNaN(v.position.x) && !float.IsNaN(v.position.y)), Is.True);
            Assert.That(vertices.Min(v => v.position.y), Is.LessThan(-height * 0.5f - 15f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ANewLineReplacesOnlyTheSameWorldAndCharacter()
    {
        using var world = new World("Dialogue replacement");
        using var otherWorld = new World("Unrelated dialogue");
        Entity actor = world.EntityManager.CreateEntity();
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var model = new DialogueUIModel();
        model.SetOpenData(new DialogueUIOpenData { World = world, Anchor = actor, Playback = new DialoguePlayback("旧对话", 20f) });
        var controller = new DialogueUIController(instance.GetComponent<DialogueUI>(), model);
        var another = new DialogueUIModel();
        MethodInfo opened = typeof(DialogueUIController).GetMethod("OnAnotherDialogueOpened", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo closing = typeof(DialogueUIController).GetField("_closeRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            another.SetOpenData(new DialogueUIOpenData { World = otherWorld, Anchor = actor, Playback = new DialoguePlayback("无关对话", 20f) });
            opened.Invoke(controller, new object[] { new CommonGameEvent("DialogueUI.Opened", another) });
            Assert.That((bool)closing.GetValue(controller), Is.False);
            another.SetOpenData(new DialogueUIOpenData { World = world, Anchor = actor, Playback = new DialoguePlayback("新对话", 20f) });
            opened.Invoke(controller, new object[] { new CommonGameEvent("DialogueUI.Opened", another) });
            Assert.That((bool)closing.GetValue(controller), Is.True);
        }
        finally { Object.DestroyImmediate(instance); controller.Dispose(); model.Dispose(); another.Dispose(); }
    }

    public static void RenderPreview(string path)
    {
        using var events = new EventScope();
        var utility = new PreviewRenderUtility(true, true);
        var model = new DialogueUIModel();
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var view = instance.GetComponent<DialogueUI>();
        try
        {
            utility.AddSingleGO(instance);
            model.SetOpenData(new DialogueUIOpenData { Playback = new DialoguePlayback("这里有新的法术。\n准备好了，就去训练场试试吧！", 20f) });
            view.EnsureInitialized();
            view.BindModel(model);
            view.OnOpen();
            model.SetAnchor(new Vector2(0, -90f), true);
            model.Advance(10f);
            Canvas.ForceUpdateCanvases();
            instance.GetComponentInChildren<TMP_Text>(true).ForceMeshUpdate();
            utility.camera.orthographic = true;
            utility.camera.orthographicSize = 180f;
            utility.camera.transform.position = new Vector3(0, 0, -1000f);
            utility.camera.transform.rotation = Quaternion.identity;
            utility.camera.nearClipPlane = 1f;
            utility.camera.farClipPlane = 2000f;
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = new Color(0.25f, 0.35f, 0.29f);
            utility.BeginStaticPreview(new Rect(0, 0, 720, 360));
            utility.camera.cameraType = CameraType.Game;
            Canvas previewCanvas = instance.GetComponent<Canvas>();
            previewCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            previewCanvas.worldCamera = utility.camera;
            previewCanvas.planeDistance = 100f;
            Canvas.ForceUpdateCanvases();
            utility.camera.Render();
            Texture2D image = utility.EndStaticPreview();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            view.OnClose();
            utility.Cleanup();
            model.Dispose();
        }
    }

    private sealed class EventScope : IDisposable
    {
        private readonly FieldInfo _field = typeof(Singleton<EventComponent>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object _previous;
        private readonly GameObject _root;

        public EventScope()
        {
            _previous = _field.GetValue(null);
            _root = new GameObject("Dialogue test event scope");
            _root.SetActive(false);
            _field.SetValue(null, _root.AddComponent<EventComponent>());
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_root);
            _field.SetValue(null, _previous);
        }
    }
}
