using System;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Shared, deterministic MinCount + weighted cost-budget selection.</summary>
public static class UnitRosterUtility
{
    public struct Choice
    {
        public FixedString128Bytes Unit;
        public int MinCount;
        public int Cost;
        public int Weight;
    }

    // Minimum members count toward the budget. The final random draw may overshoot it.
    // Returns indices so map generation can retain its resolved UnitData objects.
    public static bool Build(NativeArray<Choice> choices, float budget, float budgetMultiplier,
        ref Unity.Mathematics.Random random, ref NativeList<int> result)
    {
        result.Clear();
        if (!math.isfinite(budgetMultiplier) || budgetMultiplier <= 0f)
            return false;
        // All callers supply an unscaled budget (which may already include wave growth).
        budget *= budgetMultiplier;
        if (!math.isfinite(budget) || budget <= 0f || choices.Length == 0)
            return false;
        long totalWeight = 0;
        long minimumCount = 0;
        for (int i = 0; i < choices.Length; i++)
        {
            Choice choice = choices[i];
            if (choice.Unit.Length == 0 || choice.Cost < 1 || choice.Weight < 1 || choice.MinCount < 0)
                return false;
            totalWeight += choice.Weight;
            minimumCount += choice.MinCount;
        }
        if (totalWeight > int.MaxValue || minimumCount > int.MaxValue || budget >= int.MaxValue)
            return false;
        long totalCost = 0;
        for (int i = 0; i < choices.Length; i++)
        {
            for (int n = 0; n < choices[i].MinCount; n++)
                result.Add(i);
            totalCost += (long)choices[i].MinCount * choices[i].Cost;
        }
        while (totalCost < budget)
        {
            int roll = random.NextInt((int)totalWeight);
            for (int i = 0; i < choices.Length; i++)
            {
                roll -= choices[i].Weight;
                if (roll >= 0) continue;
                result.Add(i);
                totalCost += choices[i].Cost;
                break;
            }
        }
        return true;
    }

    // The generic state-script node reads this template from any entity's blackboard.
    public static void WriteTemplate(EntityManager manager, Entity entity, string key, UnitRosterTemplateData template)
    {
        UnitVariableSource.TrySetValue(manager, entity, key + ".costLimit", UnitValue.FromInt(template?.CostLimit ?? 1));
        int count = 0;
        if (template?.Members != null)
        {
            foreach (OpenFieldDungeonSquadMemberData member in template.Members)
            {
                if (member == null || string.IsNullOrWhiteSpace(member.UnitName)) continue;
                UnitData unit = DataComponent.Instance.Find<UnitData>(row => row.Name == member.UnitName);
                if (unit == null || string.IsNullOrWhiteSpace(unit.PrefabPath)) continue;
                string entry = key + "." + count++;
                UnitVariableSource.TrySetValue(manager, entity, entry + ".unit", UnitValue.FromString(Path.GetFileNameWithoutExtension(unit.PrefabPath)));
                UnitVariableSource.TrySetValue(manager, entity, entry + ".minCount", UnitValue.FromInt(math.max(0, member.MinCount)));
                UnitVariableSource.TrySetValue(manager, entity, entry + ".cost", UnitValue.FromInt(math.max(1, member.Cost)));
                UnitVariableSource.TrySetValue(manager, entity, entry + ".weight", UnitValue.FromInt(math.max(1, member.Weight)));
            }
        }
        UnitVariableSource.TrySetValue(manager, entity, key + ".count", UnitValue.FromInt(count));
    }
}
