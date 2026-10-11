using System;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DebugAccountTests
{
    [TestCase("1", 1UL)]
    [TestCase(" 27 ", 27UL)]
    [TestCase("9223372036854775807", 9223372036854775807UL)]
    public void LocalSelectionHasReadableNameAndIsolatedIdentity(string text, ulong expected)
    {
        Assert.That(ClientAccountDebugPreferences.TrySelect(new LobbyAccountConfig(), true, text, true,
            out var selected, out string error), Is.True, error);
        Assert.That(selected.accountId, Is.EqualTo(expected));
        Assert.That(selected.username, Is.EqualTo("Tester" + expected));
        Assert.That(selected.useLocalTestAccount, Is.True);
        Assert.That(selected.allowDevelopmentFallback, Is.False);
        Assert.That(ClientAccountPolicy.Resolve(selected, true, 76561198000000001UL, "Steam").IsLocalTestAccount, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("+1")]
    [TestCase("1.5")]
    [TestCase("abc")]
    [TestCase("9223372036854775808")]
    [TestCase("18446744073709551616")]
    public void InvalidLocalIdDoesNotProduceAConfig(string text)
    {
        Assert.That(ClientAccountDebugPreferences.TrySelect(new LobbyAccountConfig(), true, text, true,
            out var selected, out string error), Is.False);
        Assert.That(selected, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void SteamSelectionNeverFallsBackAndDoesNotRequireALocalId()
    {
        Assert.That(ClientAccountDebugPreferences.TrySelect(new LobbyAccountConfig(), false, "invalid", true,
            out var selected, out _), Is.True);
        Assert.That(selected.useLocalTestAccount, Is.False);
        Assert.That(selected.allowDevelopmentFallback, Is.False);
        Assert.That(ClientAccountPolicy.Resolve(selected, true, 0, "").IsValid, Is.False);
    }

    [Test]
    public void ChoicePreservesNetworkEndpointsWithoutMutatingSharedConfig()
    {
        var original = new LobbyAccountConfig { accountId = 8, username = "Original", lobbyAddress = "192.168.1.9",
            lobbyPort = 12345, hostedTcpAddress = "192.168.1.10", hostedTcpPort = 23456 };
        Assert.That(ClientAccountDebugPreferences.TrySelect(original, true, "4", true, out var selected, out _), Is.True);
        Assert.That(selected, Is.Not.SameAs(original));
        Assert.That(selected.lobbyAddress, Is.EqualTo(original.lobbyAddress));
        Assert.That(selected.lobbyPort, Is.EqualTo(original.lobbyPort));
        Assert.That(selected.hostedTcpAddress, Is.EqualTo(original.hostedTcpAddress));
        Assert.That(selected.hostedTcpPort, Is.EqualTo(original.hostedTcpPort));
        Assert.That(original.accountId, Is.EqualTo(8));
        Assert.That(original.username, Is.EqualTo("Original"));
        Assert.That(original.useLocalTestAccount, Is.False);
        Assert.That(original.allowDevelopmentFallback, Is.True);
    }

    [Test]
    public void LaunchArgumentsOverrideDebugSelection()
    {
        ClientAccountDebugPreferences.TrySelect(new LobbyAccountConfig(), true, "5", true, out var selected, out _);
        var steam = ClientAccountPolicy.ForLaunch(selected, new[] { "-steamAccount" }, true);
        var another = ClientAccountPolicy.ForLaunch(selected, new[] { "-localAccount", "6" }, true);
        Assert.That(steam.useLocalTestAccount, Is.False);
        Assert.That(another.accountId, Is.EqualTo(6));
        Assert.That(selected.accountId, Is.EqualTo(5));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReleaseRejectsDebugSelectionAndIgnoresPreferences(bool local)
    {
        var source = new LobbyAccountConfig();
        Assert.That(ClientAccountDebugPreferences.TrySelect(source, local, "2", false, out var selected, out _), Is.False);
        Assert.That(selected, Is.Null);
        Assert.That(ClientAccountDebugPreferences.Apply(source, false), Is.SameAs(source));
    }

    [Test]
    public void AccountPageIsAvailableOutsideTraining()
    {
        var model = new DebugUIModel();
        Assert.That(model.Pages.Count(p => p.Page == DebugPage.Account), Is.EqualTo(1));
        Assert.That(model.IsTrainingGroundActive, Is.False);
        Assert.That(model.CanSwitchAccount, Is.False, "Controller must first verify the current game state.");
    }

    [Test]
    public void PrefabHasRealAccountBindingsAndSingleLineNumericIdInput()
    {
        var data = new DebugUIData();
        data.Bind(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/DebugUI.prefab").transform);
        Assert.That(data.Content_Account_UseLocal.ButtonPlus, Is.Not.Null);
        Assert.That(data.Content_Account_UseSteam.ButtonPlus, Is.Not.Null);
        Assert.That(data.Content_Account_ReturnMenu.ButtonPlus, Is.Not.Null);
        Assert.That(data.Content_Account_Info.TextMeshProUGUI.richText, Is.False);
        var input = data.Content_Account_LocalId.TMP_InputField;
        Assert.That(input.textComponent, Is.SameAs(data.Content_Account_LocalId_Text.TextMeshProUGUI));
        Assert.That(input.lineType, Is.EqualTo(TMP_InputField.LineType.SingleLine));
        Assert.That(input.characterLimit, Is.EqualTo(19));
    }

    [Test]
    public void ReopeningViewDoesNotDuplicateAccountIntentsAndClosingUnsubscribes()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Res/UI/DebugUI.prefab");
        try
        {
            var view = root.GetComponent<DebugUI>();
            view.EnsureInitialized();
            var data = new DebugUIData();
            data.Bind(root.transform);
            int local = 0, steam = 0, returns = 0, edits = 0;
            view.AccountSwitchRequested += isLocal => { if (isLocal) local++; else steam++; };
            view.AccountReturnMenuRequested += () => returns++;
            view.LocalAccountIdChanged += _ => edits++;
            void ClickAll()
            {
                data.Content_Account_UseLocal.ButtonPlus.onClick.Invoke();
                data.Content_Account_UseSteam.ButtonPlus.onClick.Invoke();
                data.Content_Account_ReturnMenu.ButtonPlus.onClick.Invoke();
                data.Content_Account_LocalId.TMP_InputField.onValueChanged.Invoke("2");
            }
            for (int i = 0; i < 2; i++)
            {
                view.OnOpen();
                ClickAll();
                view.OnClose();
                ClickAll();
            }
            Assert.That(new[] { local, steam, returns, edits }, Is.EqualTo(new[] { 2, 2, 2, 2 }));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void AccountResetClearsOldSlotAndRuntimeDataButPreservesListeners()
    {
        // Isolated, inactive test instance only: never initialize or touch actual save files/singletons.
        var root = new GameObject("Account reset test");
        root.SetActive(false);
        try
        {
            var save = root.AddComponent<SaveDataComponent>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo Field(string name) => typeof(SaveDataComponent).GetField(name, flags);
            Field("_currentSaveIndex").SetValue(save, 3);
            Field("_currentSaveGuid").SetValue(save, Guid.NewGuid().ToString("N"));
            Field("_lastBattleSettlementId").SetValue(save, "previous-account-settlement");
            Action<string> listener = _ => { };
            save.OnSaveFailed += listener;
            typeof(SaveDataComponent).GetMethod("ResetForAccountChange", flags).Invoke(save, null);
            Assert.That(save.CurrentSaveIndex, Is.EqualTo(-1));
            Assert.That(save.CurrentSaveGuid, Is.Null);
            Assert.That(Field("_lastBattleSettlementId").GetValue(save), Is.Null);
            Assert.That(Field("_globalData").GetValue(save), Is.Not.Null);
            Assert.That(Field("_variables").GetValue(save), Is.Not.Null);
            Assert.That(Field("OnSaveFailed").GetValue(save), Is.EqualTo(listener));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
