using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FUNC;

// A minimal OpenPGP (RFC 4880) verifier for detached v4 RSA signatures: just enough to
// check go-algorand release files without depending on gpg being installed.
//
// Trust is anchored on a pinned primary key fingerprint. A signing subkey is trusted only
// if that primary key certified it (and hasn't revoked it), and a signature counts only if
// it was made while its subkey was valid.
public static class Pgp
{
    public sealed record SigningKey(byte[] Fingerprint, RSAParameters Rsa, DateTimeOffset Created, DateTimeOffset? Expires)
    {
        public string FingerprintHex => Convert.ToHexString(Fingerprint);
    }

    private sealed record Packet(int Tag, byte[] Body);

    private sealed record PublicKey(byte[] Body, byte[] Fingerprint, RSAParameters Rsa, DateTimeOffset Created);

    private sealed class Signature
    {
        public int Type, PubAlg, HashAlg;
        public byte[] HashedPart = [];  // version byte through the end of the hashed subpackets
        public byte[] Left16 = [];
        public byte[] Value = [];
        public DateTimeOffset? Created;
        public long? SigExpirySeconds;
        public long? KeyExpirySeconds;
        public byte? KeyFlags;
        public byte[]? IssuerKeyId;
        public byte[]? IssuerFingerprint;
        public bool HasUnknownCritical;
    }

    private const byte KeyFlagSign = 0x02;

    // Reads a resource embedded in FUNC's assembly (e.g. a pinned keyring).
    public static string ReadEmbedded(string name)
    {
        using var s = typeof(Pgp).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource {name}");
        using var reader = new StreamReader(s);
        return reader.ReadToEnd();
    }

    // Returns the signing subkeys in the given armored keyrings that the primary key with
    // `primaryFingerprint` has certified. Keyrings that fail to parse are skipped.
    public static List<SigningKey> LoadSigningKeys(string primaryFingerprint, params string[] armoredKeyrings)
    {
        byte[] want = Convert.FromHexString(primaryFingerprint);
        var bindings = new Dictionary<string, (SigningKey Key, DateTimeOffset BoundAt)>();
        var revoked = new HashSet<string>();
        bool primaryRevoked = false;

        foreach (string armored in armoredKeyrings)
        {
            List<Packet> packets;
            try { packets = ReadPackets(Dearmor(armored)); }
            catch (Exception ex) when (IsParseError(ex)) { continue; }

            PublicKey? primary = null;
            PublicKey? sub = null;
            foreach (var p in packets)
            {
                try
                {
                    switch (p.Tag)
                    {
                        case 6: // public key: only the pinned one starts a block we care about
                            var key = ParseKey(p.Body);
                            primary = key != null && key.Fingerprint.AsSpan().SequenceEqual(want) ? key : null;
                            sub = null;
                            break;
                        case 14: // public subkey
                            sub = primary == null ? null : ParseKey(p.Body);
                            break;
                        case 2 when primary != null:
                            var sig = ParseSignature(p.Body);
                            if (sig == null || sig.HasUnknownCritical || sig.Created is not { } at) break;
                            if (sub == null)
                            {
                                if (sig.Type == 0x20 && Check(sig, primary.Rsa, KeyMaterial(primary.Body)))
                                    primaryRevoked = true;
                                break;
                            }
                            byte[] material = [.. KeyMaterial(primary.Body), .. KeyMaterial(sub.Body)];
                            string fpr = Convert.ToHexString(sub.Fingerprint);
                            if (sig.Type == 0x28 && Check(sig, primary.Rsa, material))
                            {
                                revoked.Add(fpr);
                            }
                            else if (sig.Type == 0x18 && ((sig.KeyFlags ?? 0) & KeyFlagSign) != 0
                                && Check(sig, primary.Rsa, material)
                                && (!bindings.TryGetValue(fpr, out var existing) || at > existing.BoundAt))
                            {
                                DateTimeOffset? expires = sig.KeyExpirySeconds is > 0 and { } s ? sub.Created.AddSeconds(s) : null;
                                bindings[fpr] = (new SigningKey(sub.Fingerprint, sub.Rsa, sub.Created, expires), at);
                            }
                            break;
                    }
                }
                catch (Exception ex) when (IsParseError(ex)) { }
            }
        }

        if (primaryRevoked) return [];
        return bindings.Where(b => !revoked.Contains(b.Key)).Select(b => b.Value.Key).ToList();
    }

