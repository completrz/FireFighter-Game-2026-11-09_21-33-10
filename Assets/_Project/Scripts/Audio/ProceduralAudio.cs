using UnityEngine;

namespace Firehouse.Audio
{
    /// <summary>
    /// Placeholder sounds generated in code (no audio assets yet): truck horn, siren wail, engine loop.
    /// Swap for real recordings later by assigning clips on <see cref="Truck.TruckEffects"/>.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int Rate = 44100;
        private static AudioClip s_horn, s_siren, s_engine, s_crackle;

        /// <summary>Fire crackle: a low roar plus random pops, loopable.</summary>
        public static AudioClip FireCrackle => s_crackle ??= BuildCrackle();

        private static AudioClip BuildCrackle()
        {
            const float seconds = 3f;
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(11);
            float roar = 0f, pop = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                roar = Mathf.Lerp(roar, white, 0.03f);               // low-passed rumble
                if (rng.NextDouble() < 0.0006) pop = 0.9f;          // occasional crackle
                pop *= 0.992f;
                data[i] = roar * 0.5f + white * pop * 0.6f;
            }
            // Fade the ends so the loop seam doesn't click.
            for (int i = 0; i < 400; i++) { float f = i / 400f; data[i] *= f; data[n - 1 - i] *= f; }
            var clip = AudioClip.Create("FireCrackle", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Two-tone air-horn chord, loopable.</summary>
        public static AudioClip Horn => s_horn ??= Build("Horn", 1f, t =>
            0.32f * (Square(t * 233f) + Square(t * 311f) * 0.8f) * 0.5f);

        /// <summary>Classic wail: pitch sweeps 650 → 1350 Hz and back over 4 s, loopable.</summary>
        public static AudioClip Siren => s_siren ??= BuildPhase("Siren", 4f, t =>
        {
            float sweep = 0.5f - 0.5f * Mathf.Cos(t / 4f * Mathf.PI * 2f);
            return Mathf.Lerp(650f, 1350f, sweep);
        }, 0.28f);

        /// <summary>Low diesel rumble at ~45 Hz; pitch it up with AudioSource.pitch for RPM.</summary>
        public static AudioClip Engine => s_engine ??= BuildEngine();

        private static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;

        private static AudioClip Build(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = f(i / (float)Rate);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Frequency-modulated tone with continuous phase (no clicks), softened square-ish shape.</summary>
        private static AudioClip BuildPhase(string name, float seconds, System.Func<float, float> freq, float volume)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                phase += freq(i / (float)Rate) / Rate;
                float s = Mathf.Sin((float)(phase * Mathf.PI * 2.0));
                data[i] = volume * Mathf.Clamp(s * 2.2f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildEngine()
        {
            const float seconds = 1f; // 45 Hz fits exactly → seamless loop
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(7);
            float noise = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float saw = 2f * Mathf.Repeat(t * 45f, 1f) - 1f;
                float firing = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 90f * Mathf.PI * 2f)), 6f); // cylinder thumps
                noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2f - 1f, 0.05f);
                data[i] = 0.25f * saw * 0.6f + 0.35f * firing + 0.15f * noise;
            }
            var clip = AudioClip.Create("Engine", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
