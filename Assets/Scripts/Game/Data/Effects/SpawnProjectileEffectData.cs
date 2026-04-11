using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 创建投射物效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class SpawnProjectileEffectData : EffectData
    {
        /// <summary>投射物配置</summary>
        public GameObject Projectile;

        /// <summary>投射物飞行速度（世界单位/秒）</summary>
        public float Speed;

        /// <summary>最大飞行距离（世界单位），0 = 不限</summary>
        public float MaxRange;

        /// <summary>相对施法者的生成偏移</summary>
        public Vector3 SpawnOffset;

        /// <summary>穿透目标数量，0 = 不穿透</summary>
        public int PiercingCount;

        /// <summary>追踪强度，0 = 直线飞行，>0 = 追踪目标</summary>
        public float HomingStrength;

        [SerializeReference]
        public EffectData[] OnCollisionEffects;
        [SerializeReference]
        public EffectData[] OnDestoryEffects;
    }
}
