using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed class NotificationTests
{
    private static NotificationRequest Threat(float from, float to, int cycle = 0, int scope = 1) =>
        new("threat", "Threat", "threat", NotificationPolicy.Merge, from, to, scope: scope, cycle: cycle);

    [Test]
    public void StackIsBoundedAndCompletionDoesNotRemoveOtherItems()
    {
        var queue = new NotificationQueue();
        for (int i = 0; i < 10; i++)
            queue.Enqueue(new NotificationRequest("pickup", i.ToString(), "pickup", NotificationPolicy.Stack, maxVisible: 3));
        Assert.That(queue.Active.Select(x => x.Request.Text), Is.EqualTo(new[] { "7", "8", "9" }));
        queue.Complete(queue.Active[1].Id);
        Assert.That(queue.Active.Select(x => x.Request.Text), Is.EqualTo(new[] { "7", "9" }));
    }

    [Test]
    public void SameCycleMergesInPlaceWithoutRestartingTheViewInstance()
    {
        var queue = new NotificationQueue();
        queue.Enqueue(Threat(20, 30));
        var record = queue.Active[0];
        queue.Enqueue(Threat(30, 40));
        Assert.That(queue.Active.Count, Is.EqualTo(1));
        Assert.That(queue.Active[0], Is.SameAs(record));
        Assert.That(record.Revision, Is.EqualTo(1));
        Assert.That(record.Request.To, Is.EqualTo(40));
    }

    [Test]
    public void ResetQueuesNextCycleAndPreservesItsEarliestStartingValue()
    {
        var queue = new NotificationQueue();
        queue.Enqueue(Threat(95, 103));
        int first = queue.Active[0].Id;
        queue.Enqueue(Threat(0, 8, 1));
        queue.Enqueue(Threat(8, 21, 1));
        Assert.That(queue.Active[0].Request.To, Is.EqualTo(103));
        Assert.That(queue.PendingCount, Is.EqualTo(1));
        queue.Complete(first);
        Assert.That(queue.Active[0].Request.From, Is.Zero);
        Assert.That(queue.Active[0].Request.To, Is.EqualTo(21));
        Assert.That(queue.Active[0].Request.Cycle, Is.EqualTo(1));
    }

    [Test]
    public void ChannelsAreIndependentAndNewFloorClearsOldPendingNotices()
    {
        var queue = new NotificationQueue();
        queue.Enqueue(new NotificationRequest("pickup", "Bat wing", "pickup", NotificationPolicy.Stack));
        queue.Enqueue(Threat(0, 3));
        Assert.That(queue.Active.Count, Is.EqualTo(2), "First scoped signal must not erase a concurrent pickup.");
        queue.Enqueue(Threat(0, 8, 1));
        queue.Enqueue(new NotificationRequest("revenge", "Revenge", "revenge", NotificationPolicy.Queue, scope: 1));
        Assert.That(queue.Active.Count, Is.EqualTo(3));
        queue.Enqueue(Threat(0, 3, scope: 2));
        Assert.That(queue.Active.Count, Is.EqualTo(1));
        Assert.That(queue.PendingCount, Is.Zero);
        queue.Clear();
        Assert.That(queue.Active, Is.Empty);
    }

    [Test]
    public void QueuedTextIsBoundedAndPromotedInOrder()
    {
        var queue = new NotificationQueue();
        for (int i = 0; i < 5; i++)
            queue.Enqueue(new NotificationRequest("revenge", i.ToString(), "revenge", NotificationPolicy.Queue, maxQueued: 2));
        Assert.That(queue.PendingCount, Is.EqualTo(2));
        Assert.That(queue.Active[0].Request.Text, Is.EqualTo("0"));
        queue.Complete(queue.Active[0].Id);
        Assert.That(queue.Active[0].Request.Text, Is.EqualTo("3"));
        queue.Complete(queue.Active[0].Id);
        Assert.That(queue.Active[0].Request.Text, Is.EqualTo("4"));
    }

    [Test]
    public void EmptyRequestsAreIgnored()
    {
        var queue = new NotificationQueue();
        queue.Enqueue(default);
        Assert.That(queue.Active, Is.Empty);
        Assert.That(queue.PendingCount, Is.Zero);
    }

    [Test]
    public void AuthoredPrefabsHaveBindingsNonBlockingInputAndDistinctMotions()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/NotificationUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        var panel = prefab.GetComponent<NotificationUI>();
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.CanCloseByEscape, Is.False);
        Assert.That(prefab.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        Assert.That(panel.Templates.Select(t => t.Key), Is.EquivalentTo(new[] { "pickup", "threat", "revenge" }));
        foreach (var template in panel.Templates)
        {
            Assert.That(template.Prefab, Is.Not.Null, template.Key);
            var data = new NotificationItemData();
            data.Bind(template.Prefab.transform);
            Assert.That(data.Root.CanvasGroup, Is.Not.Null, template.Key);
            Assert.That(data.Root.CanvasGroup.blocksRaycasts, Is.False, template.Key);
            Assert.That(data.Label.TextMeshProUGUI, Is.Not.Null, template.Key);
            Assert.That(data.Label.TextMeshProUGUI.font, Is.Not.Null, template.Key);
            Assert.That(template.Prefab.Motion.HoldCurve.length, Is.EqualTo(2));
        }
        var pickup = panel.FindTemplate("pickup");
        Assert.That(pickup.Policy, Is.EqualTo(NotificationPolicy.Stack));
        Assert.That(pickup.Prefab.Motion.FadeDuringHold, Is.True);
        Assert.That(pickup.Prefab.Motion.HoldOffset.y, Is.GreaterThan(0));
        foreach (string key in new[] { "threat", "revenge" })
        {
            var motion = panel.FindTemplate(key).Prefab.Motion;
            Assert.That(motion.EnterOffset, Is.EqualTo(Vector2.zero));
            Assert.That(motion.HoldOffset, Is.EqualTo(Vector2.zero));
            Assert.That(motion.ExitOffset, Is.EqualTo(Vector2.zero));
            Assert.That(motion.ExitSeconds, Is.GreaterThan(0));
        }
        Assert.That(panel.FindTemplate("threat").Prefab, Is.TypeOf<ProgressNotificationItem>());
        Assert.That(panel.FindTemplate("threat").Prefab.UI.Fill.Image, Is.Not.Null);
    }

    [Test]
    public void FailedSpawnIsSilentSuccessfulSpawnSendsOneReplicatedSignal()
    {
        using var world = new World("Notification replication test");
        EntityManager manager = world.EntityManager;
        GameSingletonUtility.Create(manager, GameWorldRole.Server, GameSceneMode.Dungeon);
        Entity floor = manager.CreateEntity(typeof(DungeonFloorControllerComponent));
        Assert.That(NotificationSignalUtility.PublishSpawned(manager, floor, "revenge", 0), Is.False);
        using var query = manager.CreateEntityQuery(ComponentType.ReadOnly<NetworkPresentationEventQueueComponent>());
        Assert.That(query.IsEmptyIgnoreFilter, Is.True);
        Assert.That(NotificationSignalUtility.PublishSpawned(manager, floor, "revenge", 4), Is.True);
        var queue = manager.GetComponentObject<NetworkPresentationEventQueueComponent>(query.GetSingletonEntity());
        Assert.That(queue.Events.Count, Is.EqualTo(1));
        var sent = queue.Events[0];
        Assert.That(sent.eventType, Is.EqualTo(ClientPresentationEventType.Notification));
        Assert.That(sent.assetName, Is.EqualTo("revenge"));
        Assert.That(sent.valueB, Is.EqualTo(4));
        Assert.That(sent.intValue, Is.Not.Zero);
        Assert.That(sent.sequence, Is.GreaterThan(0));
        var copy = JsonConvert.DeserializeObject<NetworkPresentationEventStateData>(JsonConvert.SerializeObject(sent));
        Assert.That(copy.assetName, Is.EqualTo(sent.assetName));
        Assert.That(copy.intValue, Is.EqualTo(sent.intValue));
        Assert.That(copy.valueB, Is.EqualTo(sent.valueB));
    }

    [Test]
    public void ClientCannotEmitDuplicateAuthoritativeSignals()
    {
        using var world = new World("Notification client authority test");
        EntityManager manager = world.EntityManager;
        GameSingletonUtility.Create(manager, GameWorldRole.Client, GameSceneMode.Dungeon);
        Assert.That(NotificationSignalUtility.Publish(manager, Entity.Null, "threat", new float3(0, 8, 0)), Is.False);
        Assert.That(NotificationSignalUtility.PublishSpawned(manager, Entity.Null, "revenge", 4), Is.False);
    }
}
