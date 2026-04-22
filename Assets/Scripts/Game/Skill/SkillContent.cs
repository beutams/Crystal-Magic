using UnityEngine;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    /// <summary>
    /// 效果执行时的上下文（施法者、目标等由技能系统在调用 Execute 前填充）
    /// </summary>
    public class SkillContent
    {
        public bool HasPosition { get; set; }

        public Vector3 Position { get; set; }

        public EntityManager EntityManager { get; set; }

        public bool HasOriginEntity { get; set; }

        public Entity OriginEntity { get; set; }

        public bool HasTargetEntity { get; set; }

        public Entity TargetEntity { get; set; }

        public bool HasTarget {  get; set; }
        
        public GameObject Target { get; set; }

        public GameObject Origin { get; set; }

        public SkillContent Clone()
        {
            return (SkillContent)MemberwiseClone();
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
