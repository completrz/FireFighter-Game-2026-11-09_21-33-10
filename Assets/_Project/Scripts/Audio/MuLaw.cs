namespace Firehouse.Audio
{
    /// <summary>
    /// G.711 μ-law: 16-bit PCM ↔ 8-bit, used by the local (non-Steam) voice codec.
    /// Halves bandwidth with telephone-quality audio, which is plenty for walkie-talkie-ish proximity chat.
    /// </summary>
    public static class MuLaw
    {
        private const int Bias = 0x84;
        private const int Clip = 32635;

        public static byte Encode(short pcm)
        {
            int sample = pcm;
            int sign = (sample >> 8) & 0x80;
            if (sign != 0) sample = -sample;
            if (sample > Clip) sample = Clip;
            sample += Bias;
            int exponent = 7;
            for (int mask = 0x4000; (sample & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
            int mantissa = (sample >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        public static short Decode(byte mu)
        {
            mu = (byte)~mu;
            int sign = mu & 0x80;
            int exponent = (mu >> 4) & 0x07;
            int mantissa = mu & 0x0F;
            int sample = ((mantissa << 3) + Bias) << exponent;
            sample -= Bias;
            return (short)(sign != 0 ? -sample : sample);
        }

        public static byte EncodeFloat(float f) =>
            Encode((short)(f < -1f ? -32767 : f > 1f ? 32767 : f * 32767f));

        public static float DecodeFloat(byte mu) => Decode(mu) / 32768f;
    }
}
