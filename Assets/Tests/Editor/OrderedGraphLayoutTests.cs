using System.Collections.Generic;
using CrystalMagic.Editor;
using NUnit.Framework;
using Node = CrystalMagic.Editor.OrderedGraphLayout.Node;
using Edge = CrystalMagic.Editor.OrderedGraphLayout.Edge;

public sealed class OrderedGraphLayoutTests
{
    [Test]
    public void TreeUsesDepthRowsAndDeclaredSiblingOrder()
    {
        var nodes = new[] { N("root"), N("left"), N("right"), N("a"), N("b"), N("c") };
        var edges = new[] { E("root", "left"), E("root", "right"), E("left", "a"), E("left", "b"), E("right", "c") };
        var result = OrderedGraphLayout.Arrange(nodes, edges, "root", true);
        Assert.That(result.Positions["left"].Y, Is.EqualTo(result.Positions["right"].Y));
        Assert.That(result.Positions["left"].X, Is.LessThan(result.Positions["right"].X));
        Assert.That(result.Positions["a"].X, Is.LessThan(result.Positions["b"].X));
        Assert.That(result.Positions["b"].X, Is.LessThan(result.Positions["c"].X));
        AssertLayout(nodes, edges, result, true);
    }

    [Test]
    public void MergeFollowsLongestExecutionPath()
    {
        var nodes = new[] { N("entry"), N("short"), N("long"), N("middle"), N("merge") };
        var edges = new[] { E("entry", "short"), E("short", "merge"), E("entry", "long"), E("long", "middle"), E("middle", "merge") };
        var result = OrderedGraphLayout.Arrange(nodes, edges, "entry", false);
        Assert.That(result.Depths["merge"], Is.EqualTo(3));
        AssertLayout(nodes, edges, result, false);
    }

    [Test]
    public void LoopKeepsForwardChainAndBackConnection()
    {
        var nodes = new[] { N("entry"), N("a"), N("b"), N("done") };
        var edges = new[] { E("entry", "a"), E("a", "b"), E("b", "a"), E("b", "done") };
        var result = OrderedGraphLayout.Arrange(nodes, edges, "entry", false);
        Assert.That(result.BackEdges.Count, Is.EqualTo(1));
        Assert.That(result.BackEdges[0].From, Is.EqualTo("b"));
        Assert.That(result.BackEdges[0].To, Is.EqualTo("a"));
        Assert.That(result.Positions["b"].X, Is.GreaterThan(result.Positions["a"].X));
        Assert.That(edges.Length, Is.EqualTo(4));
        AssertLayout(nodes, edges, result, false);
    }

    [Test]
    public void SharedBehaviorLeafRespectsBothSequences()
    {
        var nodes = new[] { N("root"), N("one"), N("two"), N("a"), N("b"), N("shared"), N("end1"), N("end2") };
        var edges = new[] { E("root", "one"), E("root", "two"), E("one", "a"), E("one", "shared"), E("one", "end1"),
            E("two", "b"), E("two", "shared"), E("two", "end2") };
        var result = OrderedGraphLayout.Arrange(nodes, edges, "root", true);
        Assert.That(result.Positions["a"].X, Is.LessThan(result.Positions["shared"].X));
        Assert.That(result.Positions["b"].X, Is.LessThan(result.Positions["shared"].X));
        Assert.That(result.Positions["end1"].X, Is.GreaterThan(result.Positions["shared"].X));
        Assert.That(result.Positions["end2"].X, Is.GreaterThan(result.Positions["shared"].X));
        AssertLayout(nodes, edges, result, true);
    }

    [Test]
    public void DisconnectedNodesAndCyclesAreNotDropped()
    {
        var nodes = new[] { N("entry"), N("a"), N("island"), N("x"), N("y") };
        var edges = new[] { E("entry", "a"), E("x", "y"), E("y", "x") };
        var result = OrderedGraphLayout.Arrange(nodes, edges, "entry", false);
        Assert.That(result.Positions.Count, Is.EqualTo(nodes.Length));
        AssertLayout(nodes, edges, result, false);
    }

    [Test]
    public void TallEffectStacksReserveHeightOfWholeLayer()
    {
        var nodes = new[] { new Node("root", 420, 710), new Node("a", 420, 1010), new Node("b", 500, 260), new Node("c", 420, 110) };
        var edges = new[] { E("root", "a"), E("root", "b"), E("b", "c") };
        var result = OrderedGraphLayout.Arrange(nodes, edges, "root", true);
        Assert.That(result.Positions["c"].Y, Is.GreaterThanOrEqualTo(result.Positions["a"].Y + 1010 + 140));
        AssertLayout(nodes, edges, result, true);
    }

    [Test]
    public void RepeatedLayoutIsDeterministicAndDoesNotChangeInputs()
    {
        var nodes = new[] { N("entry"), N("b"), N("a") };
        var edges = new[] { E("entry", "a"), E("entry", "b") };
        var first = OrderedGraphLayout.Arrange(nodes, edges, "entry", true);
        var second = OrderedGraphLayout.Arrange(nodes, edges, "entry", true);
        foreach (var node in nodes)
        {
            Assert.That(second.Positions[node.Id].X, Is.EqualTo(first.Positions[node.Id].X));
            Assert.That(second.Positions[node.Id].Y, Is.EqualTo(first.Positions[node.Id].Y));
        }
        Assert.That(nodes[1].Id, Is.EqualTo("b"));
        Assert.That(edges[0].To, Is.EqualTo("a"));
    }

    private static Node N(string id) => new(id, 260, 140);
    private static Edge E(string from, string to) => new(from, to);

    private static void AssertLayout(Node[] nodes, Edge[] edges, OrderedGraphLayout.Result result, bool vertical)
    {
        Dictionary<string, Node> byId = new();
        foreach (var node in nodes) byId[node.Id] = node;
        foreach (var edge in edges)
        {
            if (result.BackEdges.Exists(back => back.From == edge.From && back.To == edge.To)) continue;
            var a = result.Positions[edge.From]; var b = result.Positions[edge.To];
            Assert.That(vertical ? b.Y - a.Y : b.X - a.X,
                Is.GreaterThanOrEqualTo((vertical ? byId[edge.From].Height : byId[edge.From].Width) + 140));
        }
        for (int i = 0; i < nodes.Length; i++)
        for (int j = i + 1; j < nodes.Length; j++)
        {
            var a = nodes[i]; var b = nodes[j];
            var pa = result.Positions[a.Id]; var pb = result.Positions[b.Id];
            bool overlap = pa.X < pb.X + b.Width && pb.X < pa.X + a.Width && pa.Y < pb.Y + b.Height && pb.Y < pa.Y + a.Height;
            Assert.That(overlap, Is.False, $"Overlapping {a.Id} / {b.Id}");
        }
    }
}
