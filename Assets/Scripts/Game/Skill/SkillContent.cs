using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    /// <summary>
    /// 效果执行时的上下文（施法者、目标等由技能系统在调用 Execute 前填充）
    /// </summary>
    public class SkillContent
    {
        public bool HasPosition { get; set; }

        public Vector3 Position { get; set; }

        public bool HasTarget {  get; set; }
        
        public GameObject Target { get; set; }

        public GameObject Origin { get; set; }

    }
}