    // True if `signature` (a binary detached signature) is a valid signature over `data`
    // by one of `keys`, made while that key was valid.
    public static bool Verify(byte[] data, byte[] signature, IReadOnlyCollection<SigningKey> keys)
    {
        try
        {
            foreach (var p in ReadPackets(signature))
            {
                if (p.Tag != 2) continue;
                var sig = ParseSignature(p.Body);
                if (sig is not { Type: 0x00 } || sig.HasUnknownCritical || sig.Created is not { } created) continue;
                if (sig.SigExpirySeconds is > 0 and { } exp && DateTimeOffset.UtcNow > created.AddSeconds(exp)) continue;

                foreach (var key in keys)
                {
                    if (sig.IssuerFingerprint is { } f && !f.AsSpan().SequenceEqual(key.Fingerprint)) continue;
                    if (sig.IssuerKeyId is { } id && !id.AsSpan().SequenceEqual(key.Fingerprint.AsSpan()[^8..])) continue;
                    if (created < key.Created || created >= key.Expires) continue;
                    if (Check(sig, key.Rsa, data)) return true;
                }
            }
        }
        catch (Exception ex) when (IsParseError(ex)) { }
        return false;
    }

    private static bool IsParseError(Exception ex) =>
        ex is FormatException or IndexOutOfRangeException or ArgumentException or CryptographicException;

