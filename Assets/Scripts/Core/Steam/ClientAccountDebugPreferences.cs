using System;
using System.Globalization;
using CrystalMagic.Game.Config;
using UnityEngine;

namespace CrystalMagic.Core
{
    // Local-machine debug choice only. CLI overrides it; release builds ignore it.
    public static class ClientAccountDebugPreferences
    {
        private const string ModeKey = "CrystalMagic.Debug.AccountMode";
        private const string IdKey = "CrystalMagic.Debug.LocalAccountId";

        public static LobbyAccountConfig Apply(LobbyAccountConfig source, bool developmentAllowed)
        {
            int mode = developmentAllowed ? PlayerPrefs.GetInt(ModeKey, 0) : 0;
            return mode is 1 or 2 && TrySelect(source, mode == 1, PlayerPrefs.GetString(IdKey, "1"),
                developmentAllowed, out var selected, out _) ? selected : source;
        }

        public static bool TrySelect(LobbyAccountConfig source, bool local, string idText,
            bool developmentAllowed, out LobbyAccountConfig selected, out string error)
        {
            selected = null;
            error = string.Empty;
            if (!developmentAllowed)
            {
                error = "账号切换仅限编辑器和 Development Build。";
                return false;
            }
            ulong id = 1;
            if (local && (!ulong.TryParse(idText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id) ||
                id == 0 || id >= ClientAccountIdentity.LocalNamespace))
            {
                error = "测试账号编号须为 1～9223372036854775807 的整数。";
                return false;
            }
            selected = ClientAccountPolicy.ForLaunch(source, Array.Empty<string>(), true);
            selected.useLocalTestAccount = local;
            selected.allowDevelopmentFallback = false; // Explicit Steam must never silently become local.
            if (local) { selected.accountId = id; selected.username = "Tester" + id.ToString(CultureInfo.InvariantCulture); }
            return true;
        }

        internal static void Save(LobbyAccountConfig selected)
        {
            if (!ClientAccountPolicy.DevelopmentAccountsAllowed) return;
            PlayerPrefs.SetInt(ModeKey, selected.useLocalTestAccount ? 1 : 2);
            if (selected.useLocalTestAccount)
                PlayerPrefs.SetString(IdKey, selected.accountId.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }
    }
}
