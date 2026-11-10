using Firehouse.Core;
using Firehouse.Lobby;
using Firehouse.Networking;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Firehouse.UI
{
    /// <summary>
    /// Prototype menu (IMGUI): host/join over Steam or locally, and an Esc menu while in a game.
    /// Lives on the NetworkRoot prefab. Will be replaced with a diegetic firehouse menu later.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private SessionManager _session;

        private string _code = "";
        private string _address = "127.0.0.1";
        private bool _pauseOpen;
        private GUIStyle _title, _button, _label, _field, _box;
        private float _scale = 1f;

        private bool ShowMain => !_session.InSession;

        private void Update()
        {
            var kb = Keyboard.current;
            if (_session.InSession && kb != null && kb.escapeKey.wasPressedThisFrame)
                _pauseOpen = !_pauseOpen;
            if (!_session.InSession) _pauseOpen = false;
            UIFocus.SetOpen(this, ShowMain || _pauseOpen);
        }

        private void EnsureStyles()
        {
            // Fit the 420x560 main panel on any window size.
            _scale = Mathf.Clamp(Mathf.Min(Screen.height / 600f, Screen.width / 460f), 0.6f, 2f);
            if (_title != null && Mathf.Approximately(_title.fontSize, Mathf.RoundToInt(34 * _scale))) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(34 * _scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(1f, 0.85f, 0.4f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * _scale), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(17 * _scale), fixedHeight = 38 * _scale };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(18 * _scale), fixedHeight = 34 * _scale, alignment = TextAnchor.MiddleCenter };
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 18) };
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (ShowMain) DrawMain();
            else
            {
                DrawSessionBadge();
                if (_pauseOpen) DrawPause();
            }
        }

        private Rect Centered(float w, float h) =>
            new((Screen.width - w * _scale) * 0.5f, (Screen.height - h * _scale) * 0.5f, w * _scale, h * _scale);

        private void DrawMain()
        {
            GUILayout.BeginArea(Centered(420, 560), _box);
            GUILayout.Label("FIREHOUSE CO-OP", _title);
            GUILayout.Label("prototype", _label);
            GUILayout.Space(12 * _scale);

            bool busy = _session.State == SessionState.Connecting;
            GUI.enabled = !busy;

            GUILayout.Label(SteamService.IsReady ? $"Steam: {SteamClient.Name}" : $"Steam: {SteamService.Instance?.InitError ?? "off"}", _label);
            GUI.enabled = !busy && SteamService.IsReady;
            if (GUILayout.Button("Host (Steam lobby, 8 players)", _button)) _session.HostSteam();
            GUILayout.BeginHorizontal();
            _code = GUILayout.TextField(_code.ToUpperInvariant(), JoinCode.Length + 2, _field, GUILayout.Width(150 * _scale));
            if (GUILayout.Button("Join with code", _button)) _session.JoinByCode(_code);
            GUILayout.EndHorizontal();

            GUILayout.Space(18 * _scale);
            GUI.enabled = !busy;
            GUILayout.Label("Local test (same PC / LAN)", _label);
            if (GUILayout.Button("Host local", _button)) _session.HostLocal();
            GUILayout.BeginHorizontal();
            _address = GUILayout.TextField(_address, 40, _field, GUILayout.Width(150 * _scale));
            if (GUILayout.Button("Join IP", _button)) _session.JoinLocal(_address);
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            GUILayout.Space(14 * _scale);
            if (!string.IsNullOrEmpty(_session.Status)) GUILayout.Label(_session.Status, _label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Quit", _button)) Quit();
            GUILayout.EndArea();
        }

        private void DrawSessionBadge()
        {
            var text = _session.Status + "   ·   Esc: menu   ·   F1: debug";
            var size = _label.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(Screen.width - size.x - 14, 8, size.x, size.y), text, _label);
        }

        private void DrawPause()
        {
            GUILayout.BeginArea(Centered(380, 330), _box);
            GUILayout.Label("PAUSED", _title);
            if (_session.Mode == TransportMode.Steam && _session.Lobby.Current.HasValue)
            {
                GUILayout.Label($"Join code: {_session.Lobby.CurrentCode}", _label);
                if (GUILayout.Button("Invite friends (Steam overlay)", _button)) _session.OpenInviteOverlay();
                if (GUILayout.Button("Copy join code", _button)) GUIUtility.systemCopyBuffer = _session.Lobby.CurrentCode;
            }
            if (GUILayout.Button("Resume", _button)) _pauseOpen = false;
            if (GUILayout.Button(_session.State == SessionState.Host ? "End game (everyone leaves)" : "Leave", _button))
            {
                _pauseOpen = false;
                _session.Leave();
            }
            GUILayout.EndArea();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
