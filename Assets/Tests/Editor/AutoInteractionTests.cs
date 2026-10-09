using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public sealed class AutoInteractionTests
{
    [Test]
    public void AutomaticStoryTakesPriorityOverMoneyAndDoesNotSelectManualBranch()
    {
        using var fixture = new Fixture();
        fixture.Npc.Interactions.Insert(0, new NPCInteractionData { Key = "Manual" });
        fixture.Target(InteractionKind.Drop);
        Entity npc = fixture.Target(InteractionKind.Npc);
        fixture.Tick(0);
        var request = fixture.Transactions[0];
        Assert.That(request.Target, Is.EqualTo(npc));
        Assert.That(request.IsAutomatic, Is.EqualTo(1));
        Assert.That(request.InteractionKey.ToString(), Is.EqualTo("Story"));
        Assert.That(fixture.Npc.GetEnabledInteractions().Single().Key, Is.EqualTo("Manual"));
    }

    [Test]
    public void CompletionFlagMustExistAndBePending()
    {
        using var fixture = new Fixture();
        fixture.Npc.Interactions[0].CompletionVariable = TownIntroTriggerUtility.CompletionVariable;
        fixture.Target(InteractionKind.Npc);
        fixture.Tick(0);
        Assert.That(fixture.Transactions.Length, Is.Zero, "Legacy saves must not replay the introduction.");
        fixture.Variables.Set(TownIntroTriggerUtility.CompletionVariable, 0);
        fixture.Tick(0.1);
        Assert.That(fixture.Transactions.Length, Is.EqualTo(1));
        GameInteractionUtility.Complete(fixture.Manager, fixture.Runtime, fixture.Transactions[0].Target,
            InteractionResultCode.Success, UnitSourceValue.None);
        fixture.Variables.Set(TownIntroTriggerUtility.CompletionVariable, 1);
        fixture.Tick(3);
        Assert.That(fixture.Transactions.Length, Is.Zero);
        Assert.That(TownIntroTriggerUtility.IsPending(fixture.Save), Is.False);
    }

    [Test]
    public void FailedPickupKeepsTargetAndWaitsBeforeRetry()
    {
        using var fixture = new Fixture();
        Entity money = fixture.Target(InteractionKind.Drop);
        fixture.Tick(0);
        GameInteractionUtility.Complete(fixture.Manager, fixture.Runtime, money,
            InteractionResultCode.Failed, UnitSourceValue.None);
        fixture.Tick(0.1);
        Assert.That(fixture.Manager.Exists(money), Is.True);
        Assert.That(fixture.Transactions.Length, Is.Zero);
        fixture.Tick(1.2);
        Assert.That(fixture.Transactions.Length, Is.EqualTo(1));
    }

    [Test]
    public void PendingTransactionIsNotDuplicated()
    {
        using var fixture = new Fixture();
        fixture.Target(InteractionKind.Drop);
        fixture.Tick(0);
        uint request = fixture.Transactions[0].RequestId;
        fixture.Tick(3);
        Assert.That(fixture.Transactions.Length, Is.EqualTo(1));
        Assert.That(fixture.Transactions[0].RequestId, Is.EqualTo(request));
    }

    [Test]
    public void LongRunningFailureStillGetsAFullRetryDelay()
    {
        using var fixture = new Fixture();
        Entity money = fixture.Target(InteractionKind.Drop);
        fixture.Tick(0);
        fixture.Tick(3);
        GameInteractionUtility.Complete(fixture.Manager, fixture.Runtime, money,
            InteractionResultCode.Failed, UnitSourceValue.None);
        fixture.Tick(3.1);
        fixture.Tick(3.5);
        Assert.That(fixture.Transactions.Length, Is.Zero);
        fixture.Tick(4.2);
        Assert.That(fixture.Transactions.Length, Is.EqualTo(1));
    }

    [Test]
    public void StorySessionUsesRequestedBranchAndCancellationReleasesInputWithoutCompleting()
    {
        using var fixture = new Fixture();
        fixture.Variables.Set(TownIntroTriggerUtility.CompletionVariable, 0);
        NPCInteractionData story = fixture.Npc.Interactions[0];
        story.CompletionVariable = TownIntroTriggerUtility.CompletionVariable;
        story.EntryNodeGuid = "Dialogue";
        story.Nodes.Add(new NPCDialogueInteractionNodeData { Guid = "Dialogue" });
        fixture.Npc.Interactions.Insert(0, new NPCInteractionData { Key = "Manual" });
        Entity npc = fixture.Target(InteractionKind.Npc);
        fixture.Tick(0);
        var managed = fixture.World.GetOrCreateSystemManaged<StateScriptManagedCommandSystem>();
        typeof(StateScriptManagedCommandSystem).GetMethod("StartNpcInteraction",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(managed, new object[] { npc });
        Assert.That(managed.HasNpcInteraction, Is.True);
        Assert.That(fixture.Gate.IsPlayerInputLocked, Is.True);
        Assert.That(fixture.Transactions[0].Phase, Is.EqualTo(InteractionPhase.Processing));
        managed.CancelNpcInteraction(npc);
        Assert.That(managed.HasNpcInteraction, Is.False);
        Assert.That(fixture.Gate.IsPlayerInputLocked, Is.False);
        Assert.That(fixture.Variables.Get(TownIntroTriggerUtility.CompletionVariable), Is.Zero);
        Assert.That(fixture.Transactions[0].ResultCode, Is.EqualTo(InteractionResultCode.Cancelled));
    }

    [TestCase(GameWorldRole.Server, GameSceneMode.Town)]
    [TestCase(GameWorldRole.Standalone, GameSceneMode.Dungeon)]
    public void AutomaticStoriesDoNotStartOutsideLocalTown(GameWorldRole role, GameSceneMode scene)
    {
        using var fixture = new Fixture(role, scene);
        fixture.Target(InteractionKind.Npc);
        fixture.Tick(0);
        Assert.That(fixture.Transactions.Length, Is.Zero);
    }

    [Test]
    public void StoryAndUiInputGatesDelayAutomaticInteraction()
    {
        using var fixture = new Fixture();
        fixture.Target(InteractionKind.Npc);
        fixture.Gate.Lock(GameGateType.PlayerInput, "Test");
        fixture.Tick(0);
        Assert.That(fixture.Transactions.Length, Is.Zero);
        fixture.Gate.Unlock(GameGateType.PlayerInput, "Test");
        fixture.Tick(0.1);
        Assert.That(fixture.Transactions.Length, Is.EqualTo(1));
    }

    [Test]
    public void HiddenTargetRejectsManualRequestButAcceptsAutomaticRequest()
    {
        using var fixture = new Fixture();
        Entity target = fixture.Target(InteractionKind.Npc, hidden: true);
        Assert.That(GameInteractionUtility.TryRequest(fixture.Manager, fixture.Runtime, fixture.Player, target), Is.True);
        Assert.That(fixture.Transactions[0].ResultCode, Is.EqualTo(InteractionResultCode.InvalidTarget));
        GameInteractionUtility.AcknowledgeAutomatic(fixture.Manager, fixture.Runtime);
        Assert.That(fixture.Transactions.Length, Is.EqualTo(1), "Do not consume manual results.");
        GameInteractionUtility.Acknowledge(fixture.Manager, fixture.Runtime, fixture.Player);
        Assert.That(GameInteractionUtility.TryRequest(fixture.Manager, fixture.Runtime, fixture.Player, target, true, "Story"), Is.True);
        Assert.That(fixture.Transactions[0].Phase, Is.EqualTo(InteractionPhase.Pending));
    }

    [Test]
    public void IntroEntityHasNoManualPromptOrRenderingAndOnlyExistsForPendingSave()
    {
        using var fixture = new Fixture();
        fixture.Npc.NPC = TownIntroTriggerUtility.NpcName;
        Assert.That(TownIntroTriggerUtility.TryCreate(fixture.Manager, fixture.Player), Is.EqualTo(Entity.Null));
        fixture.Variables.Set(TownIntroTriggerUtility.CompletionVariable, 0);
        fixture.Variables.Set(NPCSequenceUtility.StageVariable, 0);
        Entity trigger = TownIntroTriggerUtility.TryCreate(fixture.Manager, fixture.Player);
        Assert.That(trigger, Is.Not.EqualTo(Entity.Null));
        Assert.That(fixture.Manager.GetComponentData<UnitInteractableComponent>(trigger).HidePrompt, Is.EqualTo(1));
        Assert.That(fixture.Manager.HasComponent<UnitInitializationPendingTag>(trigger), Is.True);
        Assert.That(fixture.Manager.GetComponentData<UnitStateScriptComponent>(trigger).UnitDataId,
            Is.EqualTo(TownIntroTriggerUtility.UnitDataId));
        Assert.That(fixture.Manager.HasComponent<LocalToWorld>(trigger), Is.False);
    }

    [Test]
    public void IntroDataCompilesAndEveryDialogueHasALocalizedActorAnchor()
    {
        string data = Path.Combine(Application.dataPath, "Res/Data");
        var stateRows = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(data, "StateScriptDataTable.json")))["Rows"];
        var script = stateRows.Single(row => (int)row["Id"] == TownIntroTriggerUtility.UnitDataId).ToObject<StateScriptData>();
        Assert.That(StateScriptCompiler.TryBuildRegistry(new[] { script }, out var registry, out string error), Is.True, error);
        registry.Dispose();
        var npcRows = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(data, "NPCDataTable.json")))["Rows"];
        NPCData npc = npcRows.Single(row => (string)row["NPC"] == TownIntroTriggerUtility.NpcName).ToObject<NPCData>();
        var localization = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(data, "LocalizationDataTable.json")))["Rows"];
        var keys = localization.Select(row => (string)row["Key"]).ToHashSet();
        var interaction = npc.Interactions.Single();
        Assert.That(interaction.Automatic, Is.True);
        Assert.That(interaction.CompletionVariable, Is.EqualTo(TownIntroTriggerUtility.CompletionVariable));
        var visited = new HashSet<string>();
        var queue = new Queue<NPCInteractionNodeData>();
        queue.Enqueue(interaction.GetEntryNode());
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!visited.Add(node.Guid)) continue;
            var dialogue = node as NPCDialogueInteractionNodeData;
            if (dialogue != null)
            {
                Assert.That(dialogue.SpeakerAnchor, Is.EqualTo(NPCDialogueAnchor.Actor));
                Assert.That(keys.Contains(dialogue.ContentKey), Is.True);
                Assert.That(string.IsNullOrEmpty(dialogue.Speaker) || keys.Contains(dialogue.Speaker), Is.True);
            }
            foreach (var branch in node.Branches)
            {
                var next = interaction.GetNode(branch.NextNodeGuid);
                Assert.That(next, Is.Not.Null, branch.NextNodeGuid);
                queue.Enqueue(next);
            }
        }
        Assert.That(visited.Count, Is.EqualTo(interaction.Nodes.Count));
    }

    [Test]
    public void SequenceFreesActorAndDoesNotRequeueItsInvisibleTarget()
    {
        using var fixture = new Fixture();
        var story = fixture.Npc.Interactions[0];
        story.IsSequence = true;
        story.EntryNodeGuid = "wait";
        story.Nodes.Add(new NPCWaitConditionInteractionNodeData { Guid = "wait", AllowInventory = true, Condition = NPCWaitCondition.Expression, Value = "never==1" });
        Entity trigger = fixture.Target(InteractionKind.Npc, hidden: true);
        fixture.Tick(0);
        var managed = fixture.World.GetOrCreateSystemManaged<StateScriptManagedCommandSystem>();
        typeof(StateScriptManagedCommandSystem).GetMethod("StartNpcInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(managed, new object[] { trigger });
        Assert.That(managed.HasNpcSession(trigger), Is.True);
        Assert.That(managed.BlocksInventoryInput, Is.False);
        Assert.That(fixture.Gate.IsPlayerInputLocked, Is.False);
        fixture.Tick(3);
        Assert.That(fixture.Transactions.Length, Is.Zero, "Active sequences must not submit a second automatic request.");
        Entity merchant = fixture.Target(InteractionKind.Npc);
        Assert.That(GameInteractionUtility.TryRequest(fixture.Manager, fixture.Runtime, fixture.Player, merchant), Is.True);
        Assert.That(fixture.Transactions[0].Phase, Is.EqualTo(InteractionPhase.Pending));
        managed.CancelNpcInteraction(trigger);
        Assert.That(fixture.Transactions[0].Phase, Is.EqualTo(InteractionPhase.Pending), "Cancelling a sequence must not complete the merchant transaction.");
    }

    [Test]
    public void RejectedGuideInteractionStillReturnsAnAcknowledgableResult()
    {
        using var fixture = new Fixture();
        Entity npc = fixture.Target(InteractionKind.Npc);
        Assert.That(GameInteractionUtility.TryRequest(fixture.Manager, fixture.Runtime, fixture.Player, npc, requestAllowed: false), Is.True);
        Assert.That(fixture.Transactions[0].ResultCode, Is.EqualTo(InteractionResultCode.InvalidTarget));
        GameInteractionUtility.Acknowledge(fixture.Manager, fixture.Runtime, fixture.Player);
        Assert.That(fixture.Transactions.Length, Is.Zero);
    }

    [Test]
    public void FailedProgressSaveRollsBackAndLegacyIntroFlagsDoNotStartTheNewSequence()
    {
        using var fixture = new Fixture();
        fixture.Variables.Set(TownIntroTriggerUtility.CompletionVariable, 0);
        Assert.That(TownIntroTriggerUtility.IsPending(fixture.Save), Is.False);
        fixture.Variables.Set(NPCSequenceUtility.StageVariable, 2);
        Assert.That(TownIntroTriggerUtility.IsPending(fixture.Save), Is.True);
        typeof(SaveDataComponent).GetField("_currentSaveIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fixture.Save, -1);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[SaveDataComponent] Save failed: no save slot selected.");
        Assert.That(NPCSequenceUtility.SaveProgress(NPCSequenceUtility.StageVariable, 3), Is.False);
        Assert.That(fixture.Variables.Get(NPCSequenceUtility.StageVariable), Is.EqualTo(2));
    }

    [Test]
    public void SceneExitCanCancelAnotherSessionWhileNpcSessionsAreTicking()
    {
        using var fixture = new Fixture();
        var story = fixture.Npc.Interactions[0];
        story.IsSequence = true;
        story.EntryNodeGuid = "wait";
        story.Nodes.Add(new NPCWaitConditionInteractionNodeData { Guid = "wait" });
        Entity trigger = fixture.Target(InteractionKind.Npc, hidden: true);
        fixture.Tick(0);
        var managed = fixture.World.GetOrCreateSystemManaged<StateScriptManagedCommandSystem>();
        var start = typeof(StateScriptManagedCommandSystem).GetMethod("StartNpcInteraction", BindingFlags.Instance | BindingFlags.NonPublic);
        start.Invoke(managed, new object[] { trigger });
        GameInteractionUtility.AcknowledgeAutomatic(fixture.Manager, fixture.Runtime);
        Entity merchant = fixture.Target(InteractionKind.Npc);
        GameInteractionUtility.TryRequest(fixture.Manager, fixture.Runtime, fixture.Player, merchant, true, "Story");
        start.Invoke(managed, new object[] { merchant });
        var factory = (NPCInteractionNodeRunnerFactory)typeof(StateScriptManagedCommandSystem).GetField("_npcRunnerFactory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(managed);
        factory.Register(typeof(NPCWaitConditionInteractionNodeData), _ => new ExitDuringTickRunner(managed, merchant, trigger));
        Assert.DoesNotThrow(() => typeof(StateScriptManagedCommandSystem).GetMethod("TickNpcSessions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(managed, new object[] { .1f }));
        Assert.That(managed.HasNpcSession(trigger), Is.False);
        Assert.That(managed.HasNpcSession(merchant), Is.True);
        managed.CancelNpcInteraction(merchant);
    }

    [Test]
    public void FirstVisitMerchantDialogueOnlyRunsAtItsTutorialStageAndOnlyOnce()
    {
        using var fixture = new Fixture();
        fixture.Variables.Set("intro_started", 1);
        fixture.Variables.Set("intro_completed", 0);
        var rows = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Res/Data/NPCDataTable.json")))["Rows"];
        foreach (var tuple in new[] { (Name: "NPCEquipShop", Stage: 1, Spoken: "intro_equip_spoken"), (Name: "NPCPropShop", Stage: 2, Spoken: "intro_prop_spoken"), (Name: "NPCSkillShop", Stage: 3, Spoken: "intro_skill_spoken"), (Name: "NPCTraining", Stage: 5, Spoken: "intro_trainer_spoken") })
        {
            var npc = rows.Single(r => (string)r["NPC"] == tuple.Name).ToObject<NPCData>();
            fixture.Variables.Set("intro_stage", tuple.Stage);
            fixture.Variables.Set(tuple.Spoken, 0);
            Assert.That(npc.GetEnabledInteractions().First().Key.StartsWith("Intro"), Is.True);
            fixture.Variables.Set(tuple.Spoken, 1);
            Assert.That(npc.GetEnabledInteractions().First().Key, Is.EqualTo(npc.Interactions[1].Key));
            fixture.Variables.Set(tuple.Spoken, 0);
            fixture.Variables.Set("intro_started", 0);
            Assert.That(npc.GetEnabledInteractions().First().Key, Is.EqualTo(npc.Interactions[1].Key), "Legacy saves use the existing merchant interaction.");
            fixture.Variables.Set("intro_started", 1);
        }
    }

    [Test]
    public void TutorialInteractionAllowsOnlyTheCurrentMerchantUntilCompletion()
    {
        using var fixture = new Fixture();
        fixture.Variables.Set("intro_completed", 0);
        fixture.Variables.Set("intro_stage", 1);
        fixture.Npc.NPC = "NPCEquipShop";
        Entity target = fixture.Target(InteractionKind.Npc);
        Assert.That(NPCSequenceUtility.AllowsInteraction(fixture.Manager, target), Is.True);
        fixture.Npc.NPC = "NPCPropShop";
        Assert.That(NPCSequenceUtility.AllowsInteraction(fixture.Manager, target), Is.False);
        fixture.Variables.Set("intro_stage", 2);
        Assert.That(NPCSequenceUtility.AllowsInteraction(fixture.Manager, target), Is.True);
        fixture.Variables.Set("intro_stage", 4);
        Assert.That(NPCSequenceUtility.AllowsInteraction(fixture.Manager, target), Is.False);
        fixture.Variables.Set("intro_completed", 1);
        Assert.That(NPCSequenceUtility.AllowsInteraction(fixture.Manager, target), Is.True);
    }

    private sealed class ExitDuringTickRunner : NPCInteractionNodeRunner
    {
        private readonly StateScriptManagedCommandSystem _managed;
        private readonly Entity _merchant, _trigger;
        public ExitDuringTickRunner(StateScriptManagedCommandSystem managed, Entity merchant, Entity trigger) { _managed = managed; _merchant = merchant; _trigger = trigger; }
        public override void Enter(NPCInteractionSession session) { if (session.Target == _merchant) _managed.CancelNpcInteraction(_trigger); }
        public override bool IsCompleted(NPCInteractionSession session) => false;
    }

    private sealed class Fixture : IDisposable
    {
        public readonly World World = new("Automatic interaction tests");
        private readonly GameObject _root = new("Automatic interaction test components");
        private readonly AutoInteractionSystem _system;
        public EntityManager Manager => World.EntityManager;
        public readonly Entity Runtime;
        public readonly Entity Player;
        public readonly NPCData Npc = new() { Id = 7, Interactions = new() { new NPCInteractionData { Key = "Story", Automatic = true } } };
        public readonly SaveVariableData Variables = new();
        public readonly SaveDataComponent Save;
        public readonly GameGateComponent Gate;
        public DynamicBuffer<InteractionTransactionElement> Transactions => Manager.GetBuffer<InteractionTransactionElement>(Runtime);

        public Fixture(GameWorldRole role = GameWorldRole.Standalone, GameSceneMode scene = GameSceneMode.Town)
        {
            var data = _root.AddComponent<DataComponent>();
            Save = _root.AddComponent<SaveDataComponent>();
            Gate = _root.AddComponent<GameGateComponent>();
            _root.AddComponent<TransitionComponent>();
            _root.AddComponent<EventComponent>();
            typeof(SaveDataComponent).GetField("_variables", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Save, Variables);
            var tables = (Dictionary<Type, object>)typeof(DataComponent).GetField("_tables", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
            var table = new DataTable<NPCData>();
            table.Add(Npc);
            tables[typeof(NPCData)] = table;
            GameSingletonUtility.Create(Manager, role, scene);
            Runtime = GameSingletonUtility.GetEntity<GameInteractionComponent>(Manager);
            Player = Manager.CreateEntity(typeof(PlayerInputComponent), typeof(UnitStateScriptComponent), typeof(LocalTransform));
            Manager.SetComponentData(Player, LocalTransform.Identity);
            if (scene is GameSceneMode.Dungeon or GameSceneMode.Training)
                Manager.AddComponent<UnitSkillReleaseComponent>(Player);
            _system = World.GetOrCreateSystemManaged<AutoInteractionSystem>();
        }

        public Entity Target(InteractionKind kind, bool hidden = false)
        {
            Entity target = Manager.CreateEntity(typeof(UnitInteractableComponent), typeof(LocalTransform));
            Manager.SetComponentData(target, LocalTransform.Identity);
            Manager.SetComponentData(target, new UnitInteractableComponent
            {
                Data = kind == InteractionKind.Drop ? UnitInteractionData.CreateDrop(DropRewardType.Money, 0, 5) :
                    new UnitInteractionData { Kind = kind, DataId = Npc.Id },
                RangeSq = 9, IsEnabled = 1, HidePrompt = hidden ? (byte)1 : (byte)0,
            });
            return target;
        }

        public void Tick(double now)
        {
            World.SetTime(new TimeData(now, 0.033f));
            _system.Update();
            Manager.CompleteAllTrackedJobs();
        }

        public void Dispose()
        {
            Gate.Unlock(GameGateType.PlayerInput, "Test");
            World.Dispose();
            UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
