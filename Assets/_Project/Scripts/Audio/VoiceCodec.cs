using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;

namespace Firehouse.Audio
{
    /// <summary>First byte of every voice packet.</summary>
    public enum VoiceCodec : byte
    {
        Steam = 1,     // Steam's compressed voice (needs Steam on both ends)
        MuLaw12k = 2,  // Unity Microphone → 12 kHz μ-law (local/LAN sessions, Multiplayer Play Mode)
    }

    /// <summary>Decodes voice packets into float samples at <see cref="VoiceSettings.PlaybackRate"/>.</summary>
    public static class VoiceDecoder
    {
        private static readonly MemoryStream s_in = new();
        private static readonly MemoryStream s_out = new();

        /// <summary>Appends decoded samples to <paramref name="output"/>. Returns false if the packet can't be decoded here.</summary>
        public static bool Decode(byte[] packet, List<float> output)
        {
            if (packet == null || packet.Length < 2) return false;
            switch ((VoiceCodec)packet[0])
            {
                case VoiceCodec.Steam: return DecodeSteam(packet, output);
                case VoiceCodec.MuLaw12k: DecodeMuLaw(packet, 1, packet.Length - 1, output); return true;
                default: return false;
            }
        }

        private static bool DecodeSteam(byte[] packet, List<float> output)
        {
            if (!SteamClient.IsValid) return false;
            if (SteamUser.SampleRate != VoiceSettings.PlaybackRate) SteamUser.SampleRate = VoiceSettings.PlaybackRate;
            s_in.SetLength(0);
            s_in.Write(packet, 1, packet.Length - 1);
            s_in.Position = 0;
            s_out.SetLength(0);
            int bytes = SteamUser.DecompressVoice(s_in, packet.Length - 1, s_out);
            if (bytes <= 0) return false;
            var buf = s_out.GetBuffer();
            for (int i = 0; i + 1 < bytes; i += 2)
                output.Add((short)(buf[i] | (buf[i + 1] << 8)) / 32768f);
            return true;
        }

        /// <summary>μ-law at 12 kHz → float at 24 kHz (linear interpolation, ×2).</summary>
        public static void DecodeMuLaw(byte[] data, int offset, int count, List<float> output)
        {
            const int factor = VoiceSettings.PlaybackRate / VoiceSettings.LocalCodecRate;
            float prev = 0f;
            for (int i = 0; i < count; i++)
            {
                float s = MuLaw.DecodeFloat(data[offset + i]);
                for (int k = 1; k <= factor; k++)
                    output.Add(prev + (s - prev) * k / factor);
                prev = s;
            }
        }

        /// <summary>Root-mean-square level of the last <paramref name="count"/> samples.</summary>
        public static float Rms(List<float> samples, int start, int count)
        {
            if (count <= 0) return 0f;
            double sum = 0;
            for (int i = start; i < start + count; i++) sum += samples[i] * samples[i];
            return (float)Math.Sqrt(sum / count);
        }
    }
}
