using System;
using System.Reflection;
using CrystalMagic.UI;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

public sealed class BattleUIChantTests
{
    private static readonly MethodInfo ReadProgress = typeof(BattleUIModel).GetMethod(
        "TryGetChantProgress", BindingFlags.Static | BindingFlags.NonPublic);

    [TestCase(StateScriptStateStatus.Pending, 0f, 8f, true, 0f)]
    [TestCase(StateScriptStateStatus.Running, 2f, 8f, true, 0.25f)]
    [TestCase(StateScriptStateStatus.Running, 8f, 8f, true, 1f)]
    [TestCase(StateScriptStateStatus.Running, 9f, 8f, true, 1f)]
    [TestCase(StateScriptStateStatus.Stop, 8f, 8f, false, 0f)]
    [TestCase(StateScriptStateStatus.Running, 0f, 0f, false, 0f)]
    public void ReadsEvaluatedChantTimerAndIgnoresOtherTimers(
        StateScriptStateStatus status, float elapsed, float duration, bool visible, float progress)
    {
        using ChantWorld fixture = new();
        fixture.SetTimer(status, elapsed, duration);
        AssertProgress(fixture, visible, progress);
    }

    [Test]
    public void InterruptionDeathAndInactiveGraphHideTheBar()
    {
        using ChantWorld fixture = new();
        fixture.SetTimer(StateScriptStateStatus.Running, 1f, 2f);
        AssertProgress(fixture, true, 0.5f);

        fixture.SetCasting(false);
        AssertProgress(fixture, false, 0f);
        fixture.SetCasting(true);

        UnitStateScriptComponent component = fixture.Manager.GetComponentData<UnitStateScriptComponent>(fixture.Player);
        component.IsStoppedForDeath = 1;
        fixture.Manager.SetComponentData(fixture.Player, component);
        AssertProgress(fixture, false, 0f);
        component.IsStoppedForDeath = 0;
        fixture.Manager.SetComponentData(fixture.Player, component);

        DynamicBuffer<StateScriptGraphStateElement> graphs = fixture.Manager.GetBuffer<StateScriptGraphStateElement>(fixture.Player);
        graphs[0] = default;
        AssertProgress(fixture, false, 0f);
    }

    [Test]
    public void NextSkillRestartsProgressFromItsOwnEvaluatedDuration()
    {
        using ChantWorld fixture = new();
        fixture.SetTimer(StateScriptStateStatus.Running, 2f, 4f);
        AssertProgress(fixture, true, 0.5f);
        fixture.SetTimer(StateScriptStateStatus.Pending, 0f, 0.5f);
        AssertProgress(fixture, true, 0f);
        fixture.SetTimer(StateScriptStateStatus.Running, 0.25f, 0.5f);
        AssertProgress(fixture, true, 0.5f);
    }

    [Test]
    public void MissingRuntimeRegistryHidesTheBar()
    {
        using ChantWorld fixture = new();
        fixture.SetTimer(StateScriptStateStatus.Running, 1f, 2f);
        fixture.Manager.DestroyEntity(fixture.RegistryEntity);
        AssertProgress(fixture, false, 0f);
    }

    private static void AssertProgress(ChantWorld fixture, bool visible, float progress)
    {
        object[] arguments = { fixture.Manager, fixture.Player, 0f };
        Assert.That(ReadProgress.Invoke(null, arguments), Is.EqualTo(visible));
        Assert.That((float)arguments[2], Is.EqualTo(progress).Within(0.00001f));
    }

    private sealed class ChantWorld : IDisposable
    {
        private readonly World _world = new("BattleUI chant tests");
        private readonly BlobAssetReference<StateScriptRuntimeRegistryBlob> _registry;
        public EntityManager Manager => _world.EntityManager;
        public Entity Player { get; }
        public Entity RegistryEntity { get; }

        public ChantWorld()
        {
            using (BlobBuilder builder = new(Allocator.Temp))
            {
                ref StateScriptRuntimeRegistryBlob root = ref builder.ConstructRoot<StateScriptRuntimeRegistryBlob>();
                BlobBuilderArray<StateScriptUnitDefinitionBlob> units = builder.Allocate(ref root.Units, 1);
                BlobBuilderArray<StateScriptGraphDefinitionBlob> graphs = builder.Allocate(ref units[0].Graphs, 1);
                ref StateScriptGraphDefinitionBlob graph = ref graphs[0];
                BlobBuilderArray<StateScriptNodeDefinition> nodes = builder.Allocate(ref graph.Nodes, 2);
                BlobBuilderArray<int> indices = builder.Allocate(ref graph.StateNodeIndices, 2);
                BlobBuilderArray<BehaviorExpressionBlob> expressions = builder.Allocate(ref graph.Expressions, 2);
                for (int i = 0; i < 2; i++)
                {
                    nodes[i] = new StateScriptNodeDefinition
                    {
                        Type = StateScriptNodeRuntimeType.Timer,
                        ExpressionStart = i,
                        ExpressionCount = 1,
                    };
                    indices[i] = i;
                    BlobBuilderArray<ExpressionInstruction> instructions = builder.Allocate(ref expressions[i].Instructions, 1);
                    instructions[0] = new ExpressionInstruction
                    {
                        Kind = i == 0 ? ExpressionInstructionKind.Literal : ExpressionInstructionKind.Source,
                        SourceId = i == 0 ? default : UnitSourceId.PlayerSkillGetSkillChantDuration,
                    };
                }
                _registry = builder.CreateBlobAssetReference<StateScriptRuntimeRegistryBlob>(Allocator.Persistent);
            }

            RegistryEntity = Manager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent));
            Manager.SetComponentData(RegistryEntity, new StateScriptRuntimeRegistryComponent { Value = _registry });
            Player = Manager.CreateEntity(typeof(UnitStateScriptComponent), typeof(UnitVariableComponent));
            Manager.SetComponentData(Player, new UnitStateScriptComponent { DefinitionIndex = 0 });
            Manager.AddBuffer<UnitVariableElement>(Player);
            Manager.AddBuffer<StateScriptGraphStateElement>(Player).Add(new StateScriptGraphStateElement { IsActive = 1 });
            DynamicBuffer<StateScriptNodeStateElement> states = Manager.AddBuffer<StateScriptNodeStateElement>(Player);
            states.Add(new StateScriptNodeStateElement { Status = StateScriptStateStatus.Running, Time = 9f, Auxiliary = 10f });
            states.Add(default);
            SetCasting(true);
        }

        public void SetCasting(bool casting)
        {
            DynamicBuffer<UnitVariableElement> variables = Manager.GetBuffer<UnitVariableElement>(Player);
            variables.Clear();
            variables.Add(new UnitVariableElement
            {
                Key = PlayerCurrentSkillUtility.CastingVariableKey,
                Value = UnitSourceValue.FromBool(casting),
            });
        }

        public void SetTimer(StateScriptStateStatus status, float elapsed, float duration)
        {
            DynamicBuffer<StateScriptNodeStateElement> states = Manager.GetBuffer<StateScriptNodeStateElement>(Player);
            states[1] = new StateScriptNodeStateElement
            {
                Status = status,
                Time = elapsed,
                Auxiliary = duration,
            };
        }

        public void Dispose()
        {
            _world.Dispose();
            _registry.Dispose();
        }
    }
}
