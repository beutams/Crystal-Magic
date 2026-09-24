using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class PlayerInputEventTests
{
    [Test]
    public void ApplyAndStateScriptPreserveRepeatedEventsAndTheirInputSnapshots()
    {
        using InputWorld fixture = new();
        fixture.Apply(new NetworkPlayerInputStateData { pointerX = 99 });
        fixture.Apply(new NetworkPrimaryPressData { pointerX = 1 });
        fixture.Apply(new NetworkInteractData());
        fixture.Apply(new NetworkSkillChainSelectData { skillChainIndex = 3 });
        fixture.Apply(new NetworkPrimaryPressData { pointerX = 2 });
        fixture.Apply(new NetworkPrimaryPressData { pointerX = 3 });
        fixture.Apply(new NetworkInteractData());

        DynamicBuffer<PlayerInputEventElement> events = fixture.Manager.GetBuffer<PlayerInputEventElement>(fixture.Player);
        Assert.That(events.Length, Is.EqualTo(6));
        Assert.That(events[0].Input.SkillChainIndex, Is.Zero);
        Assert.That(events[3].Input.SkillChainIndex, Is.EqualTo(3));
        Assert.That(fixture.Manager.GetComponentData<PlayerInputComponent>(fixture.Player).IsPrimaryHeld, Is.Zero);

        fixture.Update();
        DynamicBuffer<StateScriptManagedCommandElement> commands = fixture.Commands;
        Assert.That(commands.Length, Is.EqualTo(6));
        float[] expected = { 1, 99, 3, 2, 3, 99 };
        for (int index = 0; index < expected.Length; index++)
        {
            if (index == 2)
                Assert.That(commands[index].Value.Int, Is.EqualTo(3));
            else
                Assert.That(commands[index].Value.Float3.x, Is.EqualTo(expected[index]));
        }
        PlayerInputComponent current = fixture.Manager.GetComponentData<PlayerInputComponent>(fixture.Player);
        Assert.That(current.PointerWorldPosition.x, Is.EqualTo(99));
        Assert.That(current.SkillChainIndex, Is.EqualTo(3));
        Assert.That(current.IsPrimaryHeld, Is.Zero);
        fixture.ClearEvents();
        fixture.Update();
        Assert.That(fixture.Commands.Length, Is.Zero, "上一帧的事件不能再次执行");
    }

    [Test]
    public void HeldInputDoesNotDuplicateClicksAndEventsDoNotAdvanceTimersMultipleTimes()
    {
        using InputWorld fixture = new(repeatWhileHeld: true);
        fixture.Apply(new NetworkPlayerInputStateData { isPrimaryHeld = 1 });
        fixture.Apply(new NetworkPrimaryPressData());
        fixture.Apply(new NetworkPrimaryPressData());
        fixture.Update();
        Assert.That(fixture.Commands.Length, Is.EqualTo(2));
        fixture.ClearEvents();
        fixture.Update();
        Assert.That(fixture.Commands.Length, Is.EqualTo(1), "持续按住每帧补一次，不复用上一帧点击");
        Assert.That(fixture.TimerTime, Is.EqualTo(0.033f).Within(0.00001f));
        fixture.ClearEvents();
        for (int index = 0; index < 5; index++)
            fixture.Apply(new NetworkPrimaryPressData());
        fixture.Update();
        Assert.That(fixture.Commands.Length, Is.EqualTo(5));
        Assert.That(fixture.TimerTime, Is.EqualTo(0.066f).Within(0.00001f));
    }

    [Test]
    public void ConfiguredInputGraphsHaveValidEventPorts()
    {
        Table table = JsonConvert.DeserializeObject<Table>(File.ReadAllText(
            Path.Combine(Directory.GetCurrentDirectory(), "Assets/Res/Data/StateScriptDataTable.json")));
        int listeners = 0;
        foreach (StateScriptData row in table.Rows)
        {
            row.EnsureValid();
            foreach (StateScriptInstanceData graph in row.Graphs)
            {
                Dictionary<string, StateScriptNodeData> nodes = graph.Nodes.ToDictionary(node => node.Guid);
                listeners += graph.Nodes.Count(node => node is PlayerInputEventStateScriptNodeData);
                foreach (StateScriptEdgeData edge in graph.Edges)
                {
                    Assert.That(StateScriptNodeSchemaUtility.Create(nodes[edge.OutputNodeGuid]).Outputs,
                        Does.Contain(edge.OutputPortName), graph.Name + " output " + edge.OutputNodeGuid);
                    Assert.That(StateScriptNodeSchemaUtility.Create(nodes[edge.InputNodeGuid]).Inputs,
                        Does.Contain(edge.InputPortName), graph.Name + " input " + edge.InputNodeGuid);
                }
            }
        }
        Assert.That(listeners, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void ConfiguredInputGraphsCompileWithEventListeners()
    {
        Table table = JsonConvert.DeserializeObject<Table>(File.ReadAllText(
            Path.Combine(Application.dataPath, "Res/Data/StateScriptDataTable.json")));
        bool success = StateScriptCompiler.TryBuildRegistry(table.Rows, out var registry, out string error);
        try { Assert.That(success, Is.True, error); }
        finally { if (registry.IsCreated) registry.Dispose(); }
    }

    private sealed class Table
    {
        public List<StateScriptData> Rows { get; set; }
    }

    private sealed class InputWorld : IDisposable
    {
        private readonly World world = new("Input event tests");
        private readonly Guid unitId = Guid.NewGuid();
        private readonly Unity.Entities.BlobAssetReference<StateScriptRuntimeRegistryBlob> registry;
        private readonly StateScriptSystem system;
        private readonly int timerIndex;
        public EntityManager Manager => world.EntityManager;
        public Entity Player { get; }
        public DynamicBuffer<StateScriptManagedCommandElement> Commands => Manager.GetBuffer<StateScriptManagedCommandElement>(Player);
        public float TimerTime => Manager.GetBuffer<StateScriptNodeStateElement>(Player)[timerIndex].Time;

        public InputWorld(bool repeatWhileHeld = false)
        {
            GameSingletonUtility.Create(Manager, GameWorldRole.Standalone, GameSceneMode.Dungeon);
            StateScriptInstanceData graph = new() { Guid = "input-graph", Name = "Input", EntryNodeGuid = "entry" };
            graph.Nodes.Add(new StateScriptEntryNodeData { Guid = "entry" });
            foreach (PlayerInputOperationType type in new[]
                     { PlayerInputOperationType.PrimaryPressed, PlayerInputOperationType.Interact, PlayerInputOperationType.SelectSkillChain })
            {
                string key = type.ToString();
                graph.Nodes.Add(new PlayerInputEventStateScriptNodeData
                {
                    Guid = key, EventType = type, RepeatWhileHeld = repeatWhileHeld,
                });
                graph.Nodes.Add(new PublishGameEventStateScriptNodeData
                {
                    Guid = key + "-publish", EventName = key,
                    Reference = new ValueExpression
                    {
                        Kind = ValueExpressionKind.Getter,
                        GetterKey = type == PlayerInputOperationType.SelectSkillChain
                            ? "player.input.skillChainIndex" : "player.input.pointerWorldPosition",
                    },
                });
                graph.Edges.Add(new StateScriptEdgeData
                {
                    OutputNodeGuid = "entry", OutputPortName = "Out", InputNodeGuid = key, InputPortName = "Start",
                });
                graph.Edges.Add(new StateScriptEdgeData
                {
                    OutputNodeGuid = key, OutputPortName = "OnEvent", InputNodeGuid = key + "-publish", InputPortName = "In",
                });
            }
            timerIndex = graph.Nodes.Count;
            graph.Nodes.Add(new TimerStateScriptNodeData
            {
                Guid = "timer", Duration = new ValueExpression { Literal = UnitValue.FromFloat(10) },
            });
            graph.Edges.Add(new StateScriptEdgeData
            {
                OutputNodeGuid = "entry", OutputPortName = "Out", InputNodeGuid = "timer", InputPortName = "Start",
            });
            StateScriptData data = new() { Id = 1, Graphs = new List<StateScriptInstanceData> { graph } };
            Assert.That(StateScriptCompiler.TryBuildRegistry(new[] { data }, out registry, out string error), Is.True, error);
            Entity registryEntity = Manager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent));
            Manager.SetComponentData(registryEntity, new StateScriptRuntimeRegistryComponent { Value = registry });
            Player = Manager.CreateEntity(typeof(PlayerInputComponent), typeof(NetworkIdentityComponent), typeof(UnitStateScriptComponent));
            Manager.SetComponentData(Player, new NetworkIdentityComponent { id = unitId });
            Manager.SetComponentData(Player, new UnitStateScriptComponent { DefinitionIndex = 0, UnitDataId = 1 });
            Manager.AddBuffer<PlayerInputEventElement>(Player);
            Manager.AddBuffer<StateScriptGraphStateElement>(Player).Add(default);
            Manager.AddBuffer<StateScriptNodeStateElement>(Player).ResizeUninitialized(graph.Nodes.Count);
            DynamicBuffer<StateScriptNodeStateElement> nodes = Manager.GetBuffer<StateScriptNodeStateElement>(Player);
            for (int index = 0; index < nodes.Length; index++) nodes[index] = default;
            Manager.AddBuffer<StateScriptSourceCommandElement>(Player);
            Manager.AddBuffer<StateScriptSourceCommandArgumentElement>(Player);
            Manager.AddBuffer<StateScriptManagedCommandElement>(Player);
            Manager.AddBuffer<StateScriptExternalResultElement>(Player);
            system = world.GetOrCreateSystemManaged<StateScriptSystem>();
            world.SetTime(new TimeData(0.033, 0.033f));
        }

        public void Apply(NetworkStateData input)
        {
            input.unitId = unitId;
            input.Apply(new NetworkStateApplyContext(Manager, 98, 33));
        }

        public void Update()
        {
            system.Update();
            Manager.CompleteAllTrackedJobs();
        }

        public void ClearEvents() => Manager.GetBuffer<PlayerInputEventElement>(Player).Clear();

        public void Dispose()
        {
            world.Dispose();
            if (registry.IsCreated) registry.Dispose();
        }
    }
}
