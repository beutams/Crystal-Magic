using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    [EditorLabel("全局配置")]
    public class GameConfig
    {
        [EditorLabel("初始金币")]
        public int StartingGold = 100;

        [EditorLabel("金币图标路径")]
        public string MoneyIconPath = "Assets/Res/Sprites/Buy.png";

        [EditorLabel("初始背包大小")]
        public int InitialBackpackSize = 20;

        [EditorLabel("初始仓库大小")]
        public int InitialStashSize = -1;

        [EditorLabel("最大存档槽位")]
        public int MaxSaveSlots = 20;

        [EditorLabel("战斗道具公共冷却")]
        public float BattlePropSharedCooldownSeconds = 3f;

        [EditorLabel("战斗道具槽位数")]
        public int BattlePropSlotCount = 4;

        [EditorLabel("战斗道具快捷槽位数")]
        public int BattlePropShortcutSlotCount = 4;

        [EditorLabel("掉落物拾取半径")]
        public float InteractionRange = 2f;

        [EditorLabel("唯一对象池上限")]
        public int SinglePoolMaxSize = 1;

        [EditorLabel("小型对象池上限")]
        public int SmallPoolMaxSize = 100;

        [EditorLabel("中型对象池上限")]
        public int MediumPoolMaxSize = 30;

        [EditorLabel("大型对象池上限")]
        public int LargePoolMaxSize = 10;
    }
}
