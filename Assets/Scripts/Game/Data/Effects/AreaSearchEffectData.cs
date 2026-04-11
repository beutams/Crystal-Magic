using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 范围搜索效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class AreaSearchEffectData : EffectData
    {
        /// <summary>搜索半径（世界单位）</summary>
        public float Radius;

        /// <summary>扇形角度（度），360 = 整圆</summary>
        public float SectorAngleDegrees = 360f;

        /// <summary>扇形前向基准的 Yaw 偏移（度）</summary>
        public float ForwardYawOffsetDegrees;

        /// <summary>搜索中心相对施法者的偏移</summary>
        public Vector3 CenterOffset;

        /// <summary>最多选取目标数量</summary>
        public int MaxTargetCount;

        /// <summary>是否需要视线检测</summary>
        public bool LineOfSightRequired;

        public EffectData[] OnAfterSearch;
    }
}
