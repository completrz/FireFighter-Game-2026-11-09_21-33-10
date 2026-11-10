using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;

namespace Firehouse.Audio
{
    /// <summary>
    /// Captures the local player's voice and turns it into network packets.
    /// Steam codec: Steam records, compresses and voice-activates for us.
    /// Local codec: Unity Microphone → resample to 12 kHz → simple voice activation → μ-law packets of 50 ms.
    /// </summary>
    public sealed class VoiceCapture : IDisposable
    {
        private const int LocalPacketSamples = VoiceSettings.LocalCodecRate / 20; // 50 ms

        public VoiceCodec Codec { get; }
        public bool Available { get; private set; }
        public string Status { get; private set; }
        /// <summary>Smoothed 0..1 level of what we're sending (for the grille glow and the HUD).</summary>
        public float Level { get; private set; }
        public bool IsTransmitting { get; private set; }

        // Steam
        private readonly MemoryStream _steamBuffer = new();

        // Local microphone
        private AudioClip _mic;
        private string _device;
        private int _micRate;
        private int _lastPos;
        private float[] _readBuf = new float[4096];
        private float _resamplePhase;
        private float _prevSample;
        private readonly List<float> _pending = new();
        private float _holdUntil;

        // Scratch
        private readonly List<float> _decoded = new();

        public VoiceCapture(VoiceCodec codec)
        {
            Codec = codec;
            if (codec == VoiceCodec.Steam)
            {
                Available = SteamClient.IsValid;
                Status = Available ? "Steam voice" : "Steam not running";
            }
            else
            {
                StartMicrophone();
            }
        }

        private void StartMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Available = false;
                Status = "No microphone found";
                return;
            }
            _device = Microphone.devices[0];
            Microphone.GetDeviceCaps(_device, out int min, out int max);
            _micRate = max == 0 ? 48000 : Mathf.Clamp(48000, min, max);
            _mic = Microphone.Start(_device, true, 1, _micRate);
            _lastPos = 0;
            Available = _mic != null;
            Status = Available ? $"Local mic: {_device} ({_micRate} Hz)" : "Microphone failed to start";
        }

        /// <param name="wantTransmit">Push-to-talk held, or open mic.</param>
        /// <param name="packets">Receives packets to send.</param>
        /// <param name="loopback">If not null, receives decoded audio of what we sent (to hear ourselves).</param>
        public void Tick(bool wantTransmit, VoiceSettings settings, List<byte[]> packets, List<float> loopback)
        {
            packets.Clear();
            if (!Available) { Level = 0f; IsTransmitting = false; return; }
            if (Codec == VoiceCodec.Steam) TickSteam(wantTransmit, packets, loopback);
            else TickMicrophone(wantTransmit, settings, packets, loopback);
        }

        private void TickSteam(bool wantTransmit, List<byte[]> packets, List<float> loopback)
        {
            if (!SteamClient.IsValid) return;
            if (SteamUser.VoiceRecord != wantTransmit) SteamUser.VoiceRecord = wantTransmit;
            float level = 0f;
            bool sent = false;
            if (wantTransmit && SteamUser.HasVoiceData)
            {
                _steamBuffer.SetLength(0);
                int n = SteamUser.ReadVoiceData(_steamBuffer);
                if (n > 0)
                {
                    var packet = new byte[n + 1];
                    packet[0] = (byte)VoiceCodec.Steam;
                    Buffer.BlockCopy(_steamBuffer.GetBuffer(), 0, packet, 1, n);
                    packets.Add(packet);
                    sent = true;

                    // Decode our own packet to measure level (and for loopback).
                    _decoded.Clear();
                    if (VoiceDecoder.Decode(packet, _decoded))
                    {
                        level = VoiceDecoder.Rms(_decoded, 0, _decoded.Count);
                        loopback?.AddRange(_decoded);
                    }
                }
            }
            IsTransmitting = sent || (IsTransmitting && Level > 0.01f);
            Level = Mathf.Max(level, Level * 0.85f);
        }

        private void TickMicrophone(bool wantTransmit, VoiceSettings s, List<byte[]> packets, List<float> loopback)
        {
            int pos = Microphone.GetPosition(_device);
            int len = _mic.samples;
            int count = (pos - _lastPos + len) % len;
            if (count == 0) { Level *= 0.85f; return; }

            if (_readBuf.Length < count) _readBuf = new float[count];
            // Read with wrap-around.
            int first = Mathf.Min(count, len - _lastPos);
            var tmp = new float[first];
            _mic.GetData(tmp, _lastPos);
            Array.Copy(tmp, 0, _readBuf, 0, first);
            if (count > first)
            {
                var tmp2 = new float[count - first];
                _mic.GetData(tmp2, 0);
                Array.Copy(tmp2, 0, _readBuf, first, count - first);
            }
            _lastPos = pos;

            // Resample to 12 kHz with gain, and measure level.
            float step = (float)_micRate / VoiceSettings.LocalCodecRate;
            int startPending = _pending.Count;
            while (_resamplePhase < count)
            {
                int i = (int)_resamplePhase;
                float frac = _resamplePhase - i;
                float a = i == 0 ? _prevSample : _readBuf[i - 1];
                float b = _readBuf[i];
                _pending.Add(Mathf.Clamp((a + (b - a) * frac) * s.MicGain, -1f, 1f));
                _resamplePhase += step;
            }
            _resamplePhase -= count;
            _prevSample = _readBuf[count - 1];

            float rms = VoiceDecoder.Rms(_pending, startPending, _pending.Count - startPending);
            if (rms > s.VoiceActivationLevel) _holdUntil = Time.unscaledTime + s.VoiceActivationHold;
            IsTransmitting = wantTransmit && Time.unscaledTime < _holdUntil;
            Level = Mathf.Max(IsTransmitting ? rms : 0f, Level * 0.85f);

            if (!IsTransmitting)
            {
                _pending.Clear();
                return;
            }

            while (_pending.Count >= LocalPacketSamples)
            {
                var packet = new byte[LocalPacketSamples + 1];
                packet[0] = (byte)VoiceCodec.MuLaw12k;
                for (int i = 0; i < LocalPacketSamples; i++)
                    packet[i + 1] = MuLaw.EncodeFloat(_pending[i]);
                _pending.RemoveRange(0, LocalPacketSamples);
                packets.Add(packet);
                if (loopback != null) VoiceDecoder.DecodeMuLaw(packet, 1, LocalPacketSamples, loopback);
            }
        }

        public void Dispose()
        {
            if (Codec == VoiceCodec.Steam)
            {
                if (SteamClient.IsValid) SteamUser.VoiceRecord = false;
            }
            else if (_device != null && Microphone.IsRecording(_device))
            {
                Microphone.End(_device);
            }
        }
    }
}
