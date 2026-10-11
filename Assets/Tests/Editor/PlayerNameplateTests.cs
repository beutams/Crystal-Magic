using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.UI;
using NUnit.Framework;
using Server;
using Unity.Entities;
using UnityEditor;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerNameplateTests
{
    [Test]
    public void OtherPlayersKeepTheirOwnNamesThroughMovementGhostModeAndLobbyReconnect()
    {
        using Fixture f = new(WorldFlags.GameClient);
        f.Player(1, true);
        f.Player(1); // Ownership also excludes the local avatar before its input marker arrives.
        Entity remote = f.Player(2);
        Entity ghost = f.Player(3);
        f.World.EntityManager.AddComponent<BattleSpectatorComponent>(ghost);
        f.World.EntityManager.AddComponent<UnitDeathComponent>(ghost);
        f.Player(0);
        f.Player(2, prefabType: NetworkEntityPrefabType.Projectile);
        f.Render();

        Assert.That(f.Nameplates.Count, Is.EqualTo(2));
        Assert.That(f.Model(remote).Text, Is.EqualTo("星火"));
        Assert.That(f.Model(ghost).Text, Is.EqualTo("<b>幽灵</b>"));
        Assert.That(f.Model(ghost).Visible, Is.True);
        InteractionPromptUIData data = new();
        data.Bind(f.View(ghost).transform);
        Assert.That(data.Prompt_Label.TextMeshProUGUI.richText, Is.False);
        Assert.That(data.Prompt_Label.TextMeshProUGUI.text, Is.EqualTo("<b>幽灵</b>"));

        Vector2 position = f.Model(remote).Position;
        f.Camera.transform.position += Vector3.right;
        f.Render();
        Assert.That(f.Model(remote).Position.x, Is.Not.EqualTo(position.x));
        f.Network.clientLobbyManager.room.players[2] = "新的昵称";
        f.Render();
        Assert.That(f.Model(remote).Text, Is.EqualTo("新的昵称"));
        const string longSteamName = "Steam昵称012345678901234567890123456789";
        f.Network.clientLobbyManager.room.players[2] = longSteamName;
        f.Render();
        Assert.That(f.Model(remote).Text, Is.EqualTo(longSteamName));

        f.Network.clientLobbyManager.room = null;
        f.Render();
        Assert.That(f.Model(remote).Text, Is.EqualTo(longSteamName));
        Assert.That(f.Model(ghost).Text, Is.EqualTo("<b>幽灵</b>"));
        f.World.EntityManager.DestroyEntity(remote);
        f.Render();
        Assert.That(f.Nameplates.Count, Is.EqualTo(1));
        f.World.EntityManager.SetEnabled(ghost, false);
        f.Render();
        Assert.That(f.Nameplates.Count, Is.Zero);

        typeof(ClientBattleManager).GetMethod("ClearBattleData", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(f.Network.clientBattleManager, null);
        Assert.That(f.Network.clientBattleManager.TryGetPlayerDisplayName(2, out _), Is.False);
    }

    [TestCase(WorldFlags.Game)]
    [TestCase(WorldFlags.GameServer)]
    public void StandaloneAndHostedServerWorldsDoNotCreateNameplates(WorldFlags flags)
    {
        using Fixture f = new(flags);
        f.Player(2);
        f.Render();
        Assert.That(f.Nameplates.Count, Is.Zero);
    }

    [Test]
    public void PaddedPlayerSpritePlacesNameJustAboveHeadWithoutAnimationBobbing()
    {
        using Fixture f = new(WorldFlags.GameClient);
        Entity remote = f.Player(2);
        var actor = new GameObject("Padded player sprite", typeof(SpriteRenderer));
        try
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Res/Sprites/Units/Characters(100x100 split)/Wizard/Wizard with shadows/Wizard_Walk.png")
                .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            var renderer = actor.GetComponent<SpriteRenderer>(); renderer.sprite = sprites[0];
            actor.transform.localScale = new Vector3(6, 6, 1);
            f.World.EntityManager.AddComponentObject(remote, renderer);
            f.Render();
            Vector2 initial = f.Model(remote).Position;
            Assert.That(initial.y, Is.InRange(f.Project(Vector3.up * .5f).y, f.Project(Vector3.up * 1f).y),
                "The 100px frame contains a 20px character; the name must follow its head, not the full padded frame.");
            var data = new InteractionPromptUIData(); data.Bind(f.View(remote).transform);
            Assert.That(data.Prompt.RectTransform.pivot, Is.EqualTo(new Vector2(.5f, 0)));
            Assert.That(data.Prompt.RectTransform.anchoredPosition.y, Is.EqualTo(initial.y + 8).Within(.001f));
            renderer.sprite = sprites[3]; f.Render();
            Assert.That(f.Model(remote).Position, Is.EqualTo(initial), "Walk frames must not move the nickname up and down.");
            actor.transform.localScale = new Vector3(6, 3, 1); f.Render();
            Assert.That(f.Model(remote).Position.y - f.Project(Vector3.zero).y,
                Is.EqualTo((initial.y - f.Project(Vector3.zero).y) * .5f).Within(.001f));
            f.Model(remote).SetVisible(false); Assert.That(f.Model(remote).AboveAnchor, Is.True);
            f.Model(remote).SetDisplay("E 宝箱", initial, true);
            Assert.That(data.Prompt.RectTransform.pivot, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(data.Prompt.RectTransform.anchoredPosition, Is.EqualTo(initial), "Normal interaction prompts retain their layout.");
        }
        finally { Object.DestroyImmediate(actor); }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly NetworkComponent Network;
        public readonly Camera Camera;
        private readonly InteractionPromptManager _prompts = new();
        private readonly World _previousWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        private readonly GameObject _root = new("Nameplate services");
        private readonly List<(FieldInfo field, object previous)> _singletons = new();
        public IDictionary Nameplates => (IDictionary)typeof(InteractionPromptManager)
            .GetField("_playerNameplates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_prompts);

        public Fixture(WorldFlags flags)
        {
            _root.SetActive(false);
            Service<EventComponent>();
            Service<ResourceComponent>().Initialize();
            ConfigComponent config = Service<ConfigComponent>();
            ((IDictionary)typeof(ConfigComponent).GetField("_configs", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(config))[typeof(GameConfig)] = new GameConfig();
            Service<PoolComponent>().Initialize();
            UIComponent ui = Service<UIComponent>();
            var groupRoot = new GameObject("Nameplate canvas", typeof(RectTransform));
            groupRoot.transform.SetParent(_root.transform, false);
            var group = groupRoot.AddComponent<ListUIGroup>();
            ui.RegisterGroup("Bottom", group, new List<string> { nameof(InteractionPromptUI) });
            Network = Service<NetworkComponent>();
            Network.clientLobbyManager = new ClientLobbyManager
            {
                room = new RoomData { players = new Dictionary<ulong, string>
                    { [1] = "自己", [2] = "星火", [3] = "<b>幽灵</b>" } },
            };
            Network.clientBattleManager = new ClientBattleManager { localAccountId = 1 };
            var cameraRoot = new GameObject("Nameplate camera", typeof(Camera));
            cameraRoot.transform.SetParent(_root.transform, false);
            Camera = cameraRoot.GetComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = 10;
            Camera.transform.position = new Vector3(0, 0, -10);
            Set("_rootRect", groupRoot.transform as RectTransform);
            Set("_currentCamera", Camera);
            Set("_initialized", true);
            World = new World("Nameplate world", flags);
            Unity.Entities.World.DefaultGameObjectInjectionWorld = World;
        }

        private T Service<T>() where T : GameComponent<T>
        {
            FieldInfo field = typeof(Singleton<T>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            _singletons.Add((field, field.GetValue(null)));
            T service = _root.AddComponent<T>();
            field.SetValue(null, service);
            return service;
        }

        public Entity Player(ulong accountId, bool local = false,
            NetworkEntityPrefabType prefabType = NetworkEntityPrefabType.Unit)
        {
            EntityManager manager = World.EntityManager;
            Entity player = manager.CreateEntity(typeof(LocalToWorld));
            manager.SetComponentData(player, new LocalToWorld { Value = float4x4.identity });
            manager.AddComponentObject(player, new PlayerCharacterComponent());
            manager.AddComponentObject(player, new NetworkEntitySpawnInfoComponent
            {
                entityInfo = new NetworkEntitySpawnInfo { ownerAccountId = accountId, prefabType = prefabType },
            });
            if (local) manager.AddComponent<NetworkPlayerComponent>(player);
            return player;
        }

        public void Render() => typeof(InteractionPromptManager)
            .GetMethod("RefreshPlayerNameplates", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_prompts, null);
        public Vector2 Project(Vector3 position)
        {
            RectTransform root = (RectTransform)typeof(InteractionPromptManager)
                .GetField("_rootRect", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_prompts);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Camera.WorldToScreenPoint(position), null, out Vector2 point);
            return point;
        }
        public InteractionPromptUIModel Model(Entity player) => (InteractionPromptUIModel)Nameplates[player]
            .GetType().GetField("Model").GetValue(Nameplates[player]);
        public InteractionPromptUI View(Entity player) => (InteractionPromptUI)Nameplates[player]
            .GetType().GetField("View").GetValue(Nameplates[player]);
        private void Set(string field, object value) => typeof(InteractionPromptManager)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_prompts, value);

        public void Dispose()
        {
            _prompts.Dispose();
            Unity.Entities.World.DefaultGameObjectInjectionWorld = _previousWorld;
            World.Dispose();
            Object.DestroyImmediate(_root);
            foreach (var item in _singletons) item.field.SetValue(null, item.previous);
        }
    }
}
