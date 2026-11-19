using System.Numerics;
using System.Security.Cryptography;

namespace FUNC
{
    // Ed25519 signature verification (RFC 8032, section 5.1.7), for wallet sign-in.
    // .NET has no Ed25519. This follows the RFC's reference implementation (section 6)
    // with BigInteger arithmetic. It only verifies, and every input is public, so it
    // doesn't need to be constant time. It is checked against the RFC's test vectors
    // in FUNC.Tests.
    public static class Ed25519
    {
        private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
        // Order of the base point.
        private static readonly BigInteger L = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
        private static readonly BigInteger D = Mod(-121665 * Inverse(121666));
        private static readonly BigInteger SqrtM1 = BigInteger.ModPow(2, (P - 1) / 4, P);
        private static readonly Point B = BasePoint();

        // Extended homogeneous coordinates: x = X/Z, y = Y/Z, x*y = T/Z.
        private readonly record struct Point(BigInteger X, BigInteger Y, BigInteger Z, BigInteger T);

        public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
        {
            if (publicKey.Length != 32 || signature.Length != 64) return false;
            Point? a = Decompress(publicKey);
            if (a == null) return false;
            byte[] rBytes = signature[..32];
            Point? r = Decompress(rBytes);
            if (r == null) return false;
            BigInteger s = FromLittleEndian(signature[32..]);
            if (s >= L) return false;

            byte[] hash = SHA512.HashData([.. rBytes, .. publicKey, .. message]);
            BigInteger h = FromLittleEndian(hash) % L;

            Point sB = Multiply(s, B);
            Point hA = Multiply(h, a.Value);
            return Equal(sB, Add(r.Value, hA));
        }

        private static BigInteger Mod(BigInteger a)
        {
            BigInteger m = a % P;
            return m.Sign < 0 ? m + P : m;
        }

        private static BigInteger Inverse(BigInteger a) => BigInteger.ModPow(Mod(a), P - 2, P);

        private static BigInteger FromLittleEndian(byte[] bytes) => new(bytes, isUnsigned: true, isBigEndian: false);

        private static Point Add(Point p, Point q)
        {
            BigInteger a = Mod((p.Y - p.X) * (q.Y - q.X));
            BigInteger b = Mod((p.Y + p.X) * (q.Y + q.X));
            BigInteger c = Mod(2 * p.T * q.T * D);
            BigInteger d = Mod(2 * p.Z * q.Z);
            BigInteger e = b - a, f = d - c, g = d + c, h = b + a;
            return new(Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h));
        }

        private static Point Multiply(BigInteger s, Point p)
        {
            Point q = new(0, 1, 1, 0); // the neutral element
            while (s > 0)
            {
                if (!s.IsEven) q = Add(q, p);
                p = Add(p, p);
                s >>= 1;
            }
            return q;
        }

        private static bool Equal(Point p, Point q) =>
            Mod(p.X * q.Z - q.X * p.Z) == 0 && Mod(p.Y * q.Z - q.Y * p.Z) == 0;

        private static BigInteger? RecoverX(BigInteger y, int sign)
        {
            if (y >= P) return null;
            BigInteger x2 = Mod((y * y - 1) * Inverse(D * y * y + 1));
            if (x2.IsZero) return sign == 0 ? BigInteger.Zero : null;
            BigInteger x = BigInteger.ModPow(x2, (P + 3) / 8, P);
            if (Mod(x * x - x2) != 0) x = Mod(x * SqrtM1);
            if (Mod(x * x - x2) != 0) return null;
            if ((int)(x & 1) != sign) x = P - x;
            return x;
        }

        private static Point? Decompress(byte[] bytes)
        {
            if (bytes.Length != 32) return null;
            BigInteger y = FromLittleEndian(bytes);
            int sign = (int)(y >> 255);
            y &= (BigInteger.One << 255) - 1;
            BigInteger? x = RecoverX(y, sign);
            if (x == null) return null;
            return new Point(x.Value, y, 1, Mod(x.Value * y));
        }

        private static Point BasePoint()
        {
            BigInteger y = Mod(4 * Inverse(5));
            BigInteger x = RecoverX(y, 0)!.Value;
            return new Point(x, y, 1, Mod(x * y));
        }
    }
}
