using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using CrystalMagic.Core;
using NUnit.Framework;
using Server;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BattlePreparationFailureTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase("WaitingForScene")]
    [TestCase("WaitingForEntities")]
    [TestCase("WaitingForStartFrame")]
    public void AbortReportsTheInterruptedStageAndPreservesTheFirstFailure(string stage)
    {
        ClientBattleManager manager = new();
        SetStage(manager, stage);
        int failures = 0;
        manager.onPreparationFailed += _ => failures++;
        LogAssert.Expect(LogType.Error, $"[BattleTrace][Client] Preparation failed: stage={stage}, error=test failure");
        manager.AbortBattle("test failure");
        manager.FailPreparation("later failure");
        Assert.That(manager.PreparationError, Is.EqualTo("test failure"));
        Assert.That(failures, Is.EqualTo(1));
        Assert.That(StageField.GetValue(manager).ToString(), Is.EqualTo("None"));
    }

    [Test]
    public void DisconnectDuringSceneLoadingReportsWaitingForSceneAndRequestsRecovery()
    {
        GameObject root = new("Battle preparation test transition");
        try
        {
            TransitionComponent transition = root.AddComponent<TransitionComponent>();
            typeof(TransitionComponent).GetField("_isTransitioning", PrivateInstance).SetValue(transition, true);
            ClientBattleManager manager = new();
            SetField(manager, "preBattleSaveIndex", 0);
            SetField(manager, "preBattleSaveGuid", Guid.NewGuid().ToString("N"));
            SetField(manager, "preBattleCharacterData", new CharacterData());
            SetStage(manager, "WaitingForScene");
            Connect connect = new() { State = ConnectState.Close };
            manager.battleConnect = connect;
            LogAssert.Expect(LogType.Error, new Regex("Preparation failed: stage=WaitingForScene, error=与战斗服务器的连接已断开。"));
            typeof(ClientBattleManager).GetMethod("OnBattleDisconnected", PrivateInstance).Invoke(manager, new object[] { connect });
            Assert.That(manager.PreparationFailed, Is.True);
            Assert.That(manager.RestoreStandaloneRequested, Is.True);
            Assert.That(manager.battleConnect, Is.Null);
            Assert.That(StageField.GetValue(manager).ToString(), Is.EqualTo("None"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void PreparationTimerReportsItsStageAfterClearingTheTimerState()
    {
        ClientBattleManager manager = new();
        object stage = Enum.Parse(StageField.FieldType, "WaitingForScene");
        typeof(ClientBattleManager).GetMethod("StartPreparationTimeout", PrivateInstance).Invoke(manager, new[] { stage });
        long id = (long)typeof(ClientBattleManager).GetField("preparationTimerId", PrivateInstance).GetValue(manager);
        try
        {
            var timers = (IDictionary)typeof(NetworkTimer).GetField("timers", PrivateInstance).GetValue(NetworkTimer.Instance);
            object timer = timers[id];
            Action callback = (Action)timer.GetType().GetField("Callback").GetValue(timer);
            LogAssert.Expect(LogType.Error, new Regex("Preparation failed: stage=WaitingForScene, error=战斗准备阶段超时：WaitingForScene。"));
            callback();
            Assert.That(manager.PreparationFailed, Is.True);
            Assert.That(StageField.GetValue(manager).ToString(), Is.EqualTo("None"));
        }
        finally { NetworkTimer.Instance.Remove(id); }
    }

    private static FieldInfo StageField => typeof(ClientBattleManager).GetField("preparationStage", PrivateInstance);
    private static void SetStage(ClientBattleManager manager, string stage) => StageField.SetValue(manager, Enum.Parse(StageField.FieldType, stage));
    private static void SetField(ClientBattleManager manager, string name, object value) => typeof(ClientBattleManager).GetField(name, PrivateInstance).SetValue(manager, value);
}
