using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Firehouse.Lobby
{
    /// <summary>
    /// Steam lobby create/join/search, join codes, overlay invites and Rich Presence.
    /// The lobby is only the "meeting point": actual game traffic goes through the Facepunch NGO transport to the lobby owner.
    /// </summary>
    public sealed class SteamLobbyService : MonoBehaviour
    {
        // App 480 is shared by every Steamworks developer, so lobbies are tagged and filtered by these keys.
        public const string GameKey = "firehouse_coop";
        public const string ProtocolVersion = "proto1"; // bump when builds become network-incompatible
        public const string CodeKey = "code";
        public const int MaxPlayers = 8;

        public Steamworks.Data.Lobby? Current { get; private set; }
        public string CurrentCode => Current?.GetData(CodeKey);

        /// <summary>Raised when the player accepts an invite or clicks "Join Game" in the Steam friends list.</summary>
        public event Action<SteamId> JoinRequested;

        private void OnEnable()
        {
            SteamFriends.OnGameLobbyJoinRequested += HandleLobbyJoinRequested;
            SteamFriends.OnGameRichPresenceJoinRequested += HandleRichPresenceJoin;
        }

        private void OnDisable()
        {
            SteamFriends.OnGameLobbyJoinRequested -= HandleLobbyJoinRequested;
            SteamFriends.OnGameRichPresenceJoinRequested -= HandleRichPresenceJoin;
        }

        private void HandleLobbyJoinRequested(Steamworks.Data.Lobby lobby, SteamId friend) => JoinRequested?.Invoke(lobby.Id);

        private void HandleRichPresenceJoin(Friend friend, string connect)
        {
            if (TryParseConnect(connect, out var id)) JoinRequested?.Invoke(id);
        }

        /// <summary>Checks the launch command line for "+connect_lobby &lt;id&gt;" (game started from an invite).</summary>
        public static bool TryGetLaunchLobby(out SteamId lobbyId)
        {
            lobbyId = default;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var id))
                {
                    lobbyId = id;
                    return true;
                }
            return false;
        }

        private static bool TryParseConnect(string connect, out SteamId id)
        {
            id = default;
            if (string.IsNullOrEmpty(connect)) return false;
            var parts = connect.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i] == "+connect_lobby" && ulong.TryParse(parts[i + 1], out var v))
                {
                    id = v;
                    return true;
                }
            return false;
        }

        public async Task<bool> CreateAsync()
        {
            Leave();
            var result = await SteamMatchmaking.CreateLobbyAsync(MaxPlayers);
            if (!result.HasValue) return false;

            var lobby = result.Value;
            lobby.SetPublic(); // public so join codes can find it; still filtered by GameKey + code
            lobby.SetJoinable(true);
            lobby.SetData(GameKey, ProtocolVersion);
            lobby.SetData(CodeKey, JoinCode.Generate());
            lobby.SetData("name", $"{SteamClient.Name}'s firehouse");
            Current = lobby;
            UpdateRichPresence();
            Debug.Log($"[Lobby] Created lobby {lobby.Id} code {CurrentCode}");
            return true;
        }

        /// <summary>Joins a Steam lobby and returns the owner's Steam ID (the NGO host), or null on failure.</summary>
        public async Task<SteamId?> JoinAsync(SteamId lobbyId)
        {
            Leave();
            var result = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            if (!result.HasValue) return null;
            var lobby = result.Value;
            if (lobby.GetData(GameKey) != ProtocolVersion)
            {
                Debug.LogWarning($"[Lobby] Lobby {lobbyId} is a different game/version ('{lobby.GetData(GameKey)}').");
                lobby.Leave();
                return null;
            }
            Current = lobby;
            UpdateRichPresence();
            return lobby.Owner.Id;
        }

        public async Task<SteamId?> FindByCodeAsync(string code)
        {
            var lobbies = await SteamMatchmaking.LobbyList
                .WithKeyValue(GameKey, ProtocolVersion)
                .WithKeyValue(CodeKey, code)
                .FilterDistanceWorldwide()
                .WithMaxResults(5)
                .RequestAsync();
            if (lobbies == null || lobbies.Length == 0) return null;
            return lobbies[0].Id;
        }

        public void OpenInviteOverlay()
        {
            if (Current.HasValue) SteamFriends.OpenGameInviteOverlay(Current.Value.Id);
        }

        public void Leave()
        {
            if (Current.HasValue)
            {
                Current.Value.Leave();
                Current = null;
            }
            if (SteamClient.IsValid) SteamFriends.ClearRichPresence();
        }

        private void UpdateRichPresence()
        {
            if (!Current.HasValue || !SteamClient.IsValid) return;
            SteamFriends.SetRichPresence("connect", $"+connect_lobby {Current.Value.Id}");
            SteamFriends.SetRichPresence("status", "At the firehouse");
        }

        private void OnApplicationQuit() => Leave();
    }
}
