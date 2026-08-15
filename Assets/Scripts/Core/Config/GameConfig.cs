using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    [EditorLabel("Global Config")]
    public class GameConfig
    {
        [EditorLabel("Starting Gold")]
        public int StartingGold = 100;

        [EditorLabel("Money Icon Path")]
        public string MoneyIconPath = "Assets/Res/Sprites/Buy.png";

        [EditorLabel("Initial Backpack Size")]
        public int InitialBackpackSize = 20;

        [EditorLabel("Initial Stash Size")]
        public int InitialStashSize = -1;

        [EditorLabel("Max Save Slots")]
        public int MaxSaveSlots = 20;

        [EditorLabel("Battle Prop Shared Cooldown Seconds")]
        public float BattlePropSharedCooldownSeconds = 3f;

        [EditorLabel("Battle Prop Slot Count")]
        public int BattlePropSlotCount = 4;

        [EditorLabel("Battle Prop Shortcut Slot Count")]
        public int BattlePropShortcutSlotCount = 4;

        [EditorLabel("Interaction Range")]
        public float InteractionRange = 2f;

        [EditorLabel("Behavior Tree Max Immediate Iterations Per Tick")]
        public int BehaviorTreeMaxImmediateIterationsPerTick = 256;

        [EditorLabel("Single Pool Max Size")]
        public int SinglePoolMaxSize = 1;

        [EditorLabel("Small Pool Max Size")]
        public int SmallPoolMaxSize = 100;

        [EditorLabel("Medium Pool Max Size")]
        public int MediumPoolMaxSize = 30;

        [EditorLabel("Large Pool Max Size")]
        public int LargePoolMaxSize = 10;
    }
}