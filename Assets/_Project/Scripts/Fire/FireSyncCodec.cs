using System.Collections.Generic;
using System.IO;

namespace Firehouse.Fire
{
    /// <summary>
    /// Compact encodings for fire grid sync.
    /// Delta: varint count, then per changed cell a varint gap from the previous index (sorted) and the state byte —
    /// typically 2 bytes per cell. Snapshot: run-length (count, value) pairs over the whole state array.
    /// </summary>
    public static class FireSyncCodec
    {
        public static byte[] EncodeDelta(List<int> sortedIndices, byte[] states)
        {
            using var ms = new MemoryStream(sortedIndices.Count * 2 + 4);
            WriteVarint(ms, (uint)sortedIndices.Count);
            int prev = -1;
            foreach (int i in sortedIndices)
            {
                WriteVarint(ms, (uint)(i - prev - 1));
                ms.WriteByte(states[i]);
                prev = i;
            }
            return ms.ToArray();
        }

        /// <summary>Applies a delta to <paramref name="states"/>; returns false if malformed.</summary>
        public static bool ApplyDelta(byte[] data, byte[] states, List<int> changed = null)
        {
            int pos = 0;
            if (!ReadVarint(data, ref pos, out uint count)) return false;
            int prev = -1;
            for (uint k = 0; k < count; k++)
            {
                if (!ReadVarint(data, ref pos, out uint gap) || pos >= data.Length) return false;
                int i = prev + 1 + (int)gap;
                if (i < 0 || i >= states.Length) return false;
                states[i] = data[pos++];
                changed?.Add(i);
                prev = i;
            }
            return true;
        }

        public static byte[] EncodeSnapshot(byte[] states)
        {
            using var ms = new MemoryStream();
            int i = 0;
            while (i < states.Length)
            {
                byte v = states[i];
                int run = 1;
                while (i + run < states.Length && states[i + run] == v && run < 255) run++;
                ms.WriteByte((byte)run);
                ms.WriteByte(v);
                i += run;
            }
            return ms.ToArray();
        }

        public static bool ApplySnapshot(byte[] data, byte[] states)
        {
            int i = 0;
            for (int p = 0; p + 1 < data.Length; p += 2)
            {
                int run = data[p];
                byte v = data[p + 1];
                for (int k = 0; k < run; k++)
                {
                    if (i >= states.Length) return false;
                    states[i++] = v;
                }
            }
            return i == states.Length;
        }

        private static void WriteVarint(Stream s, uint v)
        {
            while (v >= 0x80) { s.WriteByte((byte)(v | 0x80)); v >>= 7; }
            s.WriteByte((byte)v);
        }

        private static bool ReadVarint(byte[] d, ref int pos, out uint v)
        {
            v = 0;
            int shift = 0;
            while (pos < d.Length && shift < 35)
            {
                byte b = d[pos++];
                v |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
            }
            return false;
        }
    }
}
