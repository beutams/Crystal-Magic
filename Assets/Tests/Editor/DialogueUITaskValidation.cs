using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Opt-in EditMode validation. Never stops Play mode or touches scene/save data.</summary>
[InitializeOnLoad]
public static class DialogueUITaskValidation
{
    private const string Output = "output/dialogue-ui";
    private const string Request = Output + "/run-tests.request";

    static DialogueUITaskValidation() => EditorApplication.delayCall += RunRequested;

    private static void RunRequested()
    {
        if (!File.Exists(Request))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            File.WriteAllText(Output + "/validation-status.txt", "Not run: editor is playing, compiling or updating. Use the validation menu when idle.");
            return;
        }
        File.Delete(Request);
        Run();
    }

    [MenuItem("Tools/Validation/Dialogue UI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run Dialogue UI validation outside Play mode.");
        Directory.CreateDirectory(Output);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callback = new Results();
        try
        {
            File.WriteAllText(Output + "/validation-status.txt", "Running EditMode tests.");
            api.RegisterCallbacks(callback);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { "DialogueUITests" } }) { runSynchronously = true });
            DialogueUITests.RenderPreview(Output + "/preview.png");
        }
        catch (Exception exception)
        {
            File.AppendAllText(Output + "/validation-status.txt", "\n" + exception);
            Debug.LogException(exception);
        }
        finally
        {
            api.UnregisterCallbacks(callback);
            UnityEngine.Object.DestroyImmediate(api);
        }
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(Output + "/tests.xml"));
            File.WriteAllText(Output + "/validation-status.txt", $"{result.TestStatus}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}\n{result.Message}");
        }
    }
}
