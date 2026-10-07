using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CrystalMagic.Game.Map;
using NUnit.Framework;

public sealed class WorldDropSortingTests
{
    [TestCase(-10000f)]
    [TestCase(-300f)]
    [TestCase(-0.5f)]
    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(300f)]
    [TestCase(10000f)]
    public void ActorsStayAboveDropsEvenAtLargeMapCoordinates(float worldY)
    {
        int actorOrder = TileOcclusionSortAnchor.OrderForY(worldY);
        Assert.That(actorOrder, Is.GreaterThan(DropSpritePresentationSystem.SortingOrder));
        Assert.That(actorOrder, Is.InRange(TileOcclusionSortAnchor.MinimumOrder, TileOcclusionSortAnchor.MaximumOrder));
    }

    [TestCase("TownMap/TownMap_Occlusion.prefab")]
    [TestCase("TrainingMap/TrainingMap_Occlusion.prefab")]
    public void AuthoredGroundIsBelowDrops(string map)
    {
        string prefab = File.ReadAllText("Assets/Res/Tile/OcclusionMaps/" + map);
        int[] orders = Regex.Matches(prefab, @"m_SortingOrder: (-?\d+)").Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value))
            .Where(order => order < TileOcclusionSortAnchor.MinimumOrder).ToArray();
        Assert.That(orders, Is.Not.Empty);
        Assert.That(orders.Max(), Is.LessThan(DropSpritePresentationSystem.SortingOrder));
    }

    [Test]
    public void DropPrefabAlreadyUsesTheFixedOrderBeforePresentationInitializes()
    {
        string prefab = File.ReadAllText("Assets/Res/Prefab/Drop/Drop.prefab");
        Assert.That(Regex.Matches(prefab, @"m_SortingOrder: (-?\d+)").Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value)),
            Is.EquivalentTo(new[] { DropSpritePresentationSystem.SortingOrder }));
        Assert.That(prefab, Does.Contain("m_SortingLayerID: 0"));
        foreach (string unit in new[] { "PlayerTown", "PlayerDungeon", "Skeleton", "Knight" })
            Assert.That(File.ReadAllText($"Assets/Res/Prefab/Unit/{unit}.prefab"), Does.Contain("m_SortingLayerID: 0"));
    }
}
