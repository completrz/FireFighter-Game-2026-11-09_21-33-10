using System.Collections.Generic;
using Firehouse.Audio;
using Firehouse.Core;
using Firehouse.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Proximity voice for one Smokie. The owner captures their mic and sends packets (unreliable) to everyone;
    /// every other client plays them through this Smokie's head <see cref="VoicePlayback"/>, so volume follows distance.
    /// The talk grille on the mask glows with the speaking level.
    /// Codec: Steam voice in Steam sessions, Unity mic + μ-law in local sessions (Steam isn't available in MPPM clones).
    /// </summary>
    public sealed class SmokieVoice : NetworkBehaviour
    {
        [SerializeField] private VoiceSettings _settings;
        [SerializeField] private VoicePlayback _playback;
        [SerializeField] private Renderer _grille;
        [SerializeField, ColorUsage(false, true)] private Color _glowColor = new(1.6f, 1.1f, 0.35f);

        private static readonly int GlowId = Shader.PropertyToID("_GlowColor");

        private SmokieController _smokie;
        private VoiceCapture _capture;
        private readonly List<byte[]> _packets = new();
        private readonly List<float> _decoded = new();
        private readonly List<float> _loopback = new();
        private MaterialPropertyBlock _mpb;
        private float _shownGlow = -1f;
        private int _packetsSent, _packetsReceived;

        public float SpeakingLevel => IsOwner && _capture != null ? _capture.Level : _playback.Level;

        private void Awake()
        {
            _smokie = GetComponent<SmokieController>();
            _playback.Init(_settings);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            bool steamSession = SessionManager.Instance != null && SessionManager.Instance.Mode == TransportMode.Steam;
            _capture = new VoiceCapture(steamSession ? VoiceCodec.Steam : VoiceCodec.MuLaw12k);
            TuningRegistry.AddSection("Voice", DrawDebug);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            _capture?.Dispose();
            _capture = null;
            TuningRegistry.RemoveSection("Voice");
        }

        public override void OnDestroy()
        {
            _capture?.Dispose();
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner) TickCapture();
            UpdateGrille();
        }

        private void TickCapture()
        {
            if (_capture == null) return;
            bool want = !_settings.PushToTalk || (_smokie.Input != null && _smokie.Input.PushToTalk.IsPressed());
            _loopback.Clear();
            _capture.Tick(want, _settings, _packets, _settings.Loopback ? _loopback : null);
            foreach (var p in _packets)
            {
                VoiceRpc(p);
                _packetsSent++;
            }
            if (_settings.Loopback && _loopback.Count > 0) _playback.Push(_loopback);
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        private void VoiceRpc(byte[] packet)
        {
            _decoded.Clear();
            if (VoiceDecoder.Decode(packet, _decoded))
            {
                _playback.Push(_decoded);
                _packetsReceived++;
            }
        }

        private void UpdateGrille()
        {
            if (_grille == null) return;
            float glow = Mathf.Clamp01(SpeakingLevel * 12f);
            if (Mathf.Abs(glow - _shownGlow) < 0.02f) return;
            _shownGlow = glow;
            _mpb ??= new MaterialPropertyBlock();
            _grille.GetPropertyBlock(_mpb);
            _mpb.SetColor(GlowId, _glowColor * glow);
            _grille.SetPropertyBlock(_mpb);
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || _capture == null) return;
            string text;
            if (!_capture.Available) text = $"Voice off: {_capture.Status}";
            else if (_capture.IsTransmitting) text = "● TALKING";
            else if (_settings.PushToTalk) text = "Hold V to talk";
            else return;
            GUI.color = _capture.IsTransmitting ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(16, Screen.height - 34, 400, 24), text);
            GUI.color = Color.white;
        }

        private void DrawDebug()
        {
            if (_capture == null) return;
            GUILayout.Label($"{_capture.Status}");
            GUILayout.Label($"Level {_capture.Level:0.000}  transmitting {_capture.IsTransmitting}  sent {_packetsSent}");
            int heard = 0;
            foreach (var v in FindObjectsByType<SmokieVoice>(FindObjectsInactive.Exclude))
                if (v != this) heard += v._packetsReceived;
            GUILayout.Label($"Packets received from others: {heard}");
        }
    }
}
