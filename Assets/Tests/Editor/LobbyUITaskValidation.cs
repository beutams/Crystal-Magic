using System;
using System.IO;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using TMPro;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Explicitly requested editor validation, using temporary preview objects only.</summary>
public static class LobbyUITaskValidation
{
    private const string Output = "output/Previews/LobbyRedesign";
    [MenuItem("Tools/Validation/Lobby UI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play mode.");
        Directory.CreateDirectory(Output);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>(); var results = new Results();
        try
        {
            File.WriteAllText(Output + "/validation-status.txt", "Running lobby tests.");
            api.RegisterCallbacks(results);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "CreateRoomUITests", "RoomCreationNetworkTests", "HostedLobbyLifecycleTests" } })
                { runSynchronously = true });
            Render(false); Render(true);
        }
        catch (Exception exception)
        { File.AppendAllText(Output + "/validation-status.txt", "\n" + exception); Debug.LogException(exception); }
        finally { api.UnregisterCallbacks(results); UnityEngine.Object.DestroyImmediate(api); }
    }

    public static void BuildAndRun()
    {
        CrystalMagic.Editor.UI.LobbyUIPrefabBuilder.Build();
        Run();
    }

    public static void ValidateRoom()
    {
        CrystalMagic.Editor.UI.LobbyUIPrefabBuilder.BuildRoom();
        Directory.CreateDirectory(Output);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>(); var results = new Results();
        try
        {
            api.RegisterCallbacks(results);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "CreateRoomUITests", "RoomCreationNetworkTests", "HostedLobbyLifecycleTests" } })
                { runSynchronously = true });
            RenderRoom(true); RenderRoom(false);
        }
        finally { api.UnregisterCallbacks(results); UnityEngine.Object.DestroyImmediate(api); }
    }

    public static void RenderRoom(bool host)
    {
        var preview = new PreviewRenderUtility(true, true);
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/LobbyRoomUI.prefab"));
        GameObject events = null;
        var view = root.GetComponent<LobbyRoomUI>();
        try
        {
            if (!EventComponent.TryGetInstance(out _)) events = new GameObject("RoomPreviewEvents", typeof(EventComponent));
            preview.AddSingleGO(root); root.SetActive(true);
            foreach (var text in root.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true)) text.ApplyLocalizationKey();
            var room = new Server.RoomData { roomId = 1, ownerAccountId = 101, roomName = "旅行者的房间", maxNum = 4 };
            room.players.Add(101, "旅行者"); room.players.Add(202, "星火"); room.players.Add(303, "月影");
            room.playerready.Add(101, false); room.playerready.Add(202, true); room.playerready.Add(303, false);
            var model = new LobbyRoomUIModel(); model.SetRoom(room, host ? 101UL : 202UL);
            view.EnsureInitialized(); view.BindModel(model); view.OnOpen();
            const int width = 2560, height = 1440;
            preview.camera.orthographic = true; preview.camera.orthographicSize = height * .5f;
            preview.camera.transform.position = new Vector3(0, 0, -1000); preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.nearClipPlane = 1; preview.camera.farClipPlane = 2000;
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.black;
            preview.BeginStaticPreview(new Rect(0, 0, width, height)); preview.camera.cameraType = CameraType.Game;
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = preview.camera; canvas.planeDistance = 100;
            root.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
            preview.camera.Render(); Texture2D image = preview.EndStaticPreview();
            File.WriteAllBytes(Output + (host ? "/room-host-unity.png" : "/room-member-unity.png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { view.OnClose(); preview.Cleanup(); if (events != null) UnityEngine.Object.DestroyImmediate(events); }
    }

    public static void Render(bool modal)
    {
        var preview = new PreviewRenderUtility(true, true);
        GameObject lobby = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/LobbyUI.prefab"));
        GameObject form = null, events = null;
        try
        {
            if (!EventComponent.TryGetInstance(out _)) events = new GameObject("LobbyPreviewEvents", typeof(EventComponent));
            preview.AddSingleGO(lobby); lobby.SetActive(true);
            lobby.transform.Find("Panel/Empty").gameObject.SetActive(false);
            Transform content = lobby.transform.Find("Panel/RoomScroll/Viewport/Content");
            GameObject template = content.Find("RoomItem").gameObject;
            var rooms = new[] {
                new Server.SimpleRoomData {roomId=1, roomName="旅行者的房间", ownerName="旅行者", enterNum=1, maxNum=4},
                new Server.SimpleRoomData {roomId=2, roomName="地牢探索", ownerName="星火", enterNum=2, maxNum=3},
                new Server.SimpleRoomData {roomId=3, roomName="双人冒险", ownerName="月影", enterNum=2, maxNum=2},
            };
            foreach (var room in rooms)
            {
                GameObject row = UnityEngine.Object.Instantiate(template, content); row.SetActive(true);
                var view = row.GetComponent<LobbyUI_RoomItemView>(); view.Render(room); view.SetInteractable(true);
            }
            if (modal)
            {
                form = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/CreateRoomUI.prefab"));
                preview.AddSingleGO(form); form.SetActive(true);
                var model = new CreateRoomUIModel(); model.InitializeName("旅行者");
                var view = form.GetComponent<CreateRoomUI>(); view.EnsureInitialized(); view.BindModel(model);
                typeof(CreateRoomUI).GetMethod("RefreshView", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            }
            const int width = 2560, height = 1440;
            preview.camera.orthographic = true; preview.camera.orthographicSize = height * .5f;
            preview.camera.transform.position = new Vector3(0, 0, -1000); preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.nearClipPlane = 1; preview.camera.farClipPlane = 2000;
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.black;
            preview.BeginStaticPreview(new Rect(0, 0, width, height)); preview.camera.cameraType = CameraType.Game;
            int order = 0;
            foreach (GameObject root in modal ? new[] { lobby, form } : new[] { lobby })
            {
                Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = preview.camera; canvas.planeDistance = 100; canvas.sortingOrder = order++;
                root.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
                foreach (var text in root.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true)) text.ApplyLocalizationKey();
            }
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in lobby.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
            if (form != null) foreach (TMP_Text text in form.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
            preview.camera.Render(); Texture2D image = preview.EndStaticPreview();
            File.WriteAllBytes(Output + (modal ? "/create-room-unity.png" : "/lobby-unity.png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { preview.Cleanup(); if (events != null) UnityEngine.Object.DestroyImmediate(events); }
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
