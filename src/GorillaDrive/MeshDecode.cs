using System;
using System.Collections.Generic;

namespace GorillaDrive
{
    /// <summary>
    /// Pure decoding of Unity's serialized vertex buffer (Unity 2019+ layout). No UnityEngine types,
    /// so it can be unit-tested outside the game.
    /// </summary>
    public static class MeshDecode
    {
        // Unity 2019+ VertexAttributeFormat
        public static int FormatSize(int format)
        {
            switch (format)
            {
                case 0: return 4;            // Float32
                case 1: return 2;            // Float16
                case 2: case 3: return 1;    // UNorm8, SNorm8
                case 4: case 5: return 2;    // UNorm16, SNorm16
                case 6: case 7: return 1;    // UInt8, SInt8
                case 8: case 9: return 2;    // UInt16, SInt16
                case 10: case 11: return 4;  // UInt32, SInt32
                default: throw new NotSupportedException("vertex format " + format);
            }
        }

        public struct Channel { public int Stream, Offset, Format, Dimension; }

        /// <summary>Byte offset of each stream and its stride, laid out the way Unity writes them.</summary>
        public static void StreamLayout(IList<Channel> channels, int vertexCount, out int[] offsets, out int[] strides)
        {
            int streamCount = 0;
            foreach (var c in channels) if (c.Dimension > 0) streamCount = Math.Max(streamCount, c.Stream + 1);
            strides = new int[streamCount];
            offsets = new int[streamCount];
            foreach (var c in channels)
                if (c.Dimension > 0)
                    strides[c.Stream] = Math.Max(strides[c.Stream], c.Offset + FormatSize(c.Format) * (c.Dimension & 0xF));
            int pos = 0;
            for (int s = 0; s < streamCount; s++)
            {
                offsets[s] = pos;
                pos += strides[s] * vertexCount;
                pos = (pos + 15) & ~15; // streams are 16-byte aligned
            }
        }

        public static float ReadComponent(byte[] d, int at, int format)
        {
            switch (format)
            {
                case 0: return BitConverter.ToSingle(d, at);
                case 1: return HalfToFloat(BitConverter.ToUInt16(d, at));
                case 2: return d[at] / 255f;
                case 3: return Math.Max((sbyte)d[at] / 127f, -1f);
                case 4: return BitConverter.ToUInt16(d, at) / 65535f;
                case 5: return Math.Max(BitConverter.ToInt16(d, at) / 32767f, -1f);
                case 6: return d[at];
                case 7: return (sbyte)d[at];
                case 8: return BitConverter.ToUInt16(d, at);
                case 9: return BitConverter.ToInt16(d, at);
                case 10: return BitConverter.ToUInt32(d, at);
                case 11: return BitConverter.ToInt32(d, at);
                default: throw new NotSupportedException("vertex format " + format);
            }
        }

        /// <summary>Reads one channel for all vertices as float[vertexCount * dimension]; null when absent.</summary>
        public static float[] ReadChannel(byte[] data, IList<Channel> channels, int channelIndex, int vertexCount)
        {
            if (channelIndex >= channels.Count) return null;
            var c = channels[channelIndex];
            int dim = c.Dimension & 0xF;
            if (dim == 0) return null;
            StreamLayout(channels, vertexCount, out var offsets, out var strides);
            int size = FormatSize(c.Format);
            var result = new float[vertexCount * dim];
            for (int v = 0; v < vertexCount; v++)
            {
                int baseAt = offsets[c.Stream] + v * strides[c.Stream] + c.Offset;
                for (int k = 0; k < dim; k++)
                    result[v * dim + k] = ReadComponent(data, baseAt + k * size, c.Format);
            }
            return result;
        }

        public static int[] ReadIndices(byte[] indexBuffer, bool is32Bit, int firstByte, int indexCount)
        {
            var r = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
                r[i] = is32Bit ? (int)BitConverter.ToUInt32(indexBuffer, firstByte + i * 4)
                               : BitConverter.ToUInt16(indexBuffer, firstByte + i * 2);
            return r;
        }

        public static float HalfToFloat(ushort h)
        {
            int sign = (h >> 15) & 1, exp = (h >> 10) & 0x1F, mant = h & 0x3FF;
            float v;
            if (exp == 0) v = (float)(mant * Math.Pow(2, -24));
            else if (exp == 31) v = mant == 0 ? float.PositiveInfinity : float.NaN;
            else v = (float)((1 + mant / 1024.0) * Math.Pow(2, exp - 15));
            return sign == 1 ? -v : v;
        }

        /// <summary>
        /// Splits triangles into parts by the dominant bone of their first vertex.
        /// partOfBone maps a bone index to a part id (0 = body). Returns triangle index lists per part.
        /// </summary>
        public static List<int>[] SplitByBone(int[] triangles, int[] dominantBone, int[] partOfBone, int partCount)
        {
            var parts = new List<int>[partCount];
            for (int p = 0; p < partCount; p++) parts[p] = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int bone = dominantBone[triangles[t]];
                int part = bone >= 0 && bone < partOfBone.Length ? partOfBone[bone] : 0;
                parts[part].Add(triangles[t]); parts[part].Add(triangles[t + 1]); parts[part].Add(triangles[t + 2]);
            }
            return parts;
        }
    }
}
