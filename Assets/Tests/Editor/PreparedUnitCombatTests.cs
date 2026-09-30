using System;
using System.IO;
using System.Linq;
using CrystalMagic.Editor.Data;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PreparedUnitCombatTests
{
    [TestCase(11)]
    [TestCase(29)]
    [TestCase(25)]
    [TestCase(21)]
    [TestCase(19)]
    [TestCase(13)]
    [TestCase(12)]
    public void PreparedUnitGraphsCompileAndUseTheirOwnSkills(int unitId)
    {
        var binding = PreparedUnitCombatRangeSync.Units.Single(u => u.Id == unitId);
        JToken script = Row("StateScript", unitId);
        Assert.That(StateScriptCompiler.TryBuildRegistry(new[] { script.ToObject<StateScriptData>() },
            out var scripts, out string error), Is.True, error);
        scripts.Dispose();
        Assert.That(BehaviorTreeCompiler.TryBuildRegistry(new[] { Row("BehaviorTree", unitId).ToObject<BehaviorTreeData>() },
            out var trees, out error), Is.True, error);
        trees.Dispose();

        JToken profile = Table("UnitAnimationProfile")["Rows"].Single(r => (int)r["UnitDataId"] == unitId);
        string[] animations = profile["Animations"].Select(a => (string)a["Name"]).ToArray();
        foreach (JToken node in script["Graphs"].SelectMany(g => g["Nodes"]))
        {
            if ((string)node["SetterKey"] is "unit.animation.play" or "unit.animation.setName")
                Assert.That(animations, Does.Contain((string)node["Value"]["Literal"]["String"]), (string)node["Guid"]);
        }
        for (int i = 0; i < binding.SkillIds.Length; i++)
        {
            JToken graph = script["Graphs"].Single(g => (string)g["Name"] == $"Attack {i + 1:00}");
            Assert.That((int)graph["Nodes"].Single(n => (string)n["Type"] == "RequestSkill")["SkillId"]["Literal"]["Int"],
                Is.EqualTo(binding.SkillIds[i]));
            Assert.That(Skill(binding.SkillIds[i]).IsMonsterSkill, Is.True);
        }
    }

    [Test]
    public void AllMarkersAreDisabledAndMatchSavedCombatRanges()
    {
        JObject skills = Table("Skill");
        JObject behaviors = Table("BehaviorTree");
        JObject syncedSkills = (JObject)skills.DeepClone();
        JObject syncedBehaviors = (JObject)behaviors.DeepClone();
        int attacks = 0;
        foreach (var binding in PreparedUnitCombatRangeSync.Units)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Res/Prefab/Unit/{binding.Name}.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Count(c => c.name.StartsWith("Atk", StringComparison.Ordinal)),
                Is.EqualTo(binding.SkillIds.Length));
            for (int i = 1; i <= binding.SkillIds.Length; i++)
            {
                Collider marker = PreparedUnitCombatRangeSync.Marker(prefab, "Atk" + i);
                Assert.That(marker.enabled, Is.False);
                Assert.That(marker.isTrigger, Is.True);
                attacks++;
            }
            PreparedUnitCombatRangeSync.Apply(binding, prefab, syncedSkills, syncedBehaviors);
        }
        Assert.That(attacks, Is.EqualTo(18));
        // Collider floats have single precision; compare numeric values with a tolerance.
        CompareJson(skills, syncedSkills);
        CompareJson(behaviors, syncedBehaviors);
    }

    [TestCase(55, "Arrow01")]
    [TestCase(67, "Arrow01")]
    [TestCase(68, "Arrow02")]
    public void ShotsAreIndependentNonPiercingProjectiles(int skillId, string visualName)
    {
        SkillData skill = Skill(skillId);
        Assert.That(skill.InputType, Is.EqualTo(SkillInputType.MousePosition));
        Assert.That(skill.EffectChain, Has.Length.EqualTo(1));
        var arrow = skill.EffectChain[0] as SpawnProjectileEffectData;
        Assert.That(arrow, Is.Not.Null);
        Assert.That(arrow.VisualPrefabName, Is.EqualTo(visualName));
        Assert.That(arrow.CanPierce, Is.False);
        Assert.That(arrow.MaxRange, Is.GreaterThan(8));
        Assert.That(arrow.CollisionTargetConditions, Has.Count.EqualTo(1));
        Assert.That(arrow.CollisionTargetConditions[0].Inputs[0].GetterKey, Is.EqualTo("unit.faction.isEnemyTo"));
        Assert.That(arrow.OnCollisionEffects.Single(), Is.TypeOf<DamageEffectData>());
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Res/Prefab/VFX/{visualName}.prefab");
        Assert.That(prefab.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
        Assert.That(prefab.GetComponent<SpriteEffectAnimationAuthoring>(), Is.Null);
    }

    [Test]
    public void PriestHasRealAttackAndHealAnimationsAndDoesNotHealEnemiesOrCorpses()
    {
        JToken heal = Row("Skill", 57)["EffectChain"][0];
        Assert.That((string)heal["$type"], Does.Contain("AreaSearchEffectData"));
        Assert.That((float)heal["Radius"], Is.EqualTo(4));
        JToken relation = heal["TargetConditions"].Single(c => (string)c["Inputs"][0]["GetterKey"] == "unit.faction.isEnemyTo");
        Assert.That((string)relation["CompareType"], Is.EqualTo("IsFalse"));
        JToken alive = heal["TargetConditions"].Single(c => (string)c["Inputs"][0]["GetterKey"] == "unit.vitality.currentHealth");
        Assert.That((string)alive["CompareType"], Is.EqualTo("GreaterThan"));
        Assert.That((float)alive["Inputs"][1]["Literal"]["Float"], Is.Zero);
        Assert.That(heal["OnAfterSearch"].Any(e => ((string)e["$type"]).Contains("HealEffectData")), Is.True);
        JToken graph = Row("StateScript", 25)["Graphs"].Single(g => (string)g["Name"] == "Heal");
        Assert.That((int)graph["Nodes"].Single(n => (string)n["Type"] == "RequestSkill")["SkillId"]["Literal"]["Int"], Is.EqualTo(57));
        foreach (string name in new[] { "PriestAttack", "PriestHeal" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Res/Prefab/VFX/{name}.prefab");
            var effect = prefab.GetComponent<SpriteEffectAnimationAuthoring>();
            Assert.That(effect.EnterClip, Is.Not.Null);
            Assert.That(effect.LoopClip, Is.Null, "One-shot effects must expire.");
            var library = AssetDatabase.LoadAssetAtPath<UnitAnimationFrameLibrary>("Assets/Res/Data/UnitAnimationFrameLibrary.asset");
            var track = library.Find(effect.EnterClip);
            Assert.That(track, Is.Not.Null);
            Assert.That(track.Sprites, Is.Not.Empty);
            Assert.That(track.Sprites.All(sprite => sprite != null), Is.True);
        }
    }

    [Test]
    public void KnightDropsGuardWhileAttackingAndRestoresGuardWhenIdle()
    {
        JToken graphs = Row("StateScript", 19)["Graphs"];
        Assert.That(graphs.Any(g => (string)g["Name"] == "Shield Guard"), Is.True);
        Assert.That(graphs.Any(g => (string)g["Name"] == "Block Reaction"), Is.True);
        foreach (JToken attack in graphs.Where(g => ((string)g["Name"]).StartsWith("Attack", StringComparison.Ordinal)))
        {
            JToken remove = attack["Nodes"].Single(n => (string)n["SetterKey"] == "unit.buffs.remove");
            Assert.That((int)remove["Value"]["Literal"]["Int"], Is.EqualTo(12));
            Assert.That(attack["Edges"].Any(e => ((string)e["OutputNodeGuid"]).EndsWith("_active_true", StringComparison.Ordinal) &&
                (string)e["InputNodeGuid"] == (string)remove["Guid"]), Is.True);
        }
    }

    private static void CompareJson(JToken expected, JToken actual)
    {
        if (expected is JValue value && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer))
        {
            Assert.That((double)actual, Is.EqualTo((double)expected).Within(.0001), expected.Path);
            return;
        }
        if (expected is JValue)
        {
            Assert.That(JToken.DeepEquals(expected, actual), Is.True, expected.Path);
            return;
        }
        Assert.That(actual.Children().Count(), Is.EqualTo(expected.Children().Count()), expected.Path);
        using var left = expected.Children().GetEnumerator();
        using var right = actual.Children().GetEnumerator();
        while (left.MoveNext() && right.MoveNext())
            CompareJson(left.Current, right.Current);
    }

    private static JObject Table(string name) => JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, $"Res/Data/{name}DataTable.json")));
    private static JToken Row(string table, int id) => Table(table)["Rows"].Single(r => (int)r["Id"] == id);
    private static SkillData Skill(int id) => Row("Skill", id).ToObject<SkillData>(
        JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
}
