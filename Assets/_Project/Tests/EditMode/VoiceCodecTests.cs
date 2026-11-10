using System.Collections.Generic;
using Firehouse.Audio;
using NUnit.Framework;

namespace Firehouse.Tests
{
    public class VoiceCodecTests
    {
        [TestCase(0)]
        [TestCase(1000)]
        [TestCase(-1000)]
        [TestCase(20000)]
        [TestCase(-32000)]
        public void MuLaw_RoundTrip_IsClose(int pcm)
        {
            short decoded = MuLaw.Decode(MuLaw.Encode((short)pcm));
            // μ-law error grows with amplitude: allow ~3% + small floor.
            Assert.LessOrEqual(System.Math.Abs(decoded - pcm), System.Math.Abs(pcm) * 0.03 + 16);
        }

        [Test]
        public void MuLaw_PreservesSign()
        {
            Assert.Greater(MuLaw.DecodeFloat(MuLaw.EncodeFloat(0.5f)), 0.4f);
            Assert.Less(MuLaw.DecodeFloat(MuLaw.EncodeFloat(-0.5f)), -0.4f);
        }

        [Test]
        public void LocalPacket_DecodesToDoubleRate()
        {
            var packet = new byte[1 + 600];
            packet[0] = (byte)VoiceCodec.MuLaw12k;
            for (int i = 1; i < packet.Length; i++) packet[i] = MuLaw.EncodeFloat(0.25f);
            var output = new List<float>();
            Assert.IsTrue(VoiceDecoder.Decode(packet, output));
            Assert.AreEqual(1200, output.Count);
            Assert.AreEqual(0.25f, output[^1], 0.02f);
        }

        [Test]
        public void UnknownCodec_IsRejected()
        {
            Assert.IsFalse(VoiceDecoder.Decode(new byte[] { 99, 1, 2, 3 }, new List<float>()));
        }
    }
}
