using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed class SavePlayerSnapshotTests
{
    private const string LegacyEmptyTownSave = "{\"Location\":{\"AreaType\":0},\"Player\":{\"SaveId\":-1,\"UnitDataId\":-1,\"Faction\":0,\"X\":0,\"Y\":0,\"Z\":0,\"Health\":0,\"Mana\":0}}";

    [Test]
    public void FreshSaveRoundTripKeepsPlayerSnapshotAbsent()
    {
        var original = new SaveData { Character = new CharacterData() };
        SaveData loaded = RoundTrip(original);
        Assert.That(original.Player, Is.Null);
        Assert.That(loaded.Player, Is.Null);
        Assert.That(loaded.Character, Is.Not.Null);
        Assert.That(RoundTrip(loaded).Player, Is.Null);
    }

    [Test]
    public void ExplicitSnapshotAtOriginWithZeroVitalsIsPreserved()
    {
        SaveData loaded = RoundTrip(new SaveData { Player = new UnitRuntimeData() });
        Assert.That(loaded.Player, Is.Not.Null);
        Assert.That(loaded.Player.X, Is.Zero);
        Assert.That(loaded.Player.Health, Is.Zero);
    }

    [Test]
    public void ClearingSnapshotForTownReturnSurvivesSerialization()
    {
        SaveData loaded = RoundTrip(new SaveData { Player = new UnitRuntimeData { X = 8, Health = 100 } });
        loaded.Player = null;
        Assert.That(RoundTrip(loaded).Player, Is.Null);
    }

    [Test]
    public void LegacyFreshTownPlaceholderIsNotRestoredAsAPlayerPosition()
    {
        SaveData loaded = JsonUtility.FromJson<SaveData>(LegacyEmptyTownSave);
        Assert.That(loaded.Player, Is.Null);
        Assert.That(RoundTrip(loaded).Player, Is.Null);
    }

    [Test]
    public void LegacyTownPositionWithNoCombatVitalsIsPreserved()
    {
        SaveData loaded = JsonUtility.FromJson<SaveData>(LegacyEmptyTownSave.Replace("\"X\":0", "\"X\":56.5"));
        Assert.That(loaded.Player, Is.Not.Null);
        Assert.That(loaded.Player.X, Is.EqualTo(56.5f));
        Assert.That(loaded.Player.Health, Is.Zero);
    }

    [Test]
    public void LegacyCombatSnapshotIsPreservedEvenAtOriginWithZeroVitals()
    {
        SaveData loaded = JsonUtility.FromJson<SaveData>(LegacyEmptyTownSave.Replace("\"AreaType\":0", "\"AreaType\":1"));
        Assert.That(loaded.Player, Is.Not.Null);
        Assert.That(loaded.Player.Health, Is.Zero);
    }

    [Test]
    public void AbsentSnapshotLeavesAuthoredSpawnAndVitalsUnchanged()
    {
        using var world = new World("New save spawn regression");
        EntityManager manager = world.EntityManager;
        Entity player = manager.CreateEntity(typeof(LocalTransform), typeof(UnitVitalityComponent));
        var spawn = new float3(36.454f, -10.463f, -0.01f);
        manager.SetComponentData(player, LocalTransform.FromPosition(spawn));
        manager.SetComponentData(player, new UnitVitalityComponent { BaseMaxHealth = 100, CurrentHealth = 100 });
        MethodInfo apply = typeof(GameRuntimeStateUtility).GetMethod("ApplyUnitRuntimeData", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(apply, Is.Not.Null);
        foreach (SaveData data in new[] { RoundTrip(new SaveData()), JsonUtility.FromJson<SaveData>(LegacyEmptyTownSave) })
        {
            apply.Invoke(null, new object[] { manager, player, data.Player });
            Assert.That(manager.GetComponentData<LocalTransform>(player).Position, Is.EqualTo(spawn));
            Assert.That(manager.GetComponentData<UnitVitalityComponent>(player).CurrentHealth, Is.EqualTo(100));
        }
    }

    private static SaveData RoundTrip(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
}
