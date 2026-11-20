using System.Buffers.Binary;
using System.Text;

namespace FUNC
{
    // Just enough of Algorand's address scheme to match accounts by public key: base32
    // decoding, and application account derivation, which needs SHA-512/256 (not in .NET).
    public static class AlgoAddress
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        // The 32-byte public key in a 58-character address, as lowercase hex; null if malformed.
        public static string? KeyHex(string? address)
        {
            if (address is not { Length: 58 }) return null;
            byte[] bytes = new byte[36];
            int buffer = 0, bits = 0, n = 0;
            foreach (char c in address)
            {
                int v = Alphabet.IndexOf(c);
                if (v < 0) return null;
                buffer = (buffer << 5) | v;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    if (n < bytes.Length) bytes[n++] = (byte)(buffer >> bits);
                }
            }
            byte[] key = bytes[..32];
            byte[] check = Sha512_256(key)[^4..];
            return check.AsSpan().SequenceEqual(bytes.AsSpan(32)) ? Convert.ToHexStringLower(key) : null;
        }

        // An application's account: SHA-512/256("appID" || app id, big-endian).
        public static string AppKeyHex(ulong appId)
        {
            byte[] data = new byte[13];
            Encoding.ASCII.GetBytes("appID").CopyTo(data, 0);
            BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(5), appId);
            return Convert.ToHexStringLower(Sha512_256(data));
        }

        private static readonly ulong[] K =
        [
            0x428a2f98d728ae22, 0x7137449123ef65cd, 0xb5c0fbcfec4d3b2f, 0xe9b5dba58189dbbc, 0x3956c25bf348b538,
            0x59f111f1b605d019, 0x923f82a4af194f9b, 0xab1c5ed5da6d8118, 0xd807aa98a3030242, 0x12835b0145706fbe,
            0x243185be4ee4b28c, 0x550c7dc3d5ffb4e2, 0x72be5d74f27b896f, 0x80deb1fe3b1696b1, 0x9bdc06a725c71235,
            0xc19bf174cf692694, 0xe49b69c19ef14ad2, 0xefbe4786384f25e3, 0x0fc19dc68b8cd5b5, 0x240ca1cc77ac9c65,
            0x2de92c6f592b0275, 0x4a7484aa6ea6e483, 0x5cb0a9dcbd41fbd4, 0x76f988da831153b5, 0x983e5152ee66dfab,
            0xa831c66d2db43210, 0xb00327c898fb213f, 0xbf597fc7beef0ee4, 0xc6e00bf33da88fc2, 0xd5a79147930aa725,
            0x06ca6351e003826f, 0x142929670a0e6e70, 0x27b70a8546d22ffc, 0x2e1b21385c26c926, 0x4d2c6dfc5ac42aed,
            0x53380d139d95b3df, 0x650a73548baf63de, 0x766a0abb3c77b2a8, 0x81c2c92e47edaee6, 0x92722c851482353b,
            0xa2bfe8a14cf10364, 0xa81a664bbc423001, 0xc24b8b70d0f89791, 0xc76c51a30654be30, 0xd192e819d6ef5218,
            0xd69906245565a910, 0xf40e35855771202a, 0x106aa07032bbd1b8, 0x19a4c116b8d2d0c8, 0x1e376c085141ab53,
            0x2748774cdf8eeb99, 0x34b0bcb5e19b48a8, 0x391c0cb3c5c95a63, 0x4ed8aa4ae3418acb, 0x5b9cca4f7763e373,
            0x682e6ff3d6b2b8a3, 0x748f82ee5defb2fc, 0x78a5636f43172f60, 0x84c87814a1f0ab72, 0x8cc702081a6439ec,
            0x90befffa23631e28, 0xa4506cebde82bde9, 0xbef9a3f7b2c67915, 0xc67178f2e372532b, 0xca273eceea26619c,
            0xd186b8c721c0c207, 0xeada7dd6cde0eb1e, 0xf57d4f7fee6ed178, 0x06f067aa72176fba, 0x0a637dc5a2c898a6,
            0x113f9804bef90dae, 0x1b710b35131c471b, 0x28db77f523047d84, 0x32caab7b40c72493, 0x3c9ebe0a15c9bebc,
            0x431d67c49c100d4c, 0x4cc5d4becb3e42b6, 0x597f299cfc657e2a, 0x5fcb6fab3ad6faec, 0x6c44198c4a475817,
        ];

        // FIPS 180-4 SHA-512/256: SHA-512 with its own initial values, cut to 32 bytes.
        internal static byte[] Sha512_256(ReadOnlySpan<byte> message)
        {
            ulong[] h =
            [
                0x22312194fc2bf72c, 0x9f555fa3c84c64c2, 0x2393b86b6f53b151, 0x963877195940eabd,
                0x96283ee2a88effe3, 0xbe5e1e2553863992, 0x2b0199fc2c85b8aa, 0x0eb72ddc81c52ca2,
            ];
            int padded = (message.Length + 17 + 127) / 128 * 128;
            byte[] data = new byte[padded];
            message.CopyTo(data);
            data[message.Length] = 0x80;
            BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(padded - 8), (ulong)message.Length * 8);

            ulong[] w = new ulong[80];
            for (int block = 0; block < padded; block += 128)
            {
                for (int t = 0; t < 16; t++) w[t] = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(block + t * 8));
                for (int t = 16; t < 80; t++)
                {
                    ulong s0 = ulong.RotateRight(w[t - 15], 1) ^ ulong.RotateRight(w[t - 15], 8) ^ (w[t - 15] >> 7);
                    ulong s1 = ulong.RotateRight(w[t - 2], 19) ^ ulong.RotateRight(w[t - 2], 61) ^ (w[t - 2] >> 6);
                    w[t] = w[t - 16] + s0 + w[t - 7] + s1;
                }
                ulong a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], hh = h[7];
                for (int t = 0; t < 80; t++)
                {
                    ulong t1 = hh + (ulong.RotateRight(e, 14) ^ ulong.RotateRight(e, 18) ^ ulong.RotateRight(e, 41))
                        + ((e & f) ^ (~e & g)) + K[t] + w[t];
                    ulong t2 = (ulong.RotateRight(a, 28) ^ ulong.RotateRight(a, 34) ^ ulong.RotateRight(a, 39))
                        + ((a & b) ^ (a & c) ^ (b & c));
                    hh = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
                }
                h[0] += a; h[1] += b; h[2] += c; h[3] += d; h[4] += e; h[5] += f; h[6] += g; h[7] += hh;
            }
            byte[] result = new byte[32];
            for (int i = 0; i < 4; i++) BinaryPrimitives.WriteUInt64BigEndian(result.AsSpan(i * 8), h[i]);
            return result;
        }
    }
}
