using System.IO;
using System.Linq;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class ShieldBlockCooldownTests
{
    private const string CooldownKey = "ai.block.coolingDown";
    private static JArray Rows(string name) =>
        (JArray)JObject.Parse(File.ReadAllText($"Assets/Res/Data/{name}DataTable.json"))["Rows"];

    [TestCase(26, 10f)]
    [TestCase(14, 8f)]
    [TestCase(19, 6f)]
    [TestCase(24, 5f)]
    public void MonsterBlockCooldownStartsOnReactionAndCannotBeBypassedByChangingState(int id, float seconds)
    {
        JToken rawScript = Rows("StateScript").Single(row => (int)row["Id"] == id);
        StateScriptData script = rawScript.ToObject<StateScriptData>(
            JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
        StateScriptInstanceData guard = script.Graphs.Single(graph => graph.Name == "Shield Guard");
        StateScriptInstanceData reaction = script.Graphs.Single(graph => graph.Name == "Block Reaction");
        var timer = reaction.Nodes.OfType<TimerStateScriptNodeData>().Single(node => node.Guid.EndsWith("block_cooldown_timer"));
        Assert.That(timer.Duration.Literal.Float, Is.EqualTo(seconds));
        Assert.That(rawScript["Graphs"].Single(graph => (string)graph["Name"] == "Block Reaction")["ExecutionConditions"].ToString(),
            Does.Not.Contain("\"ai."),
            "Keep the existing summon-arrival gate, but never pause the cooldown for AI state changes.");
        Assert.That(reaction.Edges.Where(edge => edge.InputNodeGuid == timer.Guid).Select(edge => edge.InputPortName),
            Is.EquivalentTo(new[] { "Start" }), "No combat transition may abort/reset the cooldown timer.");
        var start = reaction.Nodes.OfType<SetValueStateScriptNodeData>().Single(node => node.Key == CooldownKey && node.Value.Literal.Bool);
        var ready = reaction.Nodes.OfType<SetValueStateScriptNodeData>().Single(node => node.Key == CooldownKey && !node.Value.Literal.Bool);
        Assert.That(reaction.Edges.Any(edge => edge.OutputPortName == "OnChangeTrue" && edge.InputNodeGuid == start.Guid), Is.True);
        AssertEdge(reaction, start.Guid, "Out", timer.Guid, "Start");
        AssertEdge(reaction, timer.Guid, "OnComplete", ready.Guid, "In");
        var remove = reaction.Nodes.OfType<SetValueStateScriptNodeData>().Single(node =>
            node.SetterKey == "unit.buffs.remove" && node.Value.Literal.Int == 12);
        AssertEdge(reaction, start.Guid, "Out", remove.Guid, "In");

        var init = guard.Nodes.OfType<SetValueStateScriptNodeData>().Single(node => node.Key == CooldownKey);
        Assert.That(init.Value.Literal.Bool, Is.False);
        AssertEdge(guard, guard.EntryNodeGuid, "Out", init.Guid, "In");
        foreach (var request in guard.Nodes.OfType<RequestSkillActionNodeData>())
        {
            Assert.That(request.SkillId.Literal.Int, Is.EqualTo(34));
            var reactionGate = IncomingCompare(guard, request.Guid);
            AssertFalseGetter(reactionGate.Condition, "unit.buffs.has", 13);
            var cooldownGate = IncomingCompare(guard, reactionGate.Guid);
            AssertFalseGetter(cooldownGate.Condition, "unit.variables.getBool", CooldownKey);
        }
        var allCooldownWrites = script.Graphs.SelectMany(graph => graph.Nodes).OfType<SetValueStateScriptNodeData>()
            .Where(node => node.Key == CooldownKey).ToArray();
        Assert.That(allCooldownWrites, Has.Length.EqualTo(3), "Only spawn initialization, a block and timer completion may write the cooldown.");
        foreach (var graph in new[] { guard, reaction })
        {
            Assert.That(graph.Nodes.Select(node => node.Guid).Distinct().Count(), Is.EqualTo(graph.Nodes.Count));
            foreach (var edge in graph.Edges)
            {
                var from = graph.Nodes.Single(node => node.Guid == edge.OutputNodeGuid);
                var to = graph.Nodes.Single(node => node.Guid == edge.InputNodeGuid);
                Assert.That(StateScriptNodeSchemaUtility.Create(from).Outputs, Does.Contain(edge.OutputPortName));
                Assert.That(StateScriptNodeSchemaUtility.Create(to).Inputs, Does.Contain(edge.InputPortName));
            }
        }
    }

    [Test]
    public void GuardIsConsumedOnDamageAndQueuedRearmsAreRejectedDuringCooldown()
    {
        var serializer = JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        BuffData buff = Rows("Buff").Single(row => (int)row["Id"] == 12).ToObject<BuffData>(serializer);
        Assert.That(buff.CanStack, Is.False);
        Assert.That(buff.MaxStacks, Is.EqualTo(1));
        BuffTriggerEntry hook = buff.TriggerEntries.Single();
        Assert.That(hook.HookType, Is.EqualTo(SkillHookType.OnDamaged));
        Assert.That(hook.ConsumeStackOnTrigger, Is.True);
        Assert.That(((ApplyBuffEffectData)hook.Effects.Single()).BuffId, Is.EqualTo(13));
        Assert.That(buff.PropertyModifiers.Single().Factor, Is.EqualTo(-0.5f), "Keep the existing block strength.");

        SkillData skill = Rows("Skill").Single(row => (int)row["Id"] == 34).ToObject<SkillData>(serializer);
        foreach (EffectData effect in skill.EffectChain)
        {
            Assert.That(effect.Conditions, Has.Count.EqualTo(2));
            AssertFalseGetter(effect.Conditions[0], "unit.variables.getBool", CooldownKey);
            AssertFalseGetter(effect.Conditions[1], "unit.buffs.has", 13);
        }
        int[] blockingUnits = Rows("StateScript").Where(row => row["Graphs"].Any(graph => (string)graph["Name"] == "Shield Guard"))
            .Select(row => (int)row["Id"]).ToArray();
        Assert.That(blockingUnits, Is.EquivalentTo(new[] { 14, 19, 24, 26 }));
    }

    private static CompareStateScriptNodeData IncomingCompare(StateScriptInstanceData graph, string target)
    {
        var edge = graph.Edges.Single(candidate => candidate.InputNodeGuid == target);
        Assert.That(edge.OutputPortName, Is.EqualTo("True"));
        return (CompareStateScriptNodeData)graph.Nodes.Single(node => node.Guid == edge.OutputNodeGuid);
    }

    private static void AssertFalseGetter(ConditionConfig condition, string getter, object argument)
    {
        Assert.That(condition.CompareType, Is.EqualTo("IsFalse"));
        Assert.That(condition.Inputs[0].GetterKey, Is.EqualTo(getter));
        if (argument is int number)
            Assert.That(condition.Inputs[0].Inputs[0].Literal.Int, Is.EqualTo(number));
        else
            Assert.That(condition.Inputs[0].Inputs[0].Literal.String, Is.EqualTo(argument));
    }

    private static void AssertEdge(StateScriptInstanceData graph, string from, string port, string to, string input) =>
        Assert.That(graph.Edges.Any(edge => edge.OutputNodeGuid == from && edge.OutputPortName == port &&
            edge.InputNodeGuid == to && edge.InputPortName == input), Is.True, $"{from}.{port} -> {to}.{input}");
}
