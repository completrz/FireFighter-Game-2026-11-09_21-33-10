using Firehouse.Lobby;
using NUnit.Framework;

namespace Firehouse.Tests
{
    public class JoinCodeTests
    {
        [Test]
        public void Generate_HasCorrectLengthAndAlphabet()
        {
            var rng = new System.Random(1234);
            for (int i = 0; i < 200; i++)
            {
                var code = JoinCode.Generate(rng);
                Assert.AreEqual(JoinCode.Length, code.Length);
                foreach (char c in code) StringAssert.Contains(c.ToString(), JoinCode.Alphabet);
            }
        }

        [TestCase("kpwtrf", "KPWTRF")]
        [TestCase(" KPW-TRF ", "KPWTRF")]
        [TestCase("kpw trf", "KPWTRF")]
        public void Normalize_AcceptsFriendlyInput(string input, string expected) =>
            Assert.AreEqual(expected, JoinCode.Normalize(input));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("KPWTR")]      // too short
        [TestCase("KPWTRFA")]    // too long
        [TestCase("KPWTR1")]     // digit
        [TestCase("KPWTRO")]     // O is not in the alphabet
        public void Normalize_RejectsInvalid(string input) => Assert.IsNull(JoinCode.Normalize(input));
    }
}
