using System;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    /// <summary>
    /// Explicit authoring step: save the prefab, then synchronize its disabled range markers.
    /// No runtime colliders or automatic asset writes are added to the units.
    /// </summary>
    public static class PreparedUnitCombatRangeSync
    {
        private const string SkillPath = "Assets/Res/Data/SkillDataTable.json";
        private const string BehaviorPath = "Assets/Res/Data/BehaviorTreeDataTable.json";

        public readonly struct UnitBinding
        {
            public readonly int Id;
            public readonly string Name;
            public readonly string Prefix;
            public readonly int[] SkillIds;

            public UnitBinding(int id, string name, string prefix, params int[] skillIds)
            {
                Id = id;
                Name = name;
                Prefix = prefix;
                SkillIds = skillIds;
            }
        }

        public static readonly UnitBinding[] Units =
        {
            new(11, "Swordsman", "swordsman", 50, 51, 52),
            new(29, "Soldier", "soldier", 53, 54, 55),
            new(25, "Priest", "priest", 56),
            new(21, "Lancer", "lancer", 58, 59, 60),
            new(19, "Knight", "knight", 61, 62, 63),
            new(13, "Armored Axeman", "armored_axeman", 64, 65, 66),
            new(12, "Archer", "archer", 67, 68),
        };

        [MenuItem("Tools/Crystal Magic/Units/Sync Prepared Unit Combat Ranges")]
        public static void SyncSavedPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before syncing combat ranges.");

            JObject skills = JObject.Parse(File.ReadAllText(SkillPath));
            JObject behaviors = JObject.Parse(File.ReadAllText(BehaviorPath));
            // Validate every marker before writing either data table.
            foreach (UnitBinding unit in Units)
            {
                string path = $"Assets/Res/Prefab/Unit/{unit.Name}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new InvalidOperationException($"Missing prefab: {path}");
                Apply(unit, prefab, skills, behaviors);
            }

            DataFileUtility.WriteJsonText(SkillPath, skills.ToString(Formatting.Indented));
            DataFileUtility.WriteJsonText(BehaviorPath, behaviors.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(SkillPath);
            AssetDatabase.ImportAsset(BehaviorPath);
            Debug.Log("[Combat Ranges] Synced 7 saved prefabs. Box = melee damage/check; sphere = shooting/healing radius. Disabled marker colliders must remain disabled.");
        }

        // Public to permit EditMode tests without writing the project's data tables.
        public static void Apply(UnitBinding unit, GameObject prefab, JObject skills, JObject behaviors)
        {
            JToken tree = Row(behaviors, unit.Id);
            JArray nodes = (JArray)tree["Nodes"];
            Vector3 rootScale = prefab.transform.localScale;
            Require(rootScale.x > 0f && rootScale.y > 0f, unit.Name + ": root scale must be positive.");
            float holdRadius = float.PositiveInfinity;

            for (int index = 0; index < unit.SkillIds.Length; index++)
            {
                string markerName = $"Atk{index + 1}";
                Collider marker = Marker(prefab, markerName);
                JToken skill = Row(skills, unit.SkillIds[index]);
                JToken effect = skill["EffectChain"]?[0] ?? throw new InvalidOperationException("Missing effect chain.");
                JToken hit = nodes.Single(n => (string)n["Guid"] == $"{unit.Prefix}_hit{index + 1:00}");

                if (marker is BoxCollider box)
                {
                    Require((string)hit["Type"] == "HitCheck" &&
                        ((string)effect["$type"]).Contains("RectSearchEffectData"), markerName + ": not a melee binding.");
                    Vector2 center = LocalCenter(prefab.transform, box.transform, box.center);
                    Vector3 localScale = box.transform.localScale;
                    Vector2 size = new(Mathf.Abs(box.size.x * localScale.x), Mathf.Abs(box.size.y * localScale.y));
                    Require(size.x > 0f && size.y > 0f, markerName + ": size must be positive.");
                    hit["Center"] = XY(center.x, center.y);
                    hit["Size"] = XY(size.x, size.y);
                    hit["TargetPadding"] = 0f;
                    effect["CenterOffset"] = XYZ(center.x * rootScale.x, center.y * rootScale.y, 0f);
                    effect["Size"] = XY(size.x * rootScale.x, size.y * rootScale.y);
                    effect["UseHorizontalFacing"] = true;
                }
                else if (marker is SphereCollider sphere)
                {
                    Require((string)hit["Type"] == "Check", markerName + ": not a ranged binding.");
                    float radius = WorldRadius(prefab, sphere);
                    SetRange(hit, radius);
                    holdRadius = Mathf.Min(holdRadius, radius);
                    if (((string)effect["$type"]).Contains("SpawnProjectileEffectData"))
                        effect["MaxRange"] = radius + 4f;
                    // Priest's Atk1 is cast range, not the impact splash radius.
                }
                else
                    throw new InvalidOperationException(markerName + ": expected BoxCollider or SphereCollider.");
            }

            JToken hold = nodes.FirstOrDefault(n => (string)n["Guid"] == unit.Prefix + "_hold_range_check");
            if (hold != null && !float.IsPositiveInfinity(holdRadius))
                SetRange(hold, holdRadius);

            if (unit.Id == 25)
            {
                SphereCollider heal = Marker(prefab, "Heal1") as SphereCollider;
                Require(heal != null, "Priest/Heal1 must use a SphereCollider.");
                float radius = WorldRadius(prefab, heal);
                Row(skills, 57)["EffectChain"][0]["Radius"] = radius;
                SetRange(nodes.Single(n => (string)n["Guid"] == "priest_heal_allies_check"), radius);
            }
        }

        public static Collider Marker(GameObject prefab, string name)
        {
            // These are explicit prefab authoring markers, not runtime UI bindings.
            Transform child = prefab.transform.Find(name);
            Require(child != null, prefab.name + ": missing " + name);
            Collider collider = child.GetComponent<Collider>();
            Require(collider != null && !collider.enabled && child.gameObject.activeSelf,
                prefab.name + "/" + name + ": keep the object active and the collider disabled.");
            Require(Quaternion.Angle(child.localRotation, Quaternion.identity) < .01f,
                prefab.name + "/" + name + ": rotated markers are not supported by axis-aligned combat queries.");
            return collider;
        }

        private static float WorldRadius(GameObject prefab, SphereCollider sphere)
        {
            Vector2 center = LocalCenter(prefab.transform, sphere.transform, sphere.center);
            Require(center.sqrMagnitude < .000001f,
                prefab.name + "/" + sphere.name + ": keep shooting/healing spheres centered; adjust Radius only.");
            Vector3 scale = Vector3.Scale(prefab.transform.localScale, sphere.transform.localScale);
            Require(Mathf.Abs(scale.x - scale.y) < .0001f && scale.x > 0f,
                prefab.name + ": circular ranges require equal positive X/Y scale.");
            float radius = sphere.radius * scale.x;
            Require(!float.IsNaN(radius) && !float.IsInfinity(radius) && radius > 0f,
                prefab.name + ": radius must be finite and positive.");
            return radius;
        }

        private static Vector2 LocalCenter(Transform root, Transform marker, Vector3 center)
        {
            Vector3 local = root.InverseTransformPoint(marker.TransformPoint(center));
            return new Vector2(local.x, local.y);
        }

        private static void SetRange(JToken check, float radius)
        {
            JToken comparison = check["Conditions"].Single(c =>
                (string)c["CompareType"] == "LessOrEqual" &&
                (string)c["Inputs"]?[0]?["OperationType"] == "Distance");
            comparison["Inputs"][1]["Literal"]["Float"] = radius;
        }

        private static JToken Row(JObject table, int id) => table["Rows"].Single(r => (int)r["Id"] == id);
        private static JObject XY(float x, float y) => new() { ["x"] = x, ["y"] = y };
        private static JObject XYZ(float x, float y, float z) => new() { ["x"] = x, ["y"] = y, ["z"] = z };
        private static void Require(bool valid, string message)
        {
            if (!valid)
                throw new InvalidOperationException(message);
        }
    }
}
