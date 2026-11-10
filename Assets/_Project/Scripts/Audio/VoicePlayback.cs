using System.Collections.Generic;
using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Audio
{
    /// <summary>
    /// Plays a remote player's voice through a 3D AudioSource on their head: a streaming clip fed from a
    /// jitter-buffered ring buffer. Linear distance falloff, and a low-pass "muffle" when a wall is in between.
    /// </summary>
    [RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
    public sealed class VoicePlayback : MonoBehaviour
    {
        private const int Rate = VoiceSettings.PlaybackRate;
        private const float OcclusionInterval = 0.15f;

        private VoiceSettings _settings;
        private AudioSource _source;
        private AudioLowPassFilter _lowPass;

        // Ring buffer shared with the audio thread.
        private readonly float[] _ring = new float[Rate * 2];
        private readonly object _lock = new();
        private int _readIndex, _writeIndex, _count;
        private bool _playing;
        private int _jitterSamples = (int)(Rate * 0.12f);

        private float _level;
        private float _nextOcclusionCheck;
        private bool _occluded;
        private static AudioListener s_listener;

        /// <summary>Smoothed 0..1 speaking level (for the talk grille).</summary>
        public float Level => _level;

        public void Init(VoiceSettings settings) => _settings = settings;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _lowPass = GetComponent<AudioLowPassFilter>();
            var clip = AudioClip.Create("Voice", Rate, 1, Rate, true, OnAudioRead);
            _source.clip = clip;
            _source.loop = true;
            _source.spatialBlend = 1f;
            _source.dopplerLevel = 0f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.playOnAwake = false;
            _source.Play();
        }

        /// <summary>Main thread: queue decoded samples.</summary>
        public void Push(List<float> samples)
        {
            if (samples.Count == 0) return;
            float sum = 0f;
            lock (_lock)
            {
                foreach (var s in samples)
                {
                    sum += s * s;
                    if (_count == _ring.Length)
                    {
                        // Overflow: drop the oldest sample (we're too far behind).
                        _readIndex = (_readIndex + 1) % _ring.Length;
                        _count--;
                    }
                    _ring[_writeIndex] = s;
                    _writeIndex = (_writeIndex + 1) % _ring.Length;
                    _count++;
                }
            }
            float rms = Mathf.Sqrt(sum / samples.Count);
            _level = Mathf.Max(_level, rms);
        }

        // Audio thread.
        private void OnAudioRead(float[] data)
        {
            lock (_lock)
            {
                if (!_playing && _count >= _jitterSamples) _playing = true;
                int i = 0;
                if (_playing)
                {
                    int n = Mathf.Min(_count, data.Length);
                    for (; i < n; i++)
                    {
                        data[i] = _ring[_readIndex];
                        _readIndex = (_readIndex + 1) % _ring.Length;
                    }
                    _count -= n;
                    if (_count == 0) _playing = false; // underrun → rebuffer
                }
                for (; i < data.Length; i++) data[i] = 0f;
            }
        }

        private void Update()
        {
            _level = Mathf.MoveTowards(_level, 0f, Time.deltaTime * 0.6f);
            if (_settings == null) return;

            _jitterSamples = (int)(Rate * _settings.JitterBuffer);
            _source.minDistance = _settings.MinDistance;
            _source.maxDistance = _settings.MaxDistance;

            if (Time.time >= _nextOcclusionCheck)
            {
                _nextOcclusionCheck = Time.time + OcclusionInterval;
                if (s_listener == null || !s_listener.isActiveAndEnabled) s_listener = FindAnyObjectByType<AudioListener>();
                _occluded = s_listener != null && Physics.Linecast(s_listener.transform.position, transform.position,
                    1 << Layers.Default, QueryTriggerInteraction.Ignore);
            }
            float targetCutoff = _occluded ? _settings.OccludedCutoff : 22000f;
            _lowPass.cutoffFrequency = Mathf.Lerp(_lowPass.cutoffFrequency, targetCutoff, Time.deltaTime * 8f);
            _source.volume = _settings.Volume * (_occluded ? _settings.OccludedVolume : 1f);
        }
    }
}
