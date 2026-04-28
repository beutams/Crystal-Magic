using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    /// <summary>
    /// 游戏全局配置
    /// </summary>
    [Serializable]
    [GameConfig]
    public class GameConfig
    {
        /// <summary>初始金币</summary>
        public int StartingGold = 100;

        /// <summary>背包初始容量</summary>
        public int InitialBackpackSize = 20;

        /// <summary>仓库初始容量（-1 表示无限）</summary>
        public int InitialStashSize = -1;

        /// <summary>最大存档数量</summary>
        public int MaxSaveSlots = 20;
    }
}
