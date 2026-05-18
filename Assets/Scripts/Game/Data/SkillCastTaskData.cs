using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Data
{
    public enum SkillCastHookPoint : byte
    {
        [EditorLabel("前摇开始前")]
        BeforeWindup = 0,
        [EditorLabel("咏唱结束前")]
        BeforeChantEnd = 1,
        [EditorLabel("技能执行前")]
        BeforeExecute = 2,
        [EditorLabel("后摇开始前")]
        BeforeRecovery = 3,
        [EditorLabel("后摇结束后")]
        AfterRecovery = 4,
    }

    [Serializable]
    public abstract class SkillCastTaskData
    {
        [EditorLabel("挂点")]
        public SkillCastHookPoint HookPoint;
    }

    [Serializable]
    public sealed class DoubleExecuteSkillCastTaskData : SkillCastTaskData
    {
        [EditorLabel("延迟秒数")]
        public float DelaySeconds = 0.1f;

        [EditorLabel("额外修正")]
        public List<SkillModifierEntry> RuntimeModifiers = new();

        public DoubleExecuteSkillCastTaskData()
        {
            HookPoint = SkillCastHookPoint.BeforeRecovery;
        }
    }

    [Serializable]
    public sealed class ApplyRuntimeBuffSkillCastTaskData : SkillCastTaskData
    {
        [EditorLabel("Buff Id")]
        public int BuffId = -1;

        [EditorLabel("层数")]
        public int StackCount = 1;

        [EditorLabel("受伤时消耗")]
        public bool ConsumeOnDamageTaken = true;

        [EditorLabel("可触发次数")]
        public int RemainingTriggerCount = 1;

        public ApplyRuntimeBuffSkillCastTaskData()
        {
            HookPoint = SkillCastHookPoint.BeforeWindup;
        }
    }
}
