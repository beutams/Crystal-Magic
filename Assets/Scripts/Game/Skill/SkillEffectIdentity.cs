using System;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    /// <summary>Shared identity of a cast, projectile branch and individual impact effect.</summary>
    [Serializable]
    public struct SkillEffectIdentity : IEquatable<SkillEffectIdentity>
    {
        public Guid CasterId;
        public uint CastFrame;
        public int CastOrdinal;
        public ulong Path;
        public ulong ProjectilePath;
        public uint ImpactSequence;
        public Guid ImpactTargetId;
        public Guid TargetId;
        public byte Phase;
        public byte Valid;

        public static SkillEffectIdentity Create(EntityManager manager, Entity caster, uint frame, int ordinal) => new()
        {
            CasterId = GetNetworkId(manager, caster), CastFrame = frame, CastOrdinal = ordinal, Valid = 1,
        };

        public readonly SkillEffectIdentity Child(int ordinal)
        {
            SkillEffectIdentity child = this;
            child.Path = unchecked((Path ^ (ulong)(ordinal + 1)) * 1099511628211UL);
            return child;
        }

        public readonly SkillEffectIdentity Impact(byte phase, uint sequence, Guid target)
        {
            SkillEffectIdentity impact = this;
            impact.ProjectilePath = Path;
            impact.Phase = phase;
            impact.ImpactSequence = sequence;
            impact.ImpactTargetId = target;
            impact.TargetId = target;
            return impact;
        }

        public readonly bool SameProjectile(in SkillEffectIdentity other) =>
            Valid != 0 && other.Valid != 0 && CasterId == other.CasterId &&
            CastFrame == other.CastFrame && CastOrdinal == other.CastOrdinal &&
            (Phase == 0 ? Path : ProjectilePath) == (other.Phase == 0 ? other.Path : other.ProjectilePath);

        public readonly bool Equals(SkillEffectIdentity other) =>
            Valid == other.Valid && CasterId == other.CasterId && CastFrame == other.CastFrame &&
            CastOrdinal == other.CastOrdinal && Path == other.Path && ProjectilePath == other.ProjectilePath &&
            ImpactSequence == other.ImpactSequence && ImpactTargetId == other.ImpactTargetId && TargetId == other.TargetId && Phase == other.Phase;
        public override readonly bool Equals(object obj) => obj is SkillEffectIdentity other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(
            HashCode.Combine(CasterId, CastFrame, CastOrdinal, Path, ProjectilePath, ImpactSequence, TargetId, Phase), ImpactTargetId);

        public static Guid GetNetworkId(EntityManager manager, Entity entity) =>
            entity != Entity.Null && manager.Exists(entity) && manager.HasComponent<NetworkIdentityComponent>(entity)
                ? manager.GetComponentData<NetworkIdentityComponent>(entity).id : Guid.Empty;
    }
}
