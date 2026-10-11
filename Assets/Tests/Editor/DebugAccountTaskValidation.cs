using System.IO;
using CrystalMagic.UI;
using TMPro;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Opt-in isolated tests/preview. Does not switch accounts, connect to Steam, or alter saves/scenes/preferences.</summary>
[InitializeOnLoad]
public static class DebugAccountTaskValidation
{
    private const string Output = "output/debug-account";
    private const string Request = Output + "/run-tests.request";
    static DebugAccountTaskValidation() => EditorApplication.delayCall += RunRequested;

    private static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Tools/Validation/Debug Account")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new System.InvalidOperationException("Run account validation outside Play mode/builds.");
        Directory.CreateDirectory(Output);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var results = new Results();
        try
        {
            File.WriteAllText(Output + "/validation-status.txt", "Running account EditMode tests.");
            // Test-runner Undo cleanup must not include the user's preceding editor operation.
            Undo.IncrementCurrentGroup();
            api.RegisterCallbacks(results);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "ClientAccountIdentityTests", "DebugAccountTests" } }) { runSynchronously = true });
            Render();
        }
        catch (System.Exception exception)
        {
            File.AppendAllText(Output + "/validation-status.txt", "\n" + exception);
            Debug.LogException(exception);
        }
        finally { api.UnregisterCallbacks(results); Object.DestroyImmediate(api); }
    }

    private static void Render()
    {
        var preview = new PreviewRenderUtility(true, true);
        GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/DebugUI.prefab"));
        try
        {
            preview.AddSingleGO(root);
            root.SetActive(true);
            var data = new DebugUIData();
            data.Bind(root.transform);
            data.Content.GameObject.SetActive(true);
            data.Content.CanvasGroup.alpha = 1;
            data.Content_PlayerAttributes.GameObject.SetActive(false);
            data.Content_TrainingGround.GameObject.SetActive(false);
            data.Content_UnitSpawner.GameObject.SetActive(false);
            data.Content_UnitInspector.GameObject.SetActive(false);
            data.Content_Account.GameObject.SetActive(true);
            data.Content_Account_Info.TextMeshProUGUI.text = "当前：本地测试 / Socket\n账号：Tester2  (ID: 2)\n大厅：127.0.0.1:10002\n可切换。完成后需重新选择该账号的存档。";
            data.Content_Account_Status.TextMeshProUGUI.text = new DebugUIModel().AccountStatusText;
            data.Content_Account_LocalId.TMP_InputField.SetTextWithoutNotify("2");
            data.Content_Account_ReturnMenu.ButtonPlus.enabled = false;
            data.Content_Account_ReturnMenu_Label.TextMeshProUGUI.alpha = .35f;
            var template = data.Content_Navigation_Viewport_Content_DebugItem.GameObject;
            template.SetActive(true);
            template.GetComponent<DebugUI_NavigationItemView>().Render(new(DebugPage.Account, "账号与联机"), true);
            const int width = 1500, height = 900;
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
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = preview.camera;
            canvas.planeDistance = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate();
            preview.camera.Render();
            Texture2D image = preview.EndStaticPreview();
            File.WriteAllBytes(Output + "/account.png", image.EncodeToPNG());
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
