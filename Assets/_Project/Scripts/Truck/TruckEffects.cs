using Firehouse.Audio;
using UnityEngine;

namespace Firehouse.Truck
{
    /// <summary>
    /// Local-only truck presentation driven by synced state: wheel positions/spin/steer, engine pitch,
    /// horn, siren, and the flashing lightbar + headlights. Runs on every client.
    /// </summary>
    public sealed class TruckEffects : MonoBehaviour
    {
        [SerializeField] private TruckController _truck;
        [SerializeField] private AudioSource _engine;
        [SerializeField] private AudioSource _horn;
        [SerializeField] private AudioSource _siren;
        [SerializeField] private Renderer _lightbarLeft;
        [SerializeField] private Renderer _lightbarRight;
        [SerializeField] private Light _flashLeft;
        [SerializeField] private Light _flashRight;
        [SerializeField] private Light[] _headlights;
        [SerializeField, ColorUsage(false, true)] private Color _red = new(4f, 0.3f, 0.2f);
        [SerializeField, ColorUsage(false, true)] private Color _blue = new(0.3f, 0.6f, 4f);

        private static readonly int GlowId = Shader.PropertyToID("_GlowColor");
        private MaterialPropertyBlock _mpb;
        private float[] _spin;

        private void Start()
        {
            _mpb = new MaterialPropertyBlock();
            _spin = new float[_truck.Wheels.Count];
            Setup(_engine, ProceduralAudio.Engine, true);
            Setup(_horn, ProceduralAudio.Horn, false);
            Setup(_siren, ProceduralAudio.Siren, false);
            _engine.Play();
        }

        private static void Setup(AudioSource s, AudioClip clip, bool play)
        {
            if (s == null) return;
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0.3f;
            s.rolloffMode = AudioRolloffMode.Linear;
            if (play) s.Play();
        }

        private void Update()
        {
            if (_truck == null || !_truck.IsSpawned) return;
            UpdateWheels();
            UpdateAudio();
            UpdateLights();
        }

        private void UpdateWheels()
        {
            var stats = _truck.Stats;
            float maxDist = stats.RestLength + stats.WheelRadius;
            float fwdSpeed = _truck.ForwardSpeed;
            float steer = _truck.IsOwner ? _truck.SteerAngle : _truck.Steer * stats.MaxSteerAngle * 0.8f;
            for (int i = 0; i < _truck.Wheels.Count; i++)
            {
                var w = _truck.Wheels[i];
                if (w.Visual == null) continue;
                // Visual raycast on every client (cheap) so wheels sit on bumps even when the truck is remote.
                float travel = stats.RestLength;
                if (Physics.Raycast(w.Mount.position, -_truck.transform.up, out var hit, maxDist, 1 << 0, QueryTriggerInteraction.Ignore))
                    travel = hit.distance - stats.WheelRadius;
                w.Visual.position = w.Mount.position - _truck.transform.up * travel;
                _spin[i] += fwdSpeed / stats.WheelRadius * Mathf.Rad2Deg * Time.deltaTime;
                float yaw = w.Front ? steer : 0f;
                w.Visual.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(_spin[i], 0f, 0f);
            }
        }

        private void UpdateAudio()
        {
            float speed01 = Mathf.Clamp01(Mathf.Abs(_truck.ForwardSpeed) / _truck.Stats.MaxSpeed);
            float load = Mathf.Abs(_truck.Throttle);
            if (_engine != null)
            {
                _engine.pitch = Mathf.Lerp(_engine.pitch, 0.8f + speed01 * 1.3f + load * 0.25f, Time.deltaTime * 4f);
                _engine.volume = 0.35f + load * 0.35f;
            }
            Toggle(_horn, _truck.Horn);
            Toggle(_siren, _truck.Siren);
        }

        private static void Toggle(AudioSource s, bool on)
        {
            if (s == null) return;
            if (on && !s.isPlaying) s.Play();
            else if (!on && s.isPlaying) s.Stop();
        }

        private void UpdateLights()
        {
            bool on = _truck.Lights;
            // Alternate left/right at ~3 Hz, with a double-flash pattern.
            float t = Time.time * 3f;
            bool phaseA = Mathf.Repeat(t, 1f) < 0.5f;
            bool flicker = Mathf.Repeat(t * 4f, 1f) < 0.6f;
            bool left = on && phaseA && flicker;
            bool right = on && !phaseA && flicker;
            SetGlow(_lightbarLeft, left ? _red : Color.black);
            SetGlow(_lightbarRight, right ? _blue : Color.black);
            if (_flashLeft != null) _flashLeft.enabled = left;
            if (_flashRight != null) _flashRight.enabled = right;
            if (_headlights != null)
                foreach (var h in _headlights) if (h != null) h.enabled = on;
        }

        private void SetGlow(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(GlowId, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}
