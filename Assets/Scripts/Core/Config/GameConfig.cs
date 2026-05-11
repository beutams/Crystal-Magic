using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    public class GameConfig
    {
        public int StartingGold = 100;
        public string MoneyIconPath = "Assets/Res/Sprites/Buy.png";
        public int InitialBackpackSize = 20;
        public int InitialStashSize = -1;
        public int MaxSaveSlots = 20;
    }
}
