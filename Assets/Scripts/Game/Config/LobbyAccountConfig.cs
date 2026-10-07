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

        [EditorLabel("Use Local Test Account (Editor / Development Build)")]
        public bool useLocalTestAccount;

        [EditorLabel("Allow Development Fallback")]
        public bool allowDevelopmentFallback = true;

        [EditorLabel("Lobby IP")]
        public string lobbyAddress = "127.0.0.1";
        [EditorLabel("Lobby Port")]
        public int lobbyPort = 10002;
        [EditorLabel("Hosted TCP Advertised IP (empty = observed by lobby)")]
        public string hostedTcpAddress = "";
        [EditorLabel("Hosted TCP Listen Port (0 = automatic)")]
        public int hostedTcpPort;
    }
}
