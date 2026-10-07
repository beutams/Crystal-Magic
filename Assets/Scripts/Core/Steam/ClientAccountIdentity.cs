using System;
using System.Globalization;
using System.IO;
using CrystalMagic.Game.Config;

namespace CrystalMagic.Core
{
    /// <summary>One immutable identity per launch, shared by saves and lobby login.</summary>
    public readonly struct ClientAccountIdentity
    {
        public const ulong LocalNamespace = 1UL << 63;
        public ulong AccountId { get; }
        // Keep configured/save-folder IDs readable while making wire IDs disjoint from Steam IDs.
        public ulong NetworkAccountId => IsLocalTestAccount ? AccountId | LocalNamespace : AccountId;
        public static bool IsLocalNetworkId(ulong id) => (id & LocalNamespace) != 0;
        public string Name { get; }
        public bool IsLocalTestAccount { get; }
        public bool IsValid => AccountId != 0 && AccountId < LocalNamespace && !string.IsNullOrWhiteSpace(Name);

        public ClientAccountIdentity(ulong accountId, string name, bool isLocalTestAccount)
        {
            AccountId = accountId;
            Name = name ?? string.Empty;
            IsLocalTestAccount = isLocalTestAccount;
        }

        public string GetSaveFolder(string persistentDataPath)
        {
            string root = Path.Combine(persistentDataPath, "SaveData");
            if (!IsLocalTestAccount)
                return root; // Preserve the existing Steam save location; do not migrate files.
            if (!IsValid)
                throw new InvalidOperationException("Local test account ID must be nonzero before accessing saves.");
            return Path.Combine(root, "LocalTest", AccountId.ToString(CultureInfo.InvariantCulture));
        }
    }

    public static class ClientAccountPolicy
    {
        public static LobbyAccountConfig ForLaunch(LobbyAccountConfig source, string[] arguments, bool developmentAllowed)
        {
            source ??= new LobbyAccountConfig();
            var config = new LobbyAccountConfig
            {
                accountId = source.accountId, username = source.username,
                useLocalTestAccount = source.useLocalTestAccount,
                allowDevelopmentFallback = source.allowDevelopmentFallback,
                hostedTcpAddress = source.hostedTcpAddress, hostedTcpPort = source.hostedTcpPort,
                lobbyAddress = source.lobbyAddress, lobbyPort = source.lobbyPort,
            };
            for (int i = 0; arguments != null && i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if (argument == "-steamAccount")
                { config.useLocalTestAccount = false; config.allowDevelopmentFallback = false; }
                else if (argument == "-localAccount" && developmentAllowed)
                {
                    if (++i >= arguments.Length || !ulong.TryParse(arguments[i], out ulong id) || id == 0 || id >= ClientAccountIdentity.LocalNamespace)
                        throw new ArgumentException("-localAccount requires an ID between 1 and 9223372036854775807.");
                    config.useLocalTestAccount = true; config.accountId = id; config.username = "Tester" + id;
                }
                else if (argument == "-battleAddress" || argument == "-battlePort" || argument == "-lobbyAddress" || argument == "-lobbyPort")
                {
                    if (++i >= arguments.Length) throw new ArgumentException(argument + " requires a value.");
                    if (argument == "-battleAddress") config.hostedTcpAddress = arguments[i];
                    else if (argument == "-lobbyAddress") config.lobbyAddress = arguments[i];
                    else
                    {
                        if (!int.TryParse(arguments[i], out int port) || port < (argument == "-battlePort" ? 0 : 1) || port > 65535)
                            throw new ArgumentException(argument + " has an invalid port.");
                        if (argument == "-battlePort") config.hostedTcpPort = port; else config.lobbyPort = port;
                    }
                }
            }
            return config;
        }

        public static bool DevelopmentAccountsAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static bool ShouldSkipSteam(LobbyAccountConfig config, bool developmentAllowed)
            => developmentAllowed && config != null && config.useLocalTestAccount;

        public static ClientAccountIdentity Resolve(LobbyAccountConfig config, bool developmentAllowed,
            ulong steamId, string steamName)
        {
            bool steamAvailable = steamId != 0 && !string.IsNullOrWhiteSpace(steamName);
            bool useLocal = developmentAllowed && config != null &&
                (config.useLocalTestAccount || (!steamAvailable && config.allowDevelopmentFallback));
            if (!useLocal)
                return new ClientAccountIdentity(steamId, steamName, false);

            string name = string.IsNullOrWhiteSpace(config.username) ? "TestPlayer" : config.username.Trim();
            return new ClientAccountIdentity(config.accountId, name, true);
        }
    }
}
