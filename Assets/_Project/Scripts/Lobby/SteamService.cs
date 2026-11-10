using System;
using Steamworks;
using UnityEngine;

namespace Firehouse.Lobby
{
    /// <summary>
    /// Owns the Steam client's lifetime: Init on startup, RunCallbacks every frame, Shutdown on quit.
    /// Steam stays up between network sessions because lobbies, invites and voice all need it.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class SteamService : MonoBehaviour
    {
        /// <summary>Spacewar test app until Firehouse has its own App ID. Keep in sync with steam_appid.txt.</summary>
        public const uint AppId = 480;

        public static SteamService Instance { get; private set; }
        public static bool IsReady => SteamClient.IsValid;

        public string InitError { get; private set; }

        private void Awake()
        {
            Instance = this;
            TryInit();
        }

        public bool TryInit()
        {
            if (SteamClient.IsValid) return true;
            if (IsMultiplayerPlayModeClone())
            {
                InitError = "Steam is off in Multiplayer Play Mode clones (one Steam login per PC). Use Local.";
                return false;
            }
            try
            {
                SteamClient.Init(AppId, asyncCallbacks: false);
                InitError = null;
            }
            catch (Exception e)
            {
                InitError = $"Steam not available: {e.Message}";
                Debug.LogWarning($"[Steam] {InitError}");
            }
            return SteamClient.IsValid;
        }

        private void Update()
        {
            if (SteamClient.IsValid) SteamClient.RunCallbacks();
        }

        private void OnApplicationQuit() => ShutdownSteam();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ShutdownSteam();
        }

        private static void ShutdownSteam()
        {
            if (SteamClient.IsValid) SteamClient.Shutdown();
        }

        /// <summary>Multiplayer Play Mode virtual players run from Library/VP/…; they share the main editor's Steam login.</summary>
        public static bool IsMultiplayerPlayModeClone()
        {
#if UNITY_EDITOR
            return Application.dataPath.Replace('\\', '/').Contains("/Library/VP/");
#else
            return false;
#endif
        }
    }
}