    // Concatenates the binary contents of every ASCII-armored block in `text`.
    private static byte[] Dearmor(string text)
    {
        var output = new List<byte>();
        var b64 = new StringBuilder();
        bool inBlock = false, inBody = false;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (!inBlock)
            {
                if (line.StartsWith("-----BEGIN PGP ")) { inBlock = true; inBody = false; b64.Clear(); }
                continue;
            }
            if (line.StartsWith("-----END PGP "))
            {
                output.AddRange(Convert.FromBase64String(b64.ToString()));
                inBlock = false;
                continue;
            }
            if (!inBody) { if (line.Length == 0) inBody = true; continue; }  // armor headers end at a blank line
            if (line.StartsWith('=')) continue;  // CRC-24 line
            b64.Append(line);
        }
        if (output.Count == 0) throw new FormatException("No OpenPGP armor found");
        return [.. output];
    }

    private static List<Packet> ReadPackets(byte[] data)
    {
        var packets = new List<Packet>();
        int i = 0;
        while (i < data.Length)
        {
            int h = data[i++];
            if ((h & 0x80) == 0) throw new FormatException("Invalid OpenPGP packet header");
            int tag;
            long len;
            if ((h & 0x40) != 0)
            {
                tag = h & 0x3F;
                int o1 = data[i++];
                if (o1 < 192) len = o1;
                else if (o1 < 224) len = ((o1 - 192) << 8) + data[i++] + 192;
                else if (o1 == 255) { len = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i)); i += 4; }
                else throw new FormatException("Partial-length OpenPGP packets are not supported");
            }
            else
            {
                tag = (h >> 2) & 0x0F;
                switch (h & 3)
                {
                    case 0: len = data[i]; i += 1; break;
                    case 1: len = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i)); i += 2; break;
                    case 2: len = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i)); i += 4; break;
                    default: throw new FormatException("Indeterminate-length OpenPGP packets are not supported");
                }
            }
            if (len > data.Length - i) throw new FormatException("Truncated OpenPGP packet");
            packets.Add(new Packet(tag, data[i..(i + (int)len)]));
            i += (int)len;
        }
        return packets;
    }

    // v4 RSA public key or subkey packet; other versions/algorithms return null.
    private static PublicKey? ParseKey(byte[] body)
    {
        if (body.Length < 6 || body[0] != 4 || body[5] is not (1 or 3)) return null;
        int i = 6;
        byte[] n = ReadMpi(body, ref i);
        byte[] e = ReadMpi(body, ref i);
        byte[] fingerprint = SHA1.HashData(KeyMaterial(body));
        var created = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(1)));
        return new PublicKey(body, fingerprint, new RSAParameters { Modulus = n, Exponent = e }, created);
    }

    // How a key packet is fed into fingerprints and certification hashes.
    private static byte[] KeyMaterial(byte[] body) =>
        [0x99, (byte)(body.Length >> 8), (byte)body.Length, .. body];

    private static Signature? ParseSignature(byte[] body)
    {
        if (body.Length < 6 || body[0] != 4) return null;
        var sig = new Signature { Type = body[1], PubAlg = body[2], HashAlg = body[3] };
        int hashedLen = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(4));
        int i = 6;
        ReadSubpackets(body.AsSpan(i, hashedLen), sig, hashed: true);
        i += hashedLen;
        sig.HashedPart = body[..i];
        int unhashedLen = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(i));
        i += 2;
        ReadSubpackets(body.AsSpan(i, unhashedLen), sig, hashed: false);
        i += unhashedLen;
        sig.Left16 = body[i..(i + 2)];
        i += 2;
        sig.Value = ReadMpi(body, ref i);
        return sig;
    }

    private static void ReadSubpackets(ReadOnlySpan<byte> s, Signature sig, bool hashed)
    {
        int i = 0;
        while (i < s.Length)
        {
            int len = s[i++];
            if (len >= 192 && len < 255) len = ((len - 192) << 8) + s[i++] + 192;
            else if (len == 255) { len = (int)BinaryPrimitives.ReadUInt32BigEndian(s[i..]); i += 4; }
            if (len < 1 || len > s.Length - i) throw new FormatException("Invalid OpenPGP subpacket length");

            bool critical = (s[i] & 0x80) != 0;
            int type = s[i] & 0x7F;
            var d = s.Slice(i + 1, len - 1);
            i += len;

            // Only the issuer may come from the unhashed area; it's a hint for picking a key.
            switch (type)
            {
                case 2 when hashed && d.Length == 4:
                    sig.Created = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(d));
                    break;
                case 3 when hashed && d.Length == 4:
                    sig.SigExpirySeconds = BinaryPrimitives.ReadUInt32BigEndian(d);
                    break;
                case 9 when hashed && d.Length == 4:
                    sig.KeyExpirySeconds = BinaryPrimitives.ReadUInt32BigEndian(d);
                    break;
                case 27 when hashed && d.Length >= 1:
                    sig.KeyFlags = d[0];
                    break;
                case 16 when d.Length == 8:
                    sig.IssuerKeyId = d.ToArray();
                    break;
                case 33 when d.Length == 21 && d[0] == 4:
                    sig.IssuerFingerprint = d[1..].ToArray();
                    break;
                default:
                    // RFC 4880 5.2.3.1: an unrecognized critical subpacket invalidates the signature.
                    // 28 (signer's user ID) and 32 (embedded signature) are informational here.
                    if (hashed && critical && type is not (28 or 32)) sig.HasUnknownCritical = true;
                    break;
            }
        }
    }

    private static byte[] ReadMpi(byte[] b, ref int i)
    {
        int bits = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i));
        i += 2;
        int len = (bits + 7) / 8;
        if (len > b.Length - i) throw new FormatException("Truncated OpenPGP MPI");
        byte[] v = b[i..(i + len)];
        i += len;
        return v;
    }

    // Verifies a v4 RSA signature over `material` (the signed data or key packets).
    private static bool Check(Signature sig, RSAParameters key, ReadOnlySpan<byte> material)
    {
        if (sig.PubAlg is not (1 or 3)) return false;
        HashAlgorithmName name = sig.HashAlg switch
        {
            8 => HashAlgorithmName.SHA256,
            9 => HashAlgorithmName.SHA384,
            10 => HashAlgorithmName.SHA512,
            _ => default,  // MD5/SHA-1 and unknown algorithms are rejected
        };
        if (name == default) return false;

        using var h = IncrementalHash.CreateHash(name);
        h.AppendData(material);
        h.AppendData(sig.HashedPart);
        Span<byte> trailer = stackalloc byte[6];
        trailer[0] = 4;
        trailer[1] = 0xFF;
        BinaryPrimitives.WriteUInt32BigEndian(trailer[2..], (uint)sig.HashedPart.Length);
        h.AppendData(trailer);
        byte[] digest = h.GetHashAndReset();
        if (digest[0] != sig.Left16[0] || digest[1] != sig.Left16[1]) return false;

        // The signature MPI drops leading zeros; RSA verification wants it at modulus length.
        int modLen = key.Modulus!.Length;
        if (sig.Value.Length > modLen) return false;
        byte[] value = new byte[modLen];
        sig.Value.CopyTo(value, modLen - sig.Value.Length);

        using var rsa = RSA.Create(key);
        return rsa.VerifyHash(digest, value, name, RSASignaturePadding.Pkcs1);
    }
}
