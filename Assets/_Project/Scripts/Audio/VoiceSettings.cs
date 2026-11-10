using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Audio
{
    /// <summary>Proximity voice tuning. Asset: Data/VoiceSettings.asset.</summary>
    [CreateAssetMenu(menuName = "Firehouse/Voice Settings", fileName = "VoiceSettings")]
    public sealed class VoiceSettings : TunableData
    {
        [Header("Transmit")]
        [Tunable] public bool PushToTalk = false;              // false = open mic (Steam / VAD decide when you talk)
        [Tunable(0.1f, 4f)] public float MicGain = 1.2f;
        [Tunable(0.001f, 0.1f)] public float VoiceActivationLevel = 0.015f; // local codec only (Steam has its own VAD)
        [Tunable(0f, 1f)] public float VoiceActivationHold = 0.35f;

        [Header("Proximity")]
        [Tunable(0.5f, 10f)] public float MinDistance = 2.5f;   // full volume inside this
        [Tunable(5f, 80f)] public float MaxDistance = 30f;      // silent beyond this
        [Tunable(0f, 2f)] public float Volume = 1f;
        [Tunable(300f, 8000f)] public float OccludedCutoff = 1400f; // low-pass when a wall is between you
        [Tunable(0f, 1f)] public float OccludedVolume = 0.55f;

        [Header("Playback")]
        [Tunable(0.04f, 0.4f)] public float JitterBuffer = 0.12f; // seconds buffered before playing

        [Header("Debug")]
        [Tunable] public bool Loopback = false;                 // hear your own voice (single-PC test)

        /// <summary>All voice is resampled to this rate for playback.</summary>
        public const int PlaybackRate = 24000;
        /// <summary>Sample rate of the local μ-law codec.</summary>
        public const int LocalCodecRate = 12000;
    }
}
