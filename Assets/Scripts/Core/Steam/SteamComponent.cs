#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define CRYSTAL_MAGIC_DISABLE_STEAMWORKS
#endif

using System;
using UnityEngine;
#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
using Steamworks;
#endif

namespace CrystalMagic.Core
{
    /// <summary>
    /// 管理客户端 Steamworks 生命周期，并提供当前 Steam 用户身份。
    /// </summary>
    public class SteamComponent : GameComponent<SteamComponent>
    {
        private bool initialized;
        private ulong steamId;
        private string personaName = string.Empty;

        public override int Priority => 2;
        public bool IsInitialized => initialized;
        public ulong SteamId => steamId;
        public string PersonaName => personaName;
        public string FailureReason { get; private set; } = "Steam has not been initialized.";

        public override void Initialize()
        {
            base.Initialize();

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
                SetFailure($"Steamworks initialization failed: {exception.Message}");
            }
#endif
        }

        /// <summary>
        /// 读取当前 Steam 客户端中已登录用户的 SteamID 与显示昵称。
        /// </summary>
        public bool TryGetLocalUser(out ulong accountId, out string personaName)
        {
            accountId = steamId;
            personaName = this.personaName;

#if CRYSTAL_MAGIC_DISABLE_STEAMWORKS
            return false;
#else
            return initialized && accountId != 0UL && !string.IsNullOrWhiteSpace(personaName);
#endif
        }

        private void Update()
        {
#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
            if (initialized)
            {
                SteamAPI.RunCallbacks();
            }
#endif
        }

        public override void Cleanup()
        {
            ShutdownSteamworks();
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
                SteamAPI.Shutdown();
                initialized = false;
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
            Debug.LogError($"[Steam] {reason}");
        }
    }
}
