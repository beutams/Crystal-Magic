using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Game.OpenField;
using CrystalMagic.UI;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MinimapUIFlagsTests
{
    private const string PrefabPath = "Assets/Res/UI/MinimapUI.prefab";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void PrefabKeepsOriginalMapRectAndUsesOnlyFlagAndPlayerMarkers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var data = new MinimapUIData();
        data.Bind(prefab.transform);
        Assert.That(data.Panel.RectTransform.anchoredPosition, Is.EqualTo(new Vector2(36, 36)));
        Assert.That(data.Panel.RectTransform.sizeDelta, Is.EqualTo(new Vector2(420, 420)));
        Assert.That(data.Terrain.RectTransform.sizeDelta, Is.EqualTo(new Vector2(-16, -16)));
        Assert.That(data.Terrain.Image.preserveAspect, Is.True);
        Assert.That(prefab.transform.Find("Panel/Terrain/Exit"), Is.Null);
        Assert.That(prefab.transform.Find("Panel/Terrain/Fog"), Is.Null);
        Assert.That(data.Terrain.GameObject.transform.childCount, Is.EqualTo(2));
        Assert.That(data.Player.RectTransform.GetSiblingIndex(), Is.GreaterThan(data.InterestPointRoot.RectTransform.GetSiblingIndex()));
        var frame = prefab.transform.Find("Panel/Frame").GetComponent<Image>();
        Assert.That(frame.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(frame.sprite),
            Is.EqualTo("Assets/Res/Sprites/UISprites/BookV1/Content/Titles and Underlying/13.png"));
        Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(frame.fillCenter, Is.False);
        Assert.That(frame.sprite.border, Is.EqualTo(new Vector4(6, 6, 6, 6)));
        Image flag = data.InterestPointTemplate.UI.Icon.Image;
        Assert.That(flag.sprite.name, Is.EqualTo("16x16_14"));
        Assert.That(AssetDatabase.GetAssetPath(flag.sprite),
            Is.EqualTo("Assets/Res/Sprites/UISprites/BookV1/Content/Icons/16x16.png"));
        Assert.That(data.Player.Image.sprite.name, Is.EqualTo("16x16_0"));
        Assert.That(AssetDatabase.GetAssetPath(data.Player.Image.sprite),
            Is.EqualTo("Assets/Res/Sprites/UISprites/BookV1/Content/Inscriptions/Light/16x16.png"));
        Assert.That(flag.material.shader.name, Is.EqualTo("CrystalMagic/UI/MinimapSymbol"));
        Assert.That(ShaderUtil.ShaderHasError(flag.material.shader), Is.False);
        Assert.That(prefab.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), Is.False);
    }

    [Test]
    public void FlagStaysFixedSizeAndSwitchesBackToCyanWhenReused()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var data = new MinimapUIData();
            data.Bind(root.transform);
            var view = data.InterestPointTemplate;
            var rect = (RectTransform)view.transform;
            Sprite flag = view.UI.Icon.Image.sprite;
            view.Render(new Vector2(0.2f, 0.7f), false);
            Color active = view.UI.Icon.Image.color;
            Assert.That(rect.anchorMin, Is.EqualTo(rect.anchorMax));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.2f, 0.7f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(32, 32)));
            view.Render(new Vector2(0.8f, 0.1f), true);
            Color gray = view.UI.Icon.Image.color;
            Assert.That(gray.r, Is.EqualTo(gray.g).Within(0.00001f));
            Assert.That(gray.g, Is.EqualTo(gray.b).Within(0.00001f));
            Assert.That(gray, Is.Not.EqualTo(active));
            Assert.That(view.UI.Icon.Image.sprite, Is.SameAs(flag));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(32, 32)));
            view.Render(Vector2.one * 0.5f, false);
            Assert.That(view.UI.Icon.Image.color, Is.EqualTo(active));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void AuthoritativeClearStateChangesWithoutPlayerMovementAndResetsWithMap()
    {
        using var f = new Fixture();
        Entity point = f.World.EntityManager.CreateEntity(typeof(DungeonInterestPointComponent));
        f.World.EntityManager.SetComponentData(point, new DungeonInterestPointComponent
        {
            EncounterId = 1, EncounterReady = 0, AliveGuardCount = 0, PatrolUnitCount = 2
        });
        f.World.EntityManager.AddBuffer<UnitVariableElement>(point);
        Assert.That(f.RefreshFlags(), Is.False, "An empty or unspawned point is not cleared.");
        Assert.That(f.Model.IsInterestPointCleared(1), Is.False);
        UnitVariableSource.TrySetValue(f.World.EntityManager, point,
            DungeonPatrolRuntimeUtility.EncounterDeadKey, UnitSourceValue.FromBool(true));
        Assert.That(f.RefreshFlags(), Is.True, "A stationary player must still receive a view refresh.");
        Assert.That(f.Model.IsInterestPointCleared(1), Is.True, "Surviving patrols do not undo the completed encounter.");
        Assert.That(f.RefreshFlags(), Is.False, "The unchanged state must not keep publishing updates.");
        f.World.EntityManager.DestroyEntity(point);
        Assert.That(f.RefreshFlags(), Is.False);
        Assert.That(f.Model.IsInterestPointCleared(1), Is.True);
        f.ReleaseMap();
        Assert.That(f.Model.IsInterestPointCleared(1), Is.False);
        Assert.That(GetField<World>(f.Model, "_interestPointQueryWorld"), Is.Null);
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(1, 1)]
    public void DownPointingSpriteRotatesToActualPlayerFacing(float x, float y)
    {
        using var f = new Fixture();
        Entity player = f.World.EntityManager.CreateEntity(
            typeof(UnitFactionComponent), typeof(UnitFacingComponent), typeof(LocalTransform),
            typeof(NetworkPlayerComponent));
        f.World.EntityManager.SetComponentData(player, new UnitFactionComponent { Value = UnitFactionType.Player });
        f.World.EntityManager.SetComponentData(player, new UnitFacingComponent { Direction = new float2(x, y) });
        f.World.EntityManager.SetComponentData(player, LocalTransform.FromPosition(new float3(2, 3, 0)));
        SetField(f.Model, "_cachedPlayerEntity", player);
        object[] args = { Vector3.zero, 0f };
        bool found = (bool)typeof(MinimapUIModel).GetMethod("TryGetPlayerPose", PrivateInstance).Invoke(f.Model, args);
        Assert.That(found, Is.True);
        Vector3 actual = Quaternion.Euler(0, 0, (float)args[1]) * Vector3.down;
        Vector3 expected = new Vector3(x, y, 0).normalized;
        Assert.That(Vector3.Dot(actual, expected), Is.GreaterThan(0.9999f));
        SetField(f.Model, "_playerRotationDegrees", (float)args[1]);
        f.World.EntityManager.SetComponentData(player, new UnitFacingComponent { Direction = float2.zero });
        typeof(MinimapUIModel).GetMethod("TryGetPlayerPose", PrivateInstance).Invoke(f.Model, args);
        Assert.That(Vector3.Dot(Quaternion.Euler(0, 0, (float)args[1]) * Vector3.down, expected),
            Is.GreaterThan(0.9999f), "A missing facing vector must preserve the last orientation.");
    }

    [Test]
    public void TerrainPixelsAndInterestPointCentersUseTheOriginalMapCoordinates()
    {
        using var f = new Fixture();
        Texture2D texture = f.Model.TerrainSprite.texture;
        Assert.That(texture.width, Is.EqualTo(10));
        Assert.That(texture.height, Is.EqualTo(8));
        Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That((Color32)texture.GetPixel(0, 0), Is.EqualTo(new Color32(18, 24, 30, 255)));
        Assert.That((Color32)texture.GetPixel(1, 1), Is.EqualTo(new Color32(63, 105, 70, 255)));
        Assert.That((Color32)texture.GetPixel(2, 1), Is.EqualTo(new Color32(126, 105, 80, 255)));
        Assert.That((Color32)texture.GetPixel(3, 1), Is.EqualTo(new Color32(24, 20, 30, 255)));
        var point = f.Layout.InterestPoints[0];
        Assert.That(f.Model.GetInterestPointPosition(point), Is.EqualTo(new Vector2(4.5f / 10, 3.5f / 8)));
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, PrivateInstance).SetValue(instance, value);
    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, PrivateInstance).GetValue(instance);

    private sealed class Fixture : IDisposable
    {
        private readonly World _previousWorld = World.DefaultGameObjectInjectionWorld;
        public readonly World World = new("MinimapFlagsTests");
        public readonly MinimapUIModel Model = new();
        public readonly OpenFieldDungeonLayout Layout;

        public Fixture()
        {
            World.DefaultGameObjectInjectionWorld = World;
            Layout = (OpenFieldDungeonLayout)Activator.CreateInstance(typeof(OpenFieldDungeonLayout),
                PrivateInstance, null, new object[] { 8, 6, 42 }, null);
            OpenFieldTerrainCell[] cells = GetField<OpenFieldTerrainCell[]>(Layout, "_terrainCells");
            cells[0] = OpenFieldTerrainCell.Ground;
            cells[1] = OpenFieldTerrainCell.Obstacle;
            typeof(OpenFieldDungeonLayout).GetMethod("AddInterestPoint", PrivateInstance).Invoke(Layout,
                new object[] { OpenFieldInterestSize.Large, new OpenFieldGridPosition(3, 2), 7 });
            SetField(Model, "_layout", Layout);
            GetField<HashSet<int>>(Model, "_interestPointIds").Add(1);
            typeof(MinimapUIModel).GetMethod("BuildTerrainSprite", PrivateInstance).Invoke(Model, new object[] { Layout });
        }

        public bool RefreshFlags() =>
            (bool)typeof(MinimapUIModel).GetMethod("RefreshInterestPointStates", PrivateInstance).Invoke(Model, null);

        public void ReleaseMap()
        {
            // The production model uses delayed runtime destruction; these tests run in EditMode.
            Sprite sprite = Model.TerrainSprite;
            Texture2D texture = sprite != null ? sprite.texture : null;
            SetField(Model, "_terrainSprite", null);
            SetField(Model, "_terrainTexture", null);
            typeof(MinimapUIModel).GetMethod("ReleaseMapVisual", PrivateInstance).Invoke(Model, null);
            if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        public void Dispose()
        {
            ReleaseMap();
            Model.Dispose();
            World.DefaultGameObjectInjectionWorld = _previousWorld;
            World.Dispose();
        }
    }
}
