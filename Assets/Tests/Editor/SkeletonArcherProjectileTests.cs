using System.IO;
using System.Linq;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SkeletonArcherProjectileTests
{
    private const int ArcherId = 27;
    private const int ShotId = 49;
    private const string SpritePath = "Assets/Res/Sprites/Units/Arrow(Projectile)/Arrow01(32x32).png";
    private const string VisualPath = "Assets/Res/Prefab/VFX/Arrow01.prefab";

    [Test]
    public void ArcherGraphsCompileAndAttackRequestsArrowSkill()
    {
        JToken script = Row("StateScript", ArcherId);
        StateScriptData scriptData = script.ToObject<StateScriptData>();
        Assert.That(StateScriptCompiler.TryBuildRegistry(new[] { scriptData }, out var scripts, out string error),
            Is.True, error);
        scripts.Dispose();

        BehaviorTreeData tree = Row("BehaviorTree", ArcherId).ToObject<BehaviorTreeData>();
        Assert.That(BehaviorTreeCompiler.TryBuildRegistry(new[] { tree }, out var trees, out error), Is.True, error);
        trees.Dispose();

        JToken attack = script["Graphs"].Single(g => (string)g["Name"] == "Attack 01");
        JToken request = attack["Nodes"].Single(n => (string)n["Guid"] == "skeleton_archer_attack01_skill");
        Assert.That((int)request["SkillId"]["Literal"]["Int"], Is.EqualTo(ShotId));
        Assert.That((string)request["Input"]["Position"]["GetterKey"], Is.EqualTo("unit.transform.positionOf"));
        Assert.That(attack["Edges"].Any(e =>
            (string)e["OutputNodeGuid"] == "skeleton_archer_attack01_windup" &&
            (string)e["InputNodeGuid"] == "skeleton_archer_attack01_skill"), Is.True);
    }

    [Test]
    public void ArrowIsNonPiercingAndDamagesOnlyAnEnemyOnCollision()
    {
        var serializer = JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        SkillData skill = Row("Skill", ShotId).ToObject<SkillData>(serializer);
        Assert.That(skill.IsMonsterSkill, Is.True);
        Assert.That(skill.InputType, Is.EqualTo(SkillInputType.MousePosition));
        Assert.That(skill.EffectChain, Has.Length.EqualTo(1));
        Assert.That(skill.EffectChain[0], Is.TypeOf<SpawnProjectileEffectData>());
        var arrow = (SpawnProjectileEffectData)skill.EffectChain[0];
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Res/Prefab/Projectile/{arrow.ProjectilePrefabName}.prefab"), Is.Not.Null);
        Assert.That(arrow.VisualPrefabName, Is.EqualTo("Arrow01"));
        Assert.That(arrow.Speed, Is.EqualTo(14));
        Assert.That(arrow.MaxRange, Is.EqualTo(12));
        Assert.That(arrow.CanPierce, Is.False);
        Assert.That(arrow.TriggerDestroyEffectsOnMaxRange, Is.False);
        Assert.That(arrow.OnDestroyEffects, Is.Empty);
        Assert.That(arrow.OnCollisionEffects, Has.Length.EqualTo(1));
        Assert.That(arrow.OnCollisionEffects[0], Is.TypeOf<DamageEffectData>());
        var damage = (DamageEffectData)arrow.OnCollisionEffects[0];
        Assert.That(damage.TargetSource, Is.EqualTo(DamageTargetSource.CurrentTarget));
        Assert.That(damage.ValueSource, Is.EqualTo(DamageValueSource.AttackPower));
        Assert.That(damage.DamageCoefficient, Is.EqualTo(1));
        Assert.That(damage.FlatDamageBonus, Is.Zero);
        Assert.That(damage.Element, Is.EqualTo(ElementType.None));
        Assert.That(arrow.CollisionTargetConditions, Has.Count.EqualTo(1));
        JToken filter = Row("Skill", ShotId)["EffectChain"][0]["CollisionTargetConditions"][0];
        Assert.That((string)filter["CompareType"], Is.EqualTo("IsTrue"));
        Assert.That((string)filter["Inputs"][0]["GetterKey"], Is.EqualTo("unit.faction.isEnemyTo"));
        Assert.That((string)filter["Inputs"][0]["Inputs"][0]["GetterKey"], Is.EqualTo("effect.context.originEntity"));
    }

    [Test]
    public void ArcherHoldsWithinShootingRangeBeforeChasing()
    {
        JToken nodes = Row("BehaviorTree", ArcherId)["Nodes"];
        JToken Node(string guid) => nodes.Single(n => (string)n["Guid"] == guid);
        string[] priorities = Node("skeleton_archer_selector")["ChildGuids"].Values<string>().ToArray();
        Assert.That(System.Array.IndexOf(priorities, "skeleton_archer_attack01"),
            Is.LessThan(System.Array.IndexOf(priorities, "skeleton_archer_hold_range")));
        Assert.That(System.Array.IndexOf(priorities, "skeleton_archer_hold_range"),
            Is.LessThan(System.Array.IndexOf(priorities, "skeleton_archer_chase")));
        foreach (string guid in new[] { "skeleton_archer_hit01", "skeleton_archer_hold_range_check" })
        {
            Assert.That((string)Node(guid)["Type"], Is.EqualTo("Check"));
            JToken range = Node(guid)["Conditions"][1];
            Assert.That((string)range["CompareType"], Is.EqualTo("LessOrEqual"));
            Assert.That((string)range["Inputs"][0]["OperationType"], Is.EqualTo("Distance"));
            Assert.That((float)range["Inputs"][1]["Literal"]["Float"], Is.EqualTo(8));
        }
        Assert.That((string)Node("skeleton_archer_hold_stop")["SetKey"], Is.EqualTo("unit.navigation.stop"));
        Assert.That(Node("skeleton_archer_hold_stop")["Inputs"].Count(), Is.EqualTo(1));
        Assert.That((string)Node("skeleton_archer_face_shot")["SetKey"], Is.EqualTo("unit.facing.setDirection"));
        Assert.That((string)Node("skeleton_archer_hold_idle")["Inputs"][0]["Literal"]["String"], Is.EqualTo("Idle"));
    }

    [Test]
    public void ArrowVisualUsesRequestedPixelSpriteWithoutAnimationLifetime()
    {
        var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
        Assert.That(prefab, Is.Not.Null);
        var renderer = prefab.GetComponent<SpriteRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(SpritePath));
        Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(16));
        // A static arrow lives until its projectile disappears; no empty animation controller may end it early.
        Assert.That(prefab.GetComponent<SpriteEffectAnimationAuthoring>(), Is.Null);
        Assert.That(prefab.GetComponent<Animator>(), Is.Null);
    }

    private static JToken Row(string table, int id) => JObject.Parse(File.ReadAllText(
        Path.Combine(Application.dataPath, $"Res/Data/{table}DataTable.json")))["Rows"]
        .Single(row => (int)row["Id"] == id);
}
