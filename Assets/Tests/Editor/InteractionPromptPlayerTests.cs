using System;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using Unity.Entities;

public sealed class InteractionPromptPlayerTests
{
    private static readonly MethodInfo SelectPlayer = typeof(InteractionPromptManager)
        .GetMethod("TryGetPromptPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void DungeonSelectsCombatPlayerWhenTownPlayerAlsoHasInput()
    {
        using var fixture = new Fixture(GameWorldRole.Standalone, GameSceneMode.Dungeon);
        fixture.Player(false);
        Entity combat = fixture.Player(true);
        using var prompts = new InteractionPromptManager();
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(combat));
    }

    [Test]
    public void SceneChangeReplacesCachedPlayerWithTheMatchingScenePlayer()
    {
        using var fixture = new Fixture(GameWorldRole.Standalone, GameSceneMode.Town);
        Entity town = fixture.Player(false);
        Entity combat = fixture.Player(true);
        using var prompts = new InteractionPromptManager();
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(town));
        fixture.Scene(GameSceneMode.Dungeon);
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(combat));
    }

    [Test]
    public void ClientSelectsTheLocalPlayerAmongMultipleInputEntities()
    {
        using var fixture = new Fixture(GameWorldRole.Client, GameSceneMode.Dungeon);
        fixture.Player(true);
        Entity local = fixture.Player(true, true);
        using var prompts = new InteractionPromptManager();
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(local));
        fixture.Manager.RemoveComponent<NetworkPlayerComponent>(local);
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(Entity.Null),
            "本地标记移除后不能显示其他玩家的交互候选。");
    }

    [Test]
    public void DestroyedLocalPlayerIsReplacedAfterRespawn()
    {
        using var fixture = new Fixture(GameWorldRole.Client, GameSceneMode.Dungeon);
        Entity oldPlayer = fixture.Player(true, true);
        using var prompts = new InteractionPromptManager();
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(oldPlayer));
        fixture.Manager.DestroyEntity(oldPlayer);
        Entity replacement = fixture.Player(true, true);
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(replacement));
    }

    [Test]
    public void SwitchingWorldsInvalidatesCachedPlayer()
    {
        using var first = new Fixture(GameWorldRole.Standalone, GameSceneMode.Dungeon);
        using var second = new Fixture(GameWorldRole.Standalone, GameSceneMode.Dungeon);
        Entity firstPlayer = first.Player(true);
        second.Manager.CreateEntity();
        Entity secondPlayer = second.Player(true);
        using var prompts = new InteractionPromptManager();
        Assert.That(Select(prompts, first.Manager), Is.EqualTo(firstPlayer));
        Assert.That(Select(prompts, second.Manager), Is.EqualTo(secondPlayer));
    }

    [TestCase("disabled")]
    [TestCase("initializing")]
    [TestCase("spectator")]
    [TestCase("dead")]
    [TestCase("destroying")]
    public void UnavailableLocalPlayerDoesNotFallBackToAnotherPlayer(string reason)
    {
        using var fixture = new Fixture(GameWorldRole.Client, GameSceneMode.Dungeon);
        fixture.Player(true);
        Entity local = fixture.Player(true, true);
        using var prompts = new InteractionPromptManager();
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(local));
        switch (reason)
        {
            case "disabled": fixture.Manager.SetEnabled(local, false); break;
            case "initializing": fixture.Manager.AddComponent<UnitInitializationPendingTag>(local); break;
            case "spectator": fixture.Manager.AddComponent<BattleSpectatorComponent>(local); break;
            case "dead": fixture.Manager.AddComponent<UnitDeathComponent>(local); break;
            case "destroying": fixture.Manager.AddComponent<DestroyEntityFlag>(local); break;
        }
        Assert.That(Select(prompts, fixture.Manager), Is.EqualTo(Entity.Null));
    }

    [Test]
    public void PromptTargetLookupDoesNotRequireInputEntitiesToBeASingleton()
    {
        using var fixture = new Fixture(GameWorldRole.Standalone, GameSceneMode.Dungeon);
        fixture.Player(false);
        fixture.Player(true);
        using var prompts = new InteractionPromptManager();
        World previous = World.DefaultGameObjectInjectionWorld;
        try
        {
            World.DefaultGameObjectInjectionWorld = fixture.World;
            var method = typeof(InteractionPromptManager).GetMethod("TryGetPromptTarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { default(Unity.Mathematics.float3), null, 0f };
            Assert.That(method.Invoke(prompts, arguments), Is.False);
        }
        finally { World.DefaultGameObjectInjectionWorld = previous; }
    }

    private static Entity Select(InteractionPromptManager prompts, EntityManager manager)
    {
        object[] arguments = { manager, Entity.Null };
        return (bool)SelectPlayer.Invoke(prompts, arguments) ? (Entity)arguments[1] : Entity.Null;
    }

    private sealed class Fixture : IDisposable
    {
        public readonly World World = new("Interaction prompt player tests");
        public EntityManager Manager => World.EntityManager;
        private readonly Entity _context;
        private readonly GameWorldRole _role;

        public Fixture(GameWorldRole role, GameSceneMode scene)
        {
            _role = role;
            _context = Manager.CreateEntity(typeof(GameWorldContextComponent));
            Scene(scene);
        }

        public void Scene(GameSceneMode scene) => Manager.SetComponentData(_context,
            new GameWorldContextComponent { Role = _role, SceneMode = scene });

        public Entity Player(bool combat, bool local = false)
        {
            Entity player = Manager.CreateEntity(typeof(PlayerInputComponent), typeof(UnitVariableComponent),
                typeof(UnitFactionComponent));
            Manager.SetComponentData(player, new UnitFactionComponent { Value = UnitFactionType.Player });
            Manager.AddBuffer<UnitVariableElement>(player);
            Manager.AddBuffer<UnitVariableConsumerElement>(player);
            if (combat) Manager.AddComponent<UnitSkillReleaseComponent>(player);
            if (local) Manager.AddComponent<NetworkPlayerComponent>(player);
            return player;
        }

        public void Dispose() => World.Dispose();
    }
}
