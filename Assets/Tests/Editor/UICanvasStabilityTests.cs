using System;
using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UICanvasStabilityTests
{
    [Test]
    public void BattleHudGeometryAndHitAreasStayFixedWhenSceneCameraMoves()
    {
        using CanvasFixture fixture = new();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/BattleUI.prefab");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, fixture.Scene);
        BattleUI panel = instance.GetComponent<BattleUI>();
        typeof(UIGroup).GetMethod("AttachPanel", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(fixture.Group, new object[] { panel });
        Canvas.ForceUpdateCanvases();

        GraphicRaycaster raycaster = panel.GetComponent<GraphicRaycaster>();
        RectTransform[] rects = instance.GetComponentsInChildren<RectTransform>(true);
        Vector2[] initialCorners = ReadScreenCorners(rects, raycaster.eventCamera);
        Assert.That(panel.Canvas.rootCanvas, Is.EqualTo(fixture.Group.GetComponent<Canvas>()));
        Assert.That(raycaster.eventCamera, Is.Null, "Fixed HUD must not use the scene camera for hit testing.");

        for (int i = 0; i < 32; i++)
        {
            fixture.Camera.transform.position = new Vector3(75f + i * 0.137f, -43f + i * 0.083f, -10f);
            fixture.Camera.orthographicSize = 5f + i * 0.1f;
            Canvas.ForceUpdateCanvases();
            Vector2[] corners = ReadScreenCorners(rects, raycaster.eventCamera);
            for (int c = 0; c < corners.Length; c++)
                Assert.That(Vector2.Distance(corners[c], initialCorners[c]), Is.LessThan(0.001f),
                    $"{rects[c / 4].name} moved with the scene camera at step {i}.");

            var panelRect = (RectTransform)panel.transform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, panelRect.TransformPoint(panelRect.rect.center));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(panelRect, center, raycaster.eventCamera), Is.True);
        }
    }

    [TestCase(1f)]
    [TestCase(0.75f)]
    public void WorldMarkersProjectIntoScaledOverlayWithoutACanvasCamera(float scale)
    {
        using CanvasFixture fixture = new();
        fixture.Group.GetComponent<CanvasScaler>().enabled = false;
        Canvas canvas = fixture.Group.GetComponent<Canvas>();
        canvas.scaleFactor = scale;
        RectTransform root = (RectTransform)fixture.Group.transform;
        Vector3 target = new(2.3f, -1.7f, 0f);
        Vector2 previous = default;

        for (int i = 0; i < 8; i++)
        {
            fixture.Camera.transform.position = new Vector3(i * 0.137f, i * 0.083f, -10f);
            Canvas.ForceUpdateCanvases();
            Vector3 projected = fixture.Camera.WorldToScreenPoint(target);
            Assert.That(projected.z, Is.GreaterThan(0f));
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(root, projected, null, out Vector2 local), Is.True);
            Vector2 displayed = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(local));
            Assert.That(Vector2.Distance(displayed, projected), Is.LessThan(0.001f));
            if (i > 0)
                Assert.That(Vector2.Distance(displayed, previous), Is.GreaterThan(0.1f),
                    "World markers must still follow scene-camera projection.");
            previous = displayed;
        }
    }

    private static Vector2[] ReadScreenCorners(RectTransform[] rects, Camera eventCamera)
    {
        Vector2[] positions = new Vector2[rects.Length * 4];
        Vector3[] corners = new Vector3[4];
        for (int r = 0; r < rects.Length; r++)
        {
            rects[r].GetWorldCorners(corners);
            for (int c = 0; c < 4; c++)
                positions[r * 4 + c] = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[c]);
        }
        return positions;
    }

    private sealed class CanvasFixture : IDisposable
    {
        public readonly Scene Scene;
        public readonly ListUIGroup Group;
        public readonly Camera Camera;

        public CanvasFixture()
        {
            Scene = EditorSceneManager.NewPreviewScene();
            GameObject cameraObject = new("UI stability test camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, Scene);
            Camera = cameraObject.GetComponent<Camera>();
            Camera.enabled = false;
            Camera.orthographic = true;
            Camera.orthographicSize = 5f;
            Camera.pixelRect = new Rect(0, 0, 1920, 1080);
            Camera.transform.position = new Vector3(0, 0, -10);

            GameObject groupObject = new("UI stability test group", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(groupObject, Scene);
            Group = groupObject.AddComponent<ListUIGroup>();
            // Edit-mode tests do not run the group's normal play-mode Awake.
            typeof(UIGroup).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Group, null);
            Group.ConfigureGroup("Test", 100);
            Group.ConfigureCanvasSettings(new Vector2(2560, 1440), CanvasScaler.ScreenMatchMode.Expand);
        }

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(Scene);
        }
    }
}
