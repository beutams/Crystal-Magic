#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define CRYSTAL_MAGIC_DISABLE_STEAMWORKS
#endif

using System;
using CrystalMagic.Game.Config;
using UnityEngine;
#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
using Steamworks;
#endif

namespace CrystalMagic.Core
{
    /// <summary>
    /// 管理 Steamworks 生命周期及本次启动的客户端身份；本地测试模式可跳过 Steam。
    /// </summary>
    public class SteamComponent : GameComponent<SteamComponent>
    {
        private bool initialized;
        private ulong steamId;
        private string personaName = string.Empty;
        private LobbyAccountConfig accountConfig;

        // Config (8) must precede account selection; Network (14) and SaveData (18) follow.
        public override int Priority => 9;
        public bool IsInitialized => initialized;
        public ulong SteamId => steamId;
        public string PersonaName => personaName;
        public ClientAccountIdentity Account { get; private set; }
        public LobbyAccountConfig LaunchConfig => accountConfig;
        public bool CanUseSteamP2P => initialized && Account.IsValid && !Account.IsLocalTestAccount;
        public string FailureReason { get; private set; } = "Steam has not been initialized.";

        public override void Initialize()
        {
            base.Initialize();
            Account = default;
            accountConfig = ClientAccountPolicy.ForLaunch(ConfigComponent.Instance.Get<LobbyAccountConfig>(),
                Environment.GetCommandLineArgs(), ClientAccountPolicy.DevelopmentAccountsAllowed);
            if (!ClientAccountPolicy.ShouldSkipSteam(accountConfig, ClientAccountPolicy.DevelopmentAccountsAllowed))
                InitializeSteamworks();

            Account = ClientAccountPolicy.Resolve(accountConfig, ClientAccountPolicy.DevelopmentAccountsAllowed,
                steamId, personaName);
            if (Account.IsLocalTestAccount)
            {
                if (!Account.IsValid)
                {
                    SetFailure("Local test account ID must be between 1 and 9223372036854775807.");
                    return;
                }
                FailureReason = "Local test account selected; Steam P2P is unavailable.";
                Debug.Log($"[Account] Local test account: {Account.Name} ({Account.AccountId}). " +
                    "Steam is not required. Test saves are isolated from Steam saves.");
            }
        }

        public bool TryGetAccount(out ulong accountId, out string playerName)
        {
            accountId = Account.NetworkAccountId;
            playerName = Account.Name;
            return Account.IsValid;
        }

        private void InitializeSteamworks()
        {

#if CRYSTAL_MAGIC_DISABLE_STEAMWORKS
            SetFailure("Steamworks is not available for the current platform.");
#else
            try
            {
                if (!Packsize.Test())
                {
                    SetFailure("Steamworks pack-size validation failed.");
                    return;
                }

                if (!DllCheck.Test())
                {
                    SetFailure("Steamworks native DLL validation failed.");
                    return;
                }

                ESteamAPIInitResult result = SteamAPI.InitEx(out string errorMessage);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    SetFailure(string.IsNullOrWhiteSpace(errorMessage)
                        ? $"SteamAPI initialization failed: {result}."
                        : errorMessage);
                    return;
                }

                initialized = true;
                SteamNetworkingUtils.InitRelayNetworkAccess();
                if (!CacheLocalUser())
                {
                    ShutdownSteamworks();
                    return;
                }

                FailureReason = string.Empty;
                Debug.Log($"[Steam] Steamworks initialized for {personaName} ({steamId}).");
            }
            catch (DllNotFoundException exception)
            {
                SetFailure($"Steam native library could not be loaded: {exception.Message}");
            }
            catch (Exception exception)
            {
                ShutdownSteamworks();
                SetFailure($"Steamworks initialization failed: {exception.Message}");
            }
#endif
        }

        private void Update()
        {
#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
            if (initialized)
            {
                try { SteamAPI.RunCallbacks(); }
                catch (Exception exception)
                {
                    ShutdownSteamworks();
                    SetFailure("Steam callback processing failed: " + exception.Message);
                }
            }
#endif
        }

        public override void Cleanup()
        {
            ShutdownSteamworks();
            Account = default;
            accountConfig = null;
            base.Cleanup();
        }

        protected override void OnDestroy()
        {
            ShutdownSteamworks();
            base.OnDestroy();
        }

#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
        private bool CacheLocalUser()
        {
            CSteamID localSteamId = SteamUser.GetSteamID();
            if (localSteamId.m_SteamID == 0UL)
            {
                SetFailure("Steam returned an invalid local SteamID.");
                return false;
            }

            steamId = localSteamId.m_SteamID;
            personaName = SteamFriends.GetPersonaName();
            if (string.IsNullOrWhiteSpace(personaName))
            {
                personaName = steamId.ToString();
            }

            return true;
        }
#endif

        private void ShutdownSteamworks()
        {
#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
            if (initialized)
            {
                initialized = false;
                SteamAPI.Shutdown();
                Debug.Log("[Steam] Steamworks shut down.");
            }
#endif

            initialized = false;
            steamId = 0UL;
            personaName = string.Empty;
        }

        private void SetFailure(string reason)
        {
            FailureReason = reason;
            ClientAccountIdentity fallback = ClientAccountPolicy.Resolve(accountConfig,
                ClientAccountPolicy.DevelopmentAccountsAllowed, 0, string.Empty);
            if (!Account.IsValid && fallback.IsLocalTestAccount && fallback.IsValid)
                Debug.LogWarning($"[Steam] {reason} Using the development account for this launch.");
            else
                Debug.LogError($"[Steam] {reason}");
        }
    }
}
