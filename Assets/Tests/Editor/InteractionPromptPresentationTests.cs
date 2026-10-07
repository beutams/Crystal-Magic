using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public sealed class InteractionPromptPresentationTests
{
    private static readonly MethodInfo Anchor = typeof(InteractionPromptManager).GetMethod("ResolveAnchor", BindingFlags.Static | BindingFlags.NonPublic);

    [Test]
    public void RenderCallbackUsesCameraPositionAfterTickAndHidesDestroyedTargets()
    {
        using var world = new World("Prompt render timing test");
        World previousWorld = World.DefaultGameObjectInjectionWorld;
        var root = new GameObject("Prompt test canvas", typeof(RectTransform));
        var cameraObject = new GameObject("Prompt test camera", typeof(Camera));
        cameraObject.SetActive(false);
        var sceneCamera = cameraObject.AddComponent<SceneCamera>();
        var camera = cameraObject.GetComponent<Camera>();
        typeof(SceneCamera).GetField("<Camera>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sceneCamera, camera);
        var cameraInstance = typeof(Singleton<CameraComponent>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var eventInstance = typeof(Singleton<EventComponent>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousCameraInstance = cameraInstance.GetValue(null);
        object previousEventInstance = eventInstance.GetValue(null);
        var cameraOwner = new GameObject("Prompt test camera service");
        var eventOwner = new GameObject("Prompt test event service");
        cameraOwner.SetActive(false);
        eventOwner.SetActive(false);
        var cameras = cameraOwner.AddComponent<CameraComponent>();
        cameraInstance.SetValue(null, cameras);
        eventInstance.SetValue(null, eventOwner.AddComponent<EventComponent>());
        FieldInfo currentCamera = typeof(CameraComponent).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousCamera = currentCamera.GetValue(cameras);
        using var prompts = new InteractionPromptManager();
        var model = new InteractionPromptUIModel();
        void Set(string name, object value) => typeof(InteractionPromptManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(prompts, value);
        var refresh = (Canvas.WillRenderCanvases)System.Delegate.CreateDelegate(typeof(Canvas.WillRenderCanvases), prompts,
            typeof(InteractionPromptManager).GetMethod("RefreshPrompt", BindingFlags.Instance | BindingFlags.NonPublic));
        try
        {
            var manager = world.EntityManager;
            Entity context = manager.CreateEntity(typeof(GameWorldContextComponent));
            manager.SetComponentData(context, new GameWorldContextComponent { Role = GameWorldRole.Standalone, SceneMode = GameSceneMode.Town });
            Entity player = manager.CreateEntity(typeof(PlayerInputComponent), typeof(UnitVariableComponent), typeof(UnitFactionComponent));
            manager.SetComponentData(player, new UnitFactionComponent { Value = UnitFactionType.Player });
            manager.AddBuffer<UnitVariableElement>(player);
            manager.AddBuffer<UnitVariableConsumerElement>(player);
            Entity target = manager.CreateEntity(typeof(UnitInteractableComponent), typeof(LocalToWorld));
            var interaction = new UnitInteractionData { Kind = InteractionKind.Treasure };
            manager.SetComponentData(target, new UnitInteractableComponent { Data = interaction, IsEnabled = 1 });
            manager.SetComponentData(target, new LocalToWorld { Value = float4x4.identity });
            UnitVariableSource.TrySetValue(manager, player, "game.interaction.candidates.0", UnitSourceValue.FromEntity(target));
            World.DefaultGameObjectInjectionWorld = world;
            currentCamera.SetValue(cameras, sceneCamera);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0, 0, -10);
            Set("_rootRect", root.transform);
            Set("_model", model);
            Set("_playerWorld", world);
            Set("_cachedPlayer", player);
            Set("_cachedTarget", target);
            Set("_cachedInteraction", interaction);
            Set("_cachedDisplayName", "E 宝箱");
            Set("_hasCachedDisplay", true);
            Set("_view", root.AddComponent<InteractionPromptUI>());
            Set("_initialized", true);
            prompts.Tick();
            Assert.That(model.Visible, Is.False);
            Canvas.preWillRenderCanvases += refresh;
            camera.transform.position = new Vector3(2f, 1f, -10f);
            Canvas.ForceUpdateCanvases();
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)root.transform,
                camera.WorldToScreenPoint(Vector3.zero), null, out Vector2 expected);
            Assert.That(model.Visible, Is.True);
            Assert.That(Vector2.Distance(model.Position, expected), Is.LessThan(0.01f));
            manager.DestroyEntity(target);
            Canvas.ForceUpdateCanvases();
            Assert.That(model.Visible, Is.False);
        }
        finally
        {
            Set("_view", null);
            Canvas.preWillRenderCanvases -= refresh;
            currentCamera.SetValue(cameras, previousCamera);
            World.DefaultGameObjectInjectionWorld = previousWorld;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(cameraOwner);
            Object.DestroyImmediate(eventOwner);
            cameraInstance.SetValue(null, previousCameraInstance);
            eventInstance.SetValue(null, previousEventInstance);
        }
    }

    [TestCase(0.5f)]
    [TestCase(1f)]
    [TestCase(3f)]
    public void AnchorScalesOriginalOffsetWithoutUsingSpriteBounds(float scale)
    {
        var actor = new GameObject("Prompt test actor", typeof(SpriteRenderer));
        var texture = new Texture2D(32, 64);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0f), 32f);
        try
        {
            var renderer = actor.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            actor.transform.position = new Vector3(8f, 12f, -0.1f);
            actor.transform.localScale = new Vector3(scale, scale, 1f);
            var staleTransform = new LocalToWorld { Value = float4x4.identity };
            var actual = (Vector3)Anchor.Invoke(null, new object[] { renderer, staleTransform, 1.2f });
            Assert.That(actual.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(12f + 1.2f * scale).Within(0.001f));

            actor.transform.position += Vector3.right * 2f;
            renderer.flipX = true;
            renderer.sprite = null;
            actual = (Vector3)Anchor.Invoke(null, new object[] { renderer, staleTransform, 1.2f });
            Assert.That(actual.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(12f + 1.2f * scale).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void MissingRendererFallbackRespectsEntityScale()
    {
        var transform = new LocalToWorld { Value = float4x4.TRS(new float3(4f, 8f, 0f), quaternion.identity, new float3(3f)) };
        var actual = (Vector3)Anchor.Invoke(null, new object[] { null, transform, 1.2f });
        Assert.That(actual.y, Is.EqualTo(11.6f).Within(0.001f));
    }

    [Test]
    public void PrefabHasChineseGlyphsContrastingBackgroundAndDoesNotInterceptInput()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents("Assets/Res/UI/InteractionPromptUI.prefab");
        try
        {
            var data = new InteractionPromptUIData();
            data.Bind(contents.transform);
            var label = data.Prompt_Label.TextMeshProUGUI;
            Assert.That(label.font, Is.Not.Null);
            Assert.That(label.font.HasCharacters("E交互训练商人物品拾取宝箱金币", out uint[] missing), Is.True,
                missing == null ? string.Empty : string.Join(",", missing));
            Assert.That(data.Prompt.Image.color.a, Is.GreaterThanOrEqualTo(0.85f));
            Assert.That(data.Prompt.Image.color.grayscale, Is.LessThan(0.2f));
            Assert.That(label.color.grayscale, Is.GreaterThan(0.8f));
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(data.Prompt.Image.raycastTarget, Is.False);
            Assert.That(contents.GetComponent<InteractionPromptUI>().CanCloseByEscape, Is.False);
            Assert.That(data.Prompt.RectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    [Test]
    public void LongNamesGrowTheBackgroundAndHideTogetherWithIt()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents("Assets/Res/UI/InteractionPromptUI.prefab");
        try
        {
            var view = contents.GetComponent<InteractionPromptUI>();
            view.EnsureInitialized();
            var model = new InteractionPromptUIModel();
            view.BindModel(model);
            var data = new InteractionPromptUIData();
            data.Bind(contents.transform);
            void Render(string text, Vector2 position, bool visible)
            {
                typeof(InteractionPromptUIModel).GetField("<Text>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, text);
                typeof(InteractionPromptUIModel).GetField("<Position>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, position);
                typeof(InteractionPromptUIModel).GetField("<Visible>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, visible);
                typeof(InteractionPromptUI).GetMethod("RefreshView", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            }
            Render("E 商人", new Vector2(100f, 120f), true);
            float shortWidth = data.Prompt.RectTransform.sizeDelta.x;
            Render("E 训练场物品商人", new Vector2(140f, 160f), true);
            Assert.That(data.Prompt.RectTransform.sizeDelta.x, Is.GreaterThan(shortWidth));
            Assert.That(data.Prompt.RectTransform.anchoredPosition.x, Is.EqualTo(140f));
            Assert.That(data.Prompt.RectTransform.anchoredPosition.y, Is.EqualTo(160f));
            Assert.That(data.Prompt.RectTransform.sizeDelta.x, Is.GreaterThan(data.Prompt_Label.TextMeshProUGUI.preferredWidth));
            Render("E 训练场物品商人", Vector2.zero, false);
            Assert.That(data.Prompt.GameObject.activeSelf, Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
}
