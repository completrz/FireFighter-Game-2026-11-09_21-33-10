using System.Threading.Tasks;
using Firehouse.Core;
using Firehouse.Lobby;
using Firehouse.Networking.Transport;
using Steamworks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Firehouse.Networking
{
    public enum TransportMode { Local, Steam }
    public enum SessionState { Offline, Connecting, Host, Client }

    /// <summary>
    /// Single entry point for starting/joining/leaving a game. Lives on the persistent NetworkRoot prefab
    /// (spawned before the first scene by <see cref="NetworkRootBoot"/>), next to the NetworkManager.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [SerializeField] private NetworkManager _network;
        [SerializeField] private UnityTransport _localTransport;
        [SerializeField] private FacepunchTransport _steamTransport;
        [SerializeField] private SteamLobbyService _lobby;
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private ushort _localPort = 7777;
        [SerializeField, Tooltip("Scene the host loads after starting. Empty = whatever scene was open when the game started.")]
        private string _gameplayScene = "";

        public NetworkManager Network => _network;
        public SteamLobbyService Lobby => _lobby;
        public NetworkObject PlayerPrefab => _playerPrefab;
        public SessionState State { get; private set; } = SessionState.Offline;
        public TransportMode Mode { get; private set; }
        public string Status { get; private set; } = "";
        public bool InSession => State == SessionState.Host || State == SessionState.Client;

        private string _startScene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _startScene = SceneManager.GetActiveScene().name;
            _network.OnClientDisconnectCallback += HandleClientDisconnect;
            _network.OnClientConnectedCallback += HandleClientConnected;
            _lobby.JoinRequested += id => _ = JoinSteamLobby(id);

            TuningRegistry.AddSection("Network", DrawDebug);

            if (SteamService.IsReady && SteamLobbyService.TryGetLaunchLobby(out var launchLobby))
                _ = JoinSteamLobby(launchLobby);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            TuningRegistry.RemoveSection("Network");
        }

        private string GameplayScene => string.IsNullOrEmpty(_gameplayScene) ? _startScene : _gameplayScene;

        // ---------------- Local (Unity Transport, direct IP) ----------------

        public void HostLocal()
        {
            if (!CanStart()) return;
            UseTransport(TransportMode.Local);
            _localTransport.SetConnectionData("127.0.0.1", _localPort, "0.0.0.0");
            StartAsHost();
        }

        public void JoinLocal(string address)
        {
            if (!CanStart()) return;
            UseTransport(TransportMode.Local);
            _localTransport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), _localPort);
            StartAsClient($"Connecting to {address}…");
        }

        // ---------------- Steam ----------------

        public async void HostSteam()
        {
            if (!CanStart()) return;
            if (!SteamService.IsReady) { Fail(SteamService.Instance?.InitError ?? "Steam is not running."); return; }
            State = SessionState.Connecting;
            Status = "Creating Steam lobby…";
            if (!await _lobby.CreateAsync()) { Fail("Couldn't create a Steam lobby."); return; }
            UseTransport(TransportMode.Steam);
            StartAsHost();
        }

        public async void JoinByCode(string rawCode)
        {
            if (!CanStart()) return;
            var code = JoinCode.Normalize(rawCode);
            if (code == null) { Status = $"A join code is {JoinCode.Length} letters."; return; }
            if (!SteamService.IsReady) { Fail(SteamService.Instance?.InitError ?? "Steam is not running."); return; }
            State = SessionState.Connecting;
            Status = $"Looking for lobby {code}…";
            var lobbyId = await _lobby.FindByCodeAsync(code);
            if (!lobbyId.HasValue) { Fail($"No firehouse found with code {code}."); return; }
            State = SessionState.Offline; // JoinSteamLobby re-checks
            await JoinSteamLobby(lobbyId.Value);
        }

        public async Task JoinSteamLobby(SteamId lobbyId)
        {
            if (InSession) Leave();
            if (State == SessionState.Connecting) return;
            if (!SteamService.IsReady) { Fail("Steam is not running."); return; }
            State = SessionState.Connecting;
            Status = "Joining Steam lobby…";
            var owner = await _lobby.JoinAsync(lobbyId);
            if (!owner.HasValue) { Fail("Couldn't join that lobby (full, closed or a different version)."); return; }
            if (owner.Value == SteamClient.SteamId) { Fail("That's your own lobby."); _lobby.Leave(); return; }
            UseTransport(TransportMode.Steam);
            _steamTransport.TargetSteamId = owner.Value;
            State = SessionState.Offline;
            StartAsClient("Connecting to host over Steam…");
        }

        public void OpenInviteOverlay() => _lobby.OpenInviteOverlay();

        // ---------------- Leaving ----------------

        public void Leave()
        {
            if (_network.IsListening) _network.Shutdown();
            _lobby.Leave();
            State = SessionState.Offline;
            Status = "";
            UIFocus.GameplayWantsCursorLock = false;
            // Reload the start scene (after NGO finishes shutting down) so the world resets to its offline state.
            StartCoroutine(ReloadStartSceneAfterShutdown());
        }

        private bool _reloading;

        private System.Collections.IEnumerator ReloadStartSceneAfterShutdown()
        {
            _reloading = true;
            while (_network.ShutdownInProgress || _network.IsListening) yield return null;
            var op = SceneManager.LoadSceneAsync(_startScene);
            while (op != null && !op.isDone) yield return null;
            _reloading = false;
        }

        // ---------------- Internals ----------------

        private bool CanStart()
        {
            if (State == SessionState.Connecting || InSession) return false;
            if (_reloading || _network.ShutdownInProgress || _network.IsListening)
            {
                Status = "Still shutting down the last game, try again in a moment.";
                return false;
            }
            return true;
        }

        private void UseTransport(TransportMode mode)
        {
            Mode = mode;
            _network.NetworkConfig.NetworkTransport = mode == TransportMode.Steam ? _steamTransport : _localTransport;
        }

        private void StartAsHost()
        {
            if (!_network.StartHost())
            {
                Fail("Couldn't start hosting (is the port already in use?).");
                _lobby.Leave();
                return;
            }
            State = SessionState.Host;
            Status = Mode == TransportMode.Steam ? $"Hosting · code {_lobby.CurrentCode}" : $"Hosting locally on port {_localPort}";
            if (SceneManager.GetActiveScene().name != GameplayScene)
                _network.SceneManager.LoadScene(GameplayScene, LoadSceneMode.Single);
        }

        private void StartAsClient(string status)
        {
            State = SessionState.Connecting;
            Status = status;
            if (!_network.StartClient())
                Fail("Couldn't start the client.");
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_network.IsServer || clientId != _network.LocalClientId) return;
            State = SessionState.Client;
            Status = Mode == TransportMode.Steam ? $"Joined · code {_lobby.CurrentCode}" : "Joined local game";
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            // Only care about our own connection ending (or failing to connect).
            if (_network.IsServer && clientId != NetworkManager.ServerClientId) return;
            var reason = string.IsNullOrEmpty(_network.DisconnectReason) ? "Disconnected." : _network.DisconnectReason;
            bool wasConnecting = State == SessionState.Connecting;
            if (_network.IsListening) _network.Shutdown();
            _lobby.Leave();
            State = SessionState.Offline;
            Status = wasConnecting ? $"Couldn't connect. {reason}" : $"Lost connection to host. {reason}";
            UIFocus.GameplayWantsCursorLock = false;
            if (!wasConnecting) StartCoroutine(ReloadStartSceneAfterShutdown());
        }

        private void Fail(string message)
        {
            State = SessionState.Offline;
            Status = message;
            Debug.LogWarning($"[Session] {message}");
        }

        private void DrawDebug()
        {
            GUILayout.Label($"State: {State} ({Mode})");
            GUILayout.Label($"Steam: {(SteamService.IsReady ? SteamClient.Name : SteamService.Instance?.InitError ?? "off")}");
            if (_lobby.Current.HasValue) GUILayout.Label($"Lobby: {_lobby.CurrentCode}  members {_lobby.Current.Value.MemberCount}");
            if (_network.IsListening)
            {
                GUILayout.Label($"Clients: {_network.ConnectedClientsIds.Count}  LocalId: {_network.LocalClientId}");
                if (!_network.IsServer)
                    GUILayout.Label($"RTT: {_network.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId)} ms");
                if (_network.IsServer)
                {
                    GUILayout.Label("Host: load scene for everyone");
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    {
                        var scene = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                        if (GUILayout.Button(scene)) _network.SceneManager.LoadScene(scene, LoadSceneMode.Single);
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}
