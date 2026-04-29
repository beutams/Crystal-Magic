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
                switch (Modules[i])
                {
                    case UnitStateMachineModuleData stateMachine:
                        stateMachine.States ??= new List<UnitStateConfig>();
                        break;
                }
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
        public float BaseDefense;
    }

    [System.Serializable]
    public sealed class UnitAttackModuleData : UnitModuleData
    {
        public float BaseAttackPower = 10f;
        public float BaseSkillRange = 1f;
    }

    [System.Serializable]
    public sealed class UnitManaModuleData : UnitModuleData
    {
        public float BaseMaxMp = 50f;
    }

    [System.Serializable]
    public sealed class UnitStateMachineModuleData : UnitModuleData
    {
        public List<UnitStateConfig> States = new();
    }

    [System.Serializable]
    public class UnitStateConfig
    {
        public string StateType = "";
        public List<UnitTransitionConfig> Transitions = new();
    }

    [System.Serializable]
    public class UnitTransitionConfig
    {
        public string TargetStateType = "";
        public List<ConditionConfig> Conditions = new();
    }
}
