using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    [EditorLabel("Behavior Tree Runtime Config")]
    public sealed class BehaviorTreeRuntimeConfig
    {
        [EditorLabel("Max Immediate Iterations Per Tick")]
        public int MaxImmediateIterationsPerTick = 256;
    }
}
