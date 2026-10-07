using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using Server;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DungeonSettlementTests
{
    [TestCase(DungeonSettlementOutcome.Escaped)]
    [TestCase(DungeonSettlementOutcome.Defeated)]
    public void SettlementOverwritesDungeonSaveBeforeReportOrTownTransition(DungeonSettlementOutcome outcome)
    {
        using var scope = new SettlementScope();
        scope.Character.Money = 23;
        scope.Character.Backpack.Items.Add(new InventoryItemData { ItemId = 10, Quantity = 5 });
        scope.Character.Equipment.MagicStoneId = 30;
        scope.Character.Props.Slots.Add(new CharacterPropSlotData { ItemId = 20, Quantity = 2 });
        scope.Character.Skills.Chains[0].Slots.Add(new SkillChainSlotData { SkillStoneItemId = 40 });
        scope.Save.SetVariable("TestProgress", 7);
        scope.Save.GetGlobalData().TotalPlayTimeSeconds = 123;
        Assert.That(scope.Save.Save(), Is.True);
        Assert.That(scope.ReadSave().Location.AreaType, Is.EqualTo(SaveAreaType.Dungeon));
        string guid = scope.Save.CurrentSaveGuid;
        bool savedBeforePlayerDestruction = false;
        scope.Save.OnSaveSuccess += _ => savedBeforePlayerDestruction = scope.Manager.Exists(scope.Player);

        DungeonSettlementStateData data = DungeonSettlementStateData.Create(outcome);

        Assert.That(data.IsSaved, Is.True);
        Assert.That(savedBeforePlayerDestruction, Is.True);
        scope.Manager.DestroyEntity(scope.Player); // Simulate closing without pressing Return to Town.
        Assert.That(scope.Save.LoadFromSlot(0, guid, out LoadGameContext loaded), Is.True);
        Assert.That(loaded.Location.AreaType, Is.EqualTo(SaveAreaType.Town));
        Assert.That(loaded.Player, Is.Null, "Do not restore dungeon coordinates or dead vitality in town.");
        Assert.That(loaded.DungeonRun, Is.Null);
        Assert.That(loaded.SaveGuid, Is.EqualTo(guid));
        Assert.That(scope.Save.GetStashMoney(), Is.EqualTo(100));
        Assert.That(scope.Save.GetStashData().Items.Single().Quantity, Is.EqualTo(12));
        Assert.That(scope.Save.GetVariable("TestProgress"), Is.EqualTo(7));
        Assert.That(scope.Save.GetGlobalData().TotalPlayTimeSeconds, Is.EqualTo(123));
        bool escaped = outcome == DungeonSettlementOutcome.Escaped;
        Assert.That(loaded.Character.Money, Is.EqualTo(escaped ? 23 : 0));
        Assert.That(loaded.Character.Backpack.Items.Where(x => !x.IsEmpty).Sum(x => x.Quantity), Is.EqualTo(escaped ? 5 : 0));
        Assert.That(loaded.Character.Equipment.MagicStoneId, Is.EqualTo(escaped ? 30 : -1));
        Assert.That(loaded.Character.Props.Slots.Where(x => !x.IsEmpty).Sum(x => x.Quantity), Is.EqualTo(escaped ? 2 : 0));
        Assert.That(loaded.Character.Skills.Chains[0].Slots.Count, Is.EqualTo(escaped ? 1 : 0));
    }

    [TestCase(DungeonSettlementOutcome.Escaped)]
    [TestCase(DungeonSettlementOutcome.Defeated)]
    public void FailedSaveRetriesRetainedSettlementAfterPlayerIsDestroyed(DungeonSettlementOutcome outcome)
    {
        using var scope = new SettlementScope();
        scope.Character.Money = 23;
        scope.Character.Backpack.Items.Add(new InventoryItemData { ItemId = 10, Quantity = 5 });
        Directory.CreateDirectory(scope.SavePath); // A directory in place of a save file forces a write failure.
        LogAssert.Expect(LogType.Error, new Regex("\\[SaveDataComponent\\] Error saving game:"));
        DungeonSettlementStateData data = DungeonSettlementStateData.Create(outcome);
        Assert.That(data.IsSaved, Is.False);
        Assert.That(File.Exists(scope.SavePath), Is.False);
        scope.Manager.DestroyEntity(scope.Player);
        scope.Character.Backpack.Items.Clear();
        Directory.Delete(scope.SavePath);

        Assert.That(data.TrySave(), Is.True);
        SaveData saved = scope.ReadSave();
        Assert.That(saved.Location.AreaType, Is.EqualTo(SaveAreaType.Town));
        Assert.That(saved.Player, Is.Null);
        Assert.That(saved.Character.Money, Is.EqualTo(outcome == DungeonSettlementOutcome.Escaped ? 23 : 0));
        Assert.That(saved.Character.Backpack.Items.Where(x => !x.IsEmpty).Sum(x => x.Quantity),
            Is.EqualTo(outcome == DungeonSettlementOutcome.Escaped ? 5 : 0));
        string committed = File.ReadAllText(scope.SavePath);
        scope.Stash.Money = 999;
        Assert.That(data.TrySave(), Is.True);
        Assert.That(File.ReadAllText(scope.SavePath), Is.EqualTo(committed), "A repeated confirmation must not write or settle again.");
    }

    [Test]
    public void SaveFailureKeepsSettlementConfirmationAvailableForRetry()
    {
        using var scope = new SettlementScope();
        var model = new DungeonSettlementUIModel();
        model.SetOpenData(new DungeonSettlementUIOpenData { SaveFailed = true });
        Assert.That(model.Footnote, Does.Contain("自动存档失败"));
        Assert.That(model.ConfirmLabel, Is.EqualTo("重试保存并返回"));
        Assert.That(model.TryBeginReturn(), Is.True);
        model.SetSaveFailed();
        Assert.That(model.IsReturning, Is.False);
        Assert.That(model.TryBeginReturn(), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DefeatCapturesAllCarriedItemsBeforeDungeonExitDestroysPlayer(bool acquiredLoot)
    {
        using var scope = new SettlementScope();
        CharacterData character = scope.Character;
        character.Money = 23;
        character.Backpack.Items.Add(new InventoryItemData { ItemId = 10, Quantity = 4 });
        character.Backpack.Items.Add(new InventoryItemData { ItemId = 20, Quantity = 2 });
        character.Equipment.MagicStoneId = 30;
        character.Props.Slots.Add(new CharacterPropSlotData { ItemId = 10, Quantity = 3 });
        character.Skills.Chains[0].Slots.Add(new SkillChainSlotData { SkillStoneItemId = 40 });
        if (acquiredLoot)
        {
            character.Backpack.Items[0].Quantity += 2;
            scope.Run.AcquiredItems.Add(new InventoryItemData { ItemId = 10, Quantity = 2 });
        }

        DungeonSettlementStateData data = DungeonSettlementStateData.Create(DungeonSettlementOutcome.Defeated);
        Assert.That(GameRuntimeStateUtility.GetPlayerCharacterData(), Is.Null, "Defeat removes all carried inventory.");
        scope.Manager.DestroyEntity(scope.Player); // DungeonState.OnExitBattle releases the map's player.
        character.Backpack.Items.Clear();

        Assert.That(data.Result.Items.Select(x => x.ItemId), Is.EquivalentTo(new[] { 10, 20, 30, 40 }));
        Assert.That(data.Result.Items.Single(x => x.ItemId == 10).Quantity, Is.EqualTo(acquiredLoot ? 9 : 7));
        Assert.That(data.Result.Items.Single(x => x.ItemId == 20).Quantity, Is.EqualTo(2));
        Assert.That(data.Result.ReachedFloor, Is.EqualTo(3));
        Assert.That(data.Result.Money, Is.EqualTo(23));
        Assert.That(data.ReturnContext.Character, Is.Null, "Returning to town must not restore lost items.");
        Assert.That(data.ReturnContext.Location.AreaType, Is.EqualTo(SaveAreaType.Town));
        Assert.That(GameRuntimeStateUtility.GetDungeonRunData(), Is.Null);
        Assert.That(scope.Stash.Items.Single().Quantity, Is.EqualTo(12));
        Assert.That(scope.Stash.Money, Is.EqualTo(100));
    }

    [Test]
    public void EscapeRetainsReturnInventoryWhenDungeonExitDestroysPlayer()
    {
        using var scope = new SettlementScope();
        scope.Character.Backpack.Items.Add(new InventoryItemData { ItemId = 10, Quantity = 5 });
        scope.Run.AcquiredItems.Add(new InventoryItemData { ItemId = 10, Quantity = 2 });
        DungeonSettlementStateData data = DungeonSettlementStateData.Create(DungeonSettlementOutcome.Escaped);
        scope.Manager.DestroyEntity(scope.Player);
        scope.Character.Backpack.Items.Clear();

        Assert.That(data.Result.Items.Single().Quantity, Is.EqualTo(2));
        Assert.That(data.ReturnContext.Character.Backpack.Items.Single().Quantity, Is.EqualTo(5));
        Assert.That(GameRuntimeStateUtility.GetDungeonRunData(), Is.Null);
    }

    [Test]
    public void DeathFinalizationAllowsSettlementToDestroyPlayerDuringDeathEvent()
    {
        using var scope = new SettlementScope();
        scope.Character.Backpack.Items.Add(new InventoryItemData { ItemId = 10, Quantity = 4 });
        scope.Manager.AddComponent<UnitDeathComponent>(scope.Player);
        DungeonSettlementStateData data = null;
        EventComponent.Instance.Subscribe<UnitDiedEvent>(gameEvent =>
        {
            data = DungeonSettlementStateData.Create(DungeonSettlementOutcome.Defeated);
            scope.Manager.DestroyEntity(gameEvent.Entity);
        });
        System.Type systemType = typeof(DungeonState).Assembly.GetType("UnitDeathFinalizeSystem", true);
        var system = (SystemBase)scope.World.GetOrCreateSystemManaged(systemType);

        Assert.DoesNotThrow(() => system.Update());
        Assert.That(data, Is.Not.Null);
        Assert.That(data.Result.Items.Single().Quantity, Is.EqualTo(4));
        Assert.That(scope.Manager.Exists(scope.Player), Is.False);
    }

    private sealed class SettlementScope : System.IDisposable
    {
        public readonly World World = new("Dungeon settlement lifecycle test");
        public EntityManager Manager => World.EntityManager;
        public readonly Entity Player;
        public readonly CharacterData Character = new();
        public readonly DungeonRunData Run = new() { CurrentFloor = 3, HasAcquisitionHistory = true };
        public readonly StashData Stash = new() { Money = 100 };
        public readonly SaveDataComponent Save;
        public readonly string SavePath;
        private readonly string _saveFolder;
        private readonly GameObject _services = new("Settlement test services");
        private readonly List<(FieldInfo field, object value)> _savedFields = new();

        public SettlementScope()
        {
            _services.SetActive(false);
            Replace(typeof(GameWorldManager).GetField("_gameWorld", BindingFlags.Static | BindingFlags.NonPublic), World);
            Replace(typeof(GameWorldManager).GetField("<SceneMode>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic), GameSceneMode.Dungeon);
            Save = ReplaceSingleton<SaveDataComponent>();
            ReplaceSingleton<EventComponent>();
            ReplaceSingleton<DataComponent>();
            ReplaceSingleton<NetworkComponent>();
            SteamComponent steam = ReplaceSingleton<SteamComponent>();
            ulong accountId = System.BitConverter.ToUInt64(System.Guid.NewGuid().ToByteArray(), 0) & (ClientAccountIdentity.LocalNamespace - 1);
            var account = new ClientAccountIdentity(accountId, "Settlement test", true);
            typeof(SteamComponent).GetProperty(nameof(SteamComponent.Account)).SetValue(steam, account);
            _saveFolder = account.GetSaveFolder(Application.persistentDataPath);
            Assert.That(Directory.Exists(_saveFolder), Is.False, "Tests must never reuse a player's save folder.");
            SavePath = Path.Combine(_saveFolder, "0.json");
            ConfigComponent config = ReplaceSingleton<ConfigComponent>();
            var configs = (Dictionary<System.Type, object>)typeof(ConfigComponent)
                .GetField("_configs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(config);
            configs[typeof(DungeonConfig)] = new DungeonConfig();
            configs[typeof(GameConfig)] = new GameConfig();
            Save.Initialize();
            typeof(SaveDataComponent).GetField("_currentSaveIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Save, 0);
            typeof(SaveDataComponent).GetField("_currentSaveGuid", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Save, System.Guid.NewGuid().ToString("N"));
            GameSingletonUtility.Create(Manager, GameWorldRole.Standalone, GameSceneMode.Dungeon);
            Player = Manager.CreateEntity(typeof(NetworkPlayerComponent), typeof(UnitFactionComponent));
            Manager.SetComponentData(Player, new UnitFactionComponent { Value = UnitFactionType.Player });
            Manager.AddComponentObject(Player, new PlayerCharacterComponent { Data = Character });
            Stash.Items.Add(new InventoryItemData { ItemId = 50, Quantity = 12 });
            Manager.AddComponentObject(Manager.CreateEntity(), new StashComponent { Data = Stash });
            GameRuntimeStateUtility.CreateDungeonRun(Run);
        }

        public SaveData ReadSave() => JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));

        private T ReplaceSingleton<T>() where T : Singleton<T>
        {
            T instance = _services.AddComponent<T>();
            Replace(typeof(Singleton<T>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic), instance);
            return instance;
        }

        private void Replace(FieldInfo field, object value)
        {
            _savedFields.Add((field, field.GetValue(null)));
            field.SetValue(null, value);
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_services);
            for (int i = _savedFields.Count - 1; i >= 0; i--)
                _savedFields[i].field.SetValue(null, _savedFields[i].value);
            World.Dispose();
            if (Directory.Exists(SavePath)) Directory.Delete(SavePath);
            if (File.Exists(SavePath)) File.Delete(SavePath);
            string backup = Path.ChangeExtension(SavePath, ".backup.json");
            if (File.Exists(backup)) File.Delete(backup);
            if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder);
        }
    }

    [Test]
    public void SuccessUsesAcquisitionJournalNotTheCarriedInventory()
    {
        var character = new CharacterData();
        character.Backpack.Items.Add(new InventoryItemData { ItemId = 90, Quantity = 5 });
        character.Equipment.MagicStoneId = 91;
        var run = new DungeonRunData
        {
            CurrentFloor = 5, HasAcquisitionHistory = true, AcquiredMoney = 17,
            AcquiredItems = new List<InventoryItemData> { new() { ItemId = 10, Quantity = 3 } },
        };
        var result = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Escaped, run, character);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ReachedFloor, Is.EqualTo(5));
        Assert.That(result.Items.Select(x => x.ItemId), Is.EqualTo(new[] { 10 }));
        Assert.That(result.Items[0].Quantity, Is.EqualTo(3));
        Assert.That(result.Money, Is.EqualTo(17));
        Assert.That(result.HasCompleteHistory, Is.True);
    }

    [Test]
    public void DefeatIncludesBackpackEquipmentPropsAndSkillStonesButNotAdditionIds()
    {
        var character = new CharacterData { Money = 23 };
        character.Backpack.Items.Add(new InventoryItemData { ItemId = 10, Quantity = 2 });
        character.Equipment.MagicStoneId = 11;
        character.Equipment.SpiritSlots = new[] { 12, 12, -1, -1 };
        character.Props.Slots.Add(new CharacterPropSlotData { ItemId = 10, Quantity = 3 });
        character.Skills.Chains[0].Slots.Add(new SkillChainSlotData { SkillStoneItemId = 13, SkillAdditionId = 999 });
        var result = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Defeated, null, character);
        Assert.That(result.Items.Select(x => x.ItemId), Is.EquivalentTo(new[] { 10, 11, 12, 13 }));
        Assert.That(result.Items.Single(x => x.ItemId == 10).Quantity, Is.EqualTo(5));
        Assert.That(result.Items.Single(x => x.ItemId == 12).Quantity, Is.EqualTo(2));
        Assert.That(result.Money, Is.EqualTo(23));
        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void ReportsAreDetachedBeforeSourceDataIsCleared()
    {
        var character = new CharacterData();
        var item = new InventoryItemData { ItemId = 7, Quantity = 4 };
        character.Backpack.Items.Add(item);
        var run = new DungeonRunData { HasAcquisitionHistory = true };
        run.AcquiredItems.Add(item);
        var success = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Escaped, run, character);
        var failure = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Defeated, run, character);
        item.Clear();
        run.AcquiredItems.Clear();
        character.Backpack.Items.Clear();
        Assert.That(success.Items[0].ItemId, Is.EqualTo(7));
        Assert.That(failure.Items[0].Quantity, Is.EqualTo(4));
    }

    [Test]
    public void MovingEquipmentBetweenSlotsDoesNotCreateAnotherAcquisition()
    {
        var run = new DungeonRunData { HasAcquisitionHistory = true };
        run.AcquiredItems.Add(new InventoryItemData { ItemId = 7, Quantity = 1 });
        var character = new CharacterData();
        character.Equipment.MagicStoneId = 7;
        var equipped = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Escaped, run, character);
        character.Equipment.MagicStoneId = -1;
        character.Backpack.Items.Add(new InventoryItemData { ItemId = 7, Quantity = 1 });
        var unequipped = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Escaped, run, character);
        Assert.That(equipped.Items[0].Quantity, Is.EqualTo(unequipped.Items[0].Quantity));
    }

    [Test]
    public void LegacySaveDoesNotInventAcquisitionsFromExistingInventory()
    {
        var run = JsonConvert.DeserializeObject<DungeonRunData>("{\"CurrentFloor\":3}");
        var character = new CharacterData();
        character.Equipment.MagicStoneId = 10;
        var result = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Escaped, run, character);
        Assert.That(result.HasCompleteHistory, Is.False);
        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    public void JournalSurvivesSerializationAndFloorChanges()
    {
        var run = new DungeonRunData { HasAcquisitionHistory = true, AcquiredMoney = 24, CurrentFloor = 1 };
        run.AcquiredItems.Add(new InventoryItemData { ItemId = 5, Quantity = 2 });
        run = JsonConvert.DeserializeObject<DungeonRunData>(JsonConvert.SerializeObject(run));
        run.CurrentFloor = 4;
        var result = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Escaped, run, null);
        Assert.That(result.HasCompleteHistory, Is.True);
        Assert.That(result.Items[0].Quantity, Is.EqualTo(2));
        Assert.That(result.Money, Is.EqualTo(24));
        Assert.That(result.ReachedFloor, Is.EqualTo(4));
    }

    [Test]
    public void EmptyAndMalformedSlotsDoNotProducePhantomLoot()
    {
        var character = new CharacterData();
        character.Backpack.Items.AddRange(new InventoryItemData[]
        {
            null, new() { ItemId = -1, Quantity = 5 }, new() { ItemId = 7, Quantity = 0 },
        });
        character.Skills.Chains[0] = null;
        character.Props.Slots.Add(null);
        var result = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Defeated, null, character);
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.ReachedFloor, Is.EqualTo(1));
    }

    [Test]
    public void ReportDoesNotGrantOrRemoveItemsAndMergesDuplicatesSafely()
    {
        var character = new CharacterData();
        character.Backpack.Items.Add(new InventoryItemData { ItemId = 7, Quantity = int.MaxValue });
        character.Equipment.MagicStoneId = 7;
        var first = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Defeated, null, character);
        var second = DungeonSettlementUtility.CreateResult(DungeonSettlementOutcome.Defeated, null, character);
        Assert.That(first.Items[0].Quantity, Is.EqualTo(int.MaxValue));
        Assert.That(second.Items[0].Quantity, Is.EqualTo(int.MaxValue));
        Assert.That(character.Equipment.MagicStoneId, Is.EqualTo(7));
        Assert.That(character.Backpack.Items[0].Quantity, Is.EqualTo(int.MaxValue));
    }
}
