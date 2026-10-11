using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class UnitAoiBuildSystemTests
{
    [TestCase(WorldFlags.Game, false)]
    [TestCase(WorldFlags.Game, true)]
    [TestCase(WorldFlags.GameClient, false)]
    [TestCase(WorldFlags.GameClient, true)]
    [TestCase(WorldFlags.GameServer, false)]
    [TestCase(WorldFlags.GameServer, true)]
    public void ObserverJobsAndTreeRebuildsRemainSafeAcrossFrames(WorldFlags flags, bool spectator)
    {
        using World world = new("AOI loading regression", flags);
        EntityManager manager = world.EntityManager;
        SystemHandle query = world.GetOrCreateSystem(typeof(UnitAoiBuildSystem).Assembly.GetType("UnitQueryBuildSystem"));
        SystemHandle aoi = world.GetOrCreateSystem<UnitAoiBuildSystem>();
        Entity observer = AddUnit(manager, UnitFactionType.Player, 0);
        Entity enemy = AddUnit(manager, UnitFactionType.Enemy, 5);
        if (spectator) manager.AddComponent<BattleSpectatorComponent>(observer);

        for (int frame = 0; frame < 10; frame++)
        {
            query.Update(world.Unmanaged);
            aoi.Update(world.Unmanaged);
            Assert.That(manager.IsComponentEnabled<UnitAoiActiveTag>(enemy), Is.True);
            Assert.That(manager.GetComponentData<UnitAoiStateComponent>(enemy).NearestPlayerDistanceSq, Is.EqualTo(25));
        }

        manager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(17, 0, 0)));
        query.Update(world.Unmanaged);
        aoi.Update(world.Unmanaged);
        Assert.That(manager.IsComponentEnabled<UnitAoiActiveTag>(enemy), Is.True, "Active units use the 18-unit exit radius.");

        manager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(19, 0, 0)));
        query.Update(world.Unmanaged);
        aoi.Update(world.Unmanaged);
        Assert.That(manager.IsComponentEnabled<UnitAoiActiveTag>(enemy), Is.False);
    }

    [Test]
    public void SpectatorBufferCanGrowAndShrinkWhileObserversChange()
    {
        using World world = new("AOI spectator resize regression");
        EntityManager manager = world.EntityManager;
        SystemHandle query = world.GetOrCreateSystem(typeof(UnitAoiBuildSystem).Assembly.GetType("UnitQueryBuildSystem"));
        SystemHandle aoi = world.GetOrCreateSystem<UnitAoiBuildSystem>();
        Entity enemy = AddUnit(manager, UnitFactionType.Enemy, 0);
        Entity observer = AddUnit(manager, UnitFactionType.Player, 5);
        for (int frame = 0; frame < 12; frame++)
        {
            if (frame % 2 == 0) manager.AddComponent<BattleSpectatorComponent>(observer);
            else manager.RemoveComponent<BattleSpectatorComponent>(observer);
            query.Update(world.Unmanaged);
            aoi.Update(world.Unmanaged);
            Assert.That(manager.IsComponentEnabled<UnitAoiActiveTag>(enemy), Is.True);
            Assert.That(manager.GetComponentData<UnitAoiStateComponent>(enemy).NearestPlayerDistanceSq, Is.EqualTo(25));
        }
    }

    private static Entity AddUnit(EntityManager manager, UnitFactionType faction, float x)
    {
        Entity entity = manager.CreateEntity(typeof(LocalTransform), typeof(UnitFactionComponent));
        manager.SetComponentData(entity, LocalTransform.FromPosition(new float3(x, 0, 0)));
        manager.SetComponentData(entity, new UnitFactionComponent { Value = faction });
        return entity;
    }
}
