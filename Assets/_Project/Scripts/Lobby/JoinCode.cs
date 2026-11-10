using System.Text;

namespace Firehouse.Lobby
{
    /// <summary>Short human-friendly lobby codes, e.g. "KPWTRF". No I or O so they can't be confused with 1 and 0.</summary>
    public static class JoinCode
    {
        public const int Length = 6;
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        public static string Generate(System.Random rng = null)
        {
            rng ??= new System.Random();
            var sb = new StringBuilder(Length);
            for (int i = 0; i < Length; i++)
                sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>Uppercases and drops spaces/dashes. Returns null unless exactly 6 letters from the alphabet remain.</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var sb = new StringBuilder(Length);
            foreach (char c in input.ToUpperInvariant())
            {
                if (Alphabet.IndexOf(c) >= 0) sb.Append(c);
                else if (char.IsWhiteSpace(c) || c == '-') continue;
                else return null;
            }
            return sb.Length == Length ? sb.ToString() : null;
        }
    }
}
