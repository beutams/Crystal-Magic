using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public enum SkillTriggerSource : byte
    {
        None = 0,
        ActiveCast = 1,
        PassiveHook = 2,
        BuffHook = 3,
        Script = 4,
    }

    public enum SkillHookType : byte
    {
        None = 0,
        OnSpawn = 1,
        BeforeDeath = 2,
        AfterDeath = 3,
        OnHitTarget = 4,
        OnDamaged = 5,
        OnKillTarget = 6,
        OnCastStart = 7,
        OnCastComplete = 8,
        OnBuffTick = 9,
    }

    /// <summary>
    /// Unified skill execution context shared by active casts, passive hooks, and script-driven triggers.
    /// </summary>
    public class SkillContent
    {
        public SkillTriggerSource TriggerSource { get; set; }

        public SkillHookType HookType { get; set; }

        public bool HasOtherEntity { get; set; }

        public Entity OtherEntity { get; set; }

        public float TriggerValue { get; set; }

        public bool HasPosition { get; set; }

        public Vector3 Position { get; set; }

        public EntityManager EntityManager { get; set; }

        public bool HasOriginEntity { get; set; }

        public Entity OriginEntity { get; set; }

        public int SourceSkillId { get; set; } = -1;

        public bool HasTargetEntity { get; set; }

        public Entity TargetEntity { get; set; }

        public bool HasTarget { get; set; }

        public GameObject Target { get; set; }

        public GameObject Origin { get; set; }

        public SkillModifierSet RuntimeModifiers { get; set; }

        public SkillContent Clone()
        {
            SkillContent copy = (SkillContent)MemberwiseClone();
            copy.RuntimeModifiers = RuntimeModifiers?.Clone();
            return copy;
        }

        public SkillContent CloneForTarget(Entity targetEntity, Vector3 targetPosition)
        {
            SkillContent copy = Clone();
            copy.HasTargetEntity = true;
            copy.TargetEntity = targetEntity;
            copy.HasPosition = true;
            copy.Position = targetPosition;
            return copy;
        }
    }
}
