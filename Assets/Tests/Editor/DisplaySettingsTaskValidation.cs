using System.IO;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using TMPro;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Opt-in validation of temporary prefab instances; never changes a scene or user settings.</summary>
[InitializeOnLoad]
public static class DisplaySettingsTaskValidation
{
    private const string Output = "output/display-settings";
    private const string Request = Output + "/run-tests.request";
    static DisplaySettingsTaskValidation() => EditorApplication.delayCall += RunRequested;

    private static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
        {
            File.WriteAllText(Output + "/validation-status.txt", "Not run: editor is busy. Use Tools/Validation/Display Settings when idle.");
            return;
        }
        File.Delete(Request);
        Run();
    }

    [MenuItem("Tools/Validation/Display Settings")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new System.InvalidOperationException("Run display validation outside Play mode/builds.");
        Directory.CreateDirectory(Output);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var results = new Results();
        try
        {
            File.WriteAllText(Output + "/validation-status.txt", "Running display/settings EditMode tests.");
            Undo.IncrementCurrentGroup();
            api.RegisterCallbacks(results);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "DisplaySettingsTests", "SettingUIPaperLayoutTests", "CharacterUISettingsTests",
                    "AutoInteractionTests", "NPCSequenceTests" } })
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

    private static void Render(bool book)
    {
        var preview = new PreviewRenderUtility(true, true);
        GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(book
            ? "Assets/Res/UI/CharacterUI.prefab" : "Assets/Res/UI/SettingUI.prefab"));
        try
        {
            preview.AddSingleGO(root);
            root.SetActive(true);
            var settings = new GameSettingsData { ResolutionWidth = 1920, ResolutionHeight = 1080, SkipTutorial = true };
            if (book)
            {
                foreach (Transform child in root.transform)
                    child.gameObject.SetActive(child.name == "BG" || child.name == "Setting");
                root.transform.Find("Setting").GetComponent<CharacterUI_SettingView>().Render(settings, CharacterSettingsSection.Display);
            }
            else
            {
                var view = root.GetComponent<SettingUI>();
                var model = new SettingUIModel();
                typeof(SettingUIModel).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, settings);
                view.EnsureInitialized();
                view.BindModel(model);
                typeof(SettingUI).GetMethod("RefreshView", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            }
            // Resolve authored localized labels without requiring a running GameEntry.
            var table = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Res/Data/LocalizationDataTable.json"));
            foreach (LocalizedTextMeshProUGUI label in root.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true))
            {
                string key = label.LocalizationKey;
                if (string.IsNullOrEmpty(key)) continue;
                foreach (var row in table["Rows"])
                    if ((string)row["Key"] == key) { label.text = (string)row["ChineseSimplified"]; break; }
            }
            int width = book ? 1222 : 1281, height = book ? 728 : 1072;
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = height * .5f;
            preview.camera.transform.position = new Vector3(0, 0, -1000);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.nearClipPlane = 1;
            preview.camera.farClipPlane = 2000;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.16f, .19f, .17f);
            preview.BeginStaticPreview(new Rect(0, 0, width, height));
            preview.camera.cameraType = CameraType.Game;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = preview.camera;
            canvas.planeDistance = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            if (book)
                foreach (Transform child in root.transform) child.localScale *= .5f;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate();
            preview.camera.Render();
            Texture2D image = preview.EndStaticPreview();
            File.WriteAllBytes(Output + (book ? "/book.png" : "/menu.png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally { preview.Cleanup(); }
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
