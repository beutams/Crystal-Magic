using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Entities;

public sealed class UnitSourceDispatcherTests
{
    [Test]
    public void TryGetRefreshesLookupsAfterStructuralChange()
    {
        using World world = new("Unit source dispatcher refresh test");
        EntityManager entityManager = world.EntityManager;
        GameSingletonUtility.Create(
            entityManager,
            GameWorldRole.Standalone,
            GameSceneMode.Dungeon);
        world.CreateSystemManaged<UnitSourceDispatcherSystem>();

        Entity self = entityManager.CreateEntity(typeof(UnitFactionComponent));
        Entity other = entityManager.CreateEntity(typeof(UnitFactionComponent));
        entityManager.SetComponentData(self, new UnitFactionComponent
        {
            Value = UnitFactionType.Player,
        });
        entityManager.SetComponentData(other, new UnitFactionComponent
        {
            Value = UnitFactionType.Enemy,
        });

        Assert.That(
            UnitSourceDispatcherSystem.TryGet(entityManager, out UnitSourceDispatcher first),
            Is.True);
        AssertEnemyRelation(in first, self, other);

        entityManager.CreateEntity(typeof(UnitFactionComponent));

        Assert.That(
            UnitSourceDispatcherSystem.TryGet(entityManager, out UnitSourceDispatcher refreshed),
            Is.True);
        AssertEnemyRelation(in refreshed, self, other);
    }

    private static void AssertEnemyRelation(
        in UnitSourceDispatcher dispatcher,
        Entity self,
        Entity other)
    {
        UnitSourceArguments arguments = default;
        arguments.Values.Add(UnitSourceValue.FromEntity(other));

        Assert.That(dispatcher.TryGet(
            self,
            UnitSourceId.UnitFactionIsEnemyTo,
            in arguments,
            out UnitSourceValue value), Is.True);
        Assert.That(value.TryGetBool(out bool isEnemy), Is.True);
        Assert.That(isEnemy, Is.True);
    }
}
