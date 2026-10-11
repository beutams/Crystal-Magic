using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Game.Map;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Opt-in only. Uses temporary preview objects; never saves or changes a user scene.
[InitializeOnLoad]
public static class TrainingCameraTaskValidation
{
    private const string Output = "output/training-camera";
    private const string Request = Output + "/run-tests.request";
    static TrainingCameraTaskValidation() => EditorApplication.delayCall += RunRequested;

    private static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
        {
            File.WriteAllText(Output + "/validation-status.txt", "Not run: editor busy. Use Tools/Validation/Training Camera when idle.");
            return;
        }
        File.Delete(Request);
        Run();
    }

    [MenuItem("Tools/Validation/Training Camera")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new System.InvalidOperationException("Run training camera validation outside Play mode/builds.");
        Directory.CreateDirectory(Output);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var results = new Results();
        try
        {
            File.WriteAllText(Output + "/validation-status.txt", "Running training camera EditMode tests.");
            api.RegisterCallbacks(results);
            // TestRunner reverts its captured Undo group at completion. Never
            // share that group with an earlier user prefab edit.
            Undo.IncrementCurrentGroup();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "CameraWorldBoundsTests", "SceneMapAssetTests",
                    "SceneMapPreviewTests.TrainingPreviewUsesRuntimeAssetScaleAndVisibleBounds",
                    "SceneMapPreviewTests.SwitchingPreviewReplacesOnlyBackdropAndPreservesPlacedObjects" } })
                { runSynchronously = true });
            Render(false);
            Render(true);
        }
        catch (System.Exception exception)
        {
            File.AppendAllText(Output + "/validation-status.txt", "\n" + exception);
            Debug.LogException(exception);
        }
        finally { api.UnregisterCallbacks(results); Object.DestroyImmediate(api); }
    }

    private static void Render(bool expanded)
    {
        var preview = new PreviewRenderUtility(true, true);
        var constraint = new CameraWorldBoundsConstraint();
        try
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SceneMapLayout.Training.PrefabPath));
            preview.AddSingleGO(root);
            SceneMapLayout.Training.Apply(root.transform);
            root.GetComponent<TileOcclusionMap>().CameraBackdrop.enabled = expanded;
            foreach (var tilemap in root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>()) tilemap.RefreshAllTiles();
            Camera camera = preview.camera;
            camera.cameraType = CameraType.Game;
            camera.orthographic = false;
            camera.fieldOfView = 60;
            camera.aspect = 16f / 9f;
            camera.transform.position = new Vector3(7.5f, -10.5f, -15f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta; // Any missing backdrop remains obvious.
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            constraint.Set(root.GetInstanceID(), SceneMapLayout.GetCameraWorldBounds(root));
            constraint.Apply(camera);
            preview.BeginStaticPreview(new Rect(0, 0, 1280, 720));
            camera.Render();
            Texture2D image = preview.EndStaticPreview();
            File.WriteAllBytes(Output + (expanded ? "/after.png" : "/before.png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally { constraint.Reset(); preview.Cleanup(); }
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(Output + "/tests.xml"));
            File.WriteAllText(Output + "/validation-status.txt",
                $"{result.TestStatus}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}\n{result.Message}");
        }
    }
}
