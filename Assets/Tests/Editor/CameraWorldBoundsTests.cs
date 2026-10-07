using CrystalMagic.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class CameraWorldBoundsTests
{
    private Scene _scene;
    private Camera _camera;
    private CameraWorldBoundsConstraint _constraint;
    private static Bounds Training => new Bounds(new Vector3(7.5f, -7.5f, .45f), new Vector3(15, 15, 1.1f));

    [SetUp]
    public void Setup()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Camera boundary test", typeof(Camera));
        SceneManager.MoveGameObjectToScene(root, _scene);
        _camera = root.GetComponent<Camera>();
        _camera.scene = _scene;
        _camera.transform.position = new Vector3(0, 0, -15);
        _camera.aspect = 16f / 9f;
        _camera.fieldOfView = 60;
        _camera.orthographicSize = 10;
        _constraint = new CameraWorldBoundsConstraint();
    }

    [TearDown]
    public void TearDown()
    {
        _constraint.Reset();
        EditorSceneManager.ClosePreviewScene(_scene);
    }

    [TestCase(false, 16f / 9f)]
    [TestCase(false, 32f / 9f)]
    [TestCase(false, 9f / 16f)]
    [TestCase(true, 16f / 9f)]
    [TestCase(true, 32f / 9f)]
    [TestCase(true, 9f / 16f)]
    public void SmallMapFitsEntireViewportIncludingLayerDepth(bool orthographic, float aspect)
    {
        _camera.orthographic = orthographic;
        _camera.aspect = aspect;
        _constraint.Set(1, Training);
        foreach (Vector2 corner in new[] { new Vector2(-100, -100), new Vector2(100, -100), new Vector2(-100, 100), new Vector2(100, 100) })
        {
            _camera.transform.position = new Vector3(corner.x, corner.y, -15);
            _constraint.Apply(_camera);
            AssertViewportInside(_camera, Training);
            _camera.transform.position = _constraint.ClampPosition(_camera,
                _camera.transform.position + new Vector3(corner.x, corner.y, 0)); // Shake is clamped after follow.
            AssertViewportInside(_camera, Training);
            Assert.That(_camera.transform.position.z, Is.EqualTo(-15));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResizeAndMapSwitchRecoverOriginalLensAndIgnoreOldOwner(bool orthographic)
    {
        _camera.orthographic = orthographic;
        _constraint.Set(1, Training);
        _constraint.Apply(_camera);
        float smallLens = orthographic ? _camera.orthographicSize : _camera.fieldOfView;
        _camera.aspect = .75f;
        _constraint.Apply(_camera);
        Assert.That(orthographic ? _camera.orthographicSize : _camera.fieldOfView, Is.GreaterThan(smallLens));
        AssertViewportInside(_camera, Training);
        Bounds town = new Bounds(new Vector3(45, -30, 0), new Vector3(90, 60, 0));
        _constraint.Set(2, town);
        _constraint.Apply(_camera);
        Assert.That(_camera.fieldOfView, Is.EqualTo(60));
        Assert.That(_camera.orthographicSize, Is.EqualTo(10));
        _constraint.Clear(1); // Delayed release of the previous map.
        _camera.transform.position = new Vector3(500, 500, -15);
        _constraint.Apply(_camera);
        AssertViewportInside(_camera, town);
        _constraint.Set(3, Training);
        _constraint.Apply(_camera);
        _constraint.Clear(3);
        Assert.That(_camera.fieldOfView, Is.EqualTo(60));
        Assert.That(_camera.orthographicSize, Is.EqualTo(10));
        Vector3 unbounded = new Vector3(500, 500, -15);
        Assert.That(_constraint.ClampPosition(_camera, unbounded), Is.EqualTo(unbounded));
    }

    [Test]
    public void ChangingCameraRestoresPreviousCameraAndFitsNewCamera()
    {
        _constraint.Set(1, Training);
        _constraint.Apply(_camera);
        var second = new GameObject("Second camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(second, _scene);
        Camera camera = second.GetComponent<Camera>();
        camera.aspect = 2; camera.fieldOfView = 70;
        camera.transform.position = new Vector3(-100, 100, -20);
        _constraint.Apply(camera);
        Assert.That(_camera.fieldOfView, Is.EqualTo(60));
        AssertViewportInside(camera, Training);
        _constraint.Reset();
        Assert.That(camera.fieldOfView, Is.EqualTo(70));
    }

    [TestCase("Assets/Res/Tile/OcclusionMaps/TownMap/TownMap_Occlusion.prefab", 90, 60)]
    [TestCase("Assets/Res/Tile/OcclusionMaps/TrainingMap/TrainingMap_Occlusion.prefab", 15, 15)]
    public void RealMapsUseCollisionExtentAndVisibleLayerDepth(string path, int width, int height)
    {
        var map = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), _scene);
        Bounds bounds = SceneMapLayout.GetCameraWorldBounds(map);
        Assert.That(bounds.min.x, Is.EqualTo(0));
        Assert.That(bounds.min.y, Is.EqualTo(-height));
        Assert.That(bounds.max.x, Is.EqualTo(width));
        Assert.That(bounds.max.y, Is.EqualTo(0));
        foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>())
            if (renderer.enabled)
            {
                Assert.That(renderer.bounds.min.z, Is.GreaterThanOrEqualTo(bounds.min.z - .00001f));
                Assert.That(renderer.bounds.max.z, Is.LessThanOrEqualTo(bounds.max.z + .00001f));
            }
        _constraint.Set(map.GetInstanceID(), bounds);
        _constraint.Apply(_camera);
        AssertViewportInside(_camera, bounds);
    }

    private static void AssertViewportInside(Camera camera, Bounds bounds)
    {
        const float tolerance = .0002f;
        foreach (float z in new[] { bounds.min.z, bounds.max.z })
            foreach (Vector2 corner in new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one })
            {
                Ray ray = camera.ViewportPointToRay(corner);
                Vector3 point = ray.GetPoint((z - ray.origin.z) / ray.direction.z);
                Assert.That(point.x, Is.InRange(bounds.min.x - tolerance, bounds.max.x + tolerance));
                Assert.That(point.y, Is.InRange(bounds.min.y - tolerance, bounds.max.y + tolerance));
            }
    }
}
