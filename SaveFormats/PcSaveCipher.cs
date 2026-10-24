// Port of MVGLTools/SaveFile.cpp by SydMontague (BSD-3-Clause).
// See THIRD-PARTY-NOTICES.txt for the copyright and license.
using System;
using System.Numerics;
using System.Text;

namespace DigimonStorySaveEditor.SaveFormats
{
    internal static class PcSaveCipher
    {
        // Numbered saves use @Lilimon; slot_ and system_data use different keys
        // and are deliberately not accepted by this editor.
        public static byte[] Decrypt(byte[] data) { return Transform(data, false); }
        public static byte[] Encrypt(byte[] data) { return Transform(data, true); }

        private static byte[] Transform(byte[] input, bool encrypt)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0 || input.Length % 16 != 0)
                throw new ArgumentException("A PC save must contain complete 16-byte blocks.");
            byte[] data = (byte[])input.Clone();
            unchecked
            {
                ulong key = BitConverter.ToUInt64(Encoding.ASCII.GetBytes("@Lilimon"), 0) ^ 0x1415926535897932UL;
                int[] shifts = { 8, 48, 24, 0, 16, 3, 40, 21, 32, 47, 56 };
                byte[] keyBytes = new byte[shifts.Length];
                for (int i = 0; i < shifts.Length; i++) keyBytes[i] = (byte)(key >> shifts[i]);
                if (!encrypt) RotateBlocks(data, key, false);

                ulong tmp = (ulong)((new BigInteger(key) * 0x3B2153E7529FE1FFUL) >> 64);
                uint state = (uint)(key - (((((key - tmp) >> 1) + tmp) >> 15) * 0xCFF7));
                uint sum = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    byte value = data[i];
                    byte adjustment = (byte)(((0xAB8F69E3UL * sum) >> 39) * 0x41);
                    if (encrypt)
                    {
                        value = (byte)((value ^ (byte)state ^ keyBytes[i % 11]) + (byte)sum + adjustment);
                        sum += data[i];
                    }
                    else
                    {
                        value = (byte)((byte)(value - adjustment - (byte)sum) ^ keyBytes[i % 11] ^ (byte)state);
                        sum += value;
                    }
                    data[i] = value;
                    // The multiply/add wraps at 32 bits in the original C++.
                    ulong next = (uint)(state * 0x10DCDU + 13U);
                    state = (uint)(next - (((next * 0x40004001UL) >> 61) * 0x7FFF7FFFUL));
                }
                if (encrypt) RotateBlocks(data, key, true);
            }
            return data;
        }

        private static void RotateBlocks(byte[] data, ulong key, bool encrypt)
        {
            unchecked
            {
                ulong vector = (ulong)((new BigInteger(0x801302D26B3BEAE5UL) * key) >> 78);
                uint state = (uint)((uint)key - vector * 0x7FED);
                for (int offset = 0; offset < data.Length; offset += 16)
                {
                    uint quotient = (uint)((state * 0x24924925UL) >> 32);
                    int count = -(int)(((((state - quotient) >> 1) + quotient) >> 2) * 7 - state - 1);
                    ulong sum = 0;
                    for (int i = offset; i < offset + 16; i++)
                    {
                        byte value = data[i];
                        if (encrypt) sum += value;
                        data[i] = encrypt
                            ? (byte)((value << count) | (value >> (8 - count)))
                            : (byte)((value >> count) | (value << (8 - count)));
                        if (!encrypt) sum += data[i];
                    }
                    ulong tmp = (0x72C62A25UL * sum) >> 40;
                    uint next = (uint)((state * 0x10DCDU + 1U) + (sum - tmp * 0x23B) * 2);
                    state = (uint)(next - (((0x40004001UL * next) >> 61) * 0x7FFF7FFFUL));
                }
            }
        }
    }
}
