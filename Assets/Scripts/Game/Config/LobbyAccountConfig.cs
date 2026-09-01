using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    [EditorLabel("Lobby Account Config")]
    public class LobbyAccountConfig
    {
        [EditorLabel("Account Id")]
        public ulong accountId = 1;

        [EditorLabel("Username")]
        public string username = "TestPlayer";
    }
}
