using System.Collections.Generic;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    [ReadOnlyData]
    [System.Serializable]
    public class UnitData : DataRow
    {
        public string Name;
        public string Description;
        public string PrefabPath;
        public List<UnitModuleData> Modules = new();

        public T GetModule<T>() where T : UnitModuleData
        {
            if (Modules == null)
            {
                return null;
            }

            for (int i = 0; i < Modules.Count; i++)
            {
                if (Modules[i] is T module)
                {
                    return module;
                }
            }

            return null;
        }

        public T GetOrCreateModule<T>() where T : UnitModuleData, new()
        {
            T module = GetModule<T>();
            if (module != null)
            {
                return module;
            }

            Modules ??= new List<UnitModuleData>();
            module = new T();
            Modules.Add(module);
            return module;
        }

        public void NormalizeModules()
        {
            Modules ??= new List<UnitModuleData>();

            for (int i = 0; i < Modules.Count; i++)
            {
                if (Modules[i] is UnitBuffModuleData buffModule)
                    buffModule.Buffs ??= new List<UnitInitialBuffEntry>();
            }
        }
    }

    [System.Serializable]
    public abstract class UnitModuleData
    {
    }

    [System.Serializable]
    public sealed class UnitMoveModuleData : UnitModuleData
    {
        public float BaseMoveSpeed = 5f;
        public float BaseMaxAcceleration = 30f;
    }

    [System.Serializable]
    public sealed class UnitVitalityModuleData : UnitModuleData
    {
        public float BaseMaxHealth = 100f;
        public float BaseHealthRegenPerSecond;
        public float BaseDefense;
    }

    [System.Serializable]
    public sealed class UnitAttackModuleData : UnitModuleData
    {
        public float BaseAttackPower = 10f;
        public float BaseSkillRange = 1f;
        public float BaseActionSpeedBonus;
        public float BaseChantSpeedBonus;
    }

    [System.Serializable]
    public sealed class UnitManaModuleData : UnitModuleData
    {
        public float BaseMaxMp = 50f;
        public float BaseMpRegenPerSecond;
    }

    [System.Serializable]
    public sealed class UnitFactionModuleData : UnitModuleData
    {
        public UnitFactionType Faction = UnitFactionType.Friend;
    }

    [System.Serializable]
    public sealed class UnitPerceptionModuleData : UnitModuleData
    {
        public float SearchRadius = 8f;
    }

    [System.Serializable]
    public sealed class UnitBuffModuleData : UnitModuleData
    {
        public List<UnitInitialBuffEntry> Buffs = new();
    }

    [System.Serializable]
    public sealed class UnitInitialBuffEntry
    {
        public int BuffId = -1;
        public float DurationSeconds = -1f;
        public int StackCount = 1;
    }

    [System.Serializable]
    public class UnitDropModuleData : UnitModuleData
    {
        public int DropDataId = -1;
    }

    [System.Serializable] public sealed class UnitDungeonFootprintModuleData : UnitModuleData { public int Width = 1; public int Height = 1; }
}
