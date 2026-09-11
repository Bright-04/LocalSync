namespace LocalSync.Core.Security;

/// <summary>
/// RFC 4648 base32 without padding, plus a Luhn mod-32 check character.
/// </summary>
/// <remarks>
/// Base32 rather than base64url because a device ID gets read aloud, typed into
/// a terminal, and pasted across systems that normalise case. The RFC 4648
/// alphabet (A-Z, 2-7) has no lowercase and no 0/1/8/9, so the only realistic
/// confusions are O/0, I/1 and L/1, which <see cref="Normalize"/> maps away.
/// </remarks>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var outputLength = (data.Length * 8 + 4) / 5;
        var result = new char[outputLength];

        int bitBuffer = 0;
        int bitCount = 0;
        int index = 0;

        foreach (var b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                bitCount -= 5;
                result[index++] = Alphabet[(bitBuffer >> bitCount) & 0x1F];
            }
        }

        if (bitCount > 0)
        {
            result[index] = Alphabet[(bitBuffer << (5 - bitCount)) & 0x1F];
        }

        return new string(result);
    }

    public static bool TryDecode(string? encoded, out byte[] data)
    {
        data = [];

        if (string.IsNullOrEmpty(encoded))
        {
            return false;
        }

        var normalized = Normalize(encoded);
        var byteLength = normalized.Length * 5 / 8;
        if (byteLength == 0)
        {
            return false;
        }

        var buffer = new byte[byteLength];
        int bitBuffer = 0;
        int bitCount = 0;
        int index = 0;

        foreach (var c in normalized)
        {
            var value = Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (value < 0)
            {
                return false;
            }

            bitBuffer = (bitBuffer << 5) | value;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                if (index >= buffer.Length)
                {
                    // Trailing bits beyond the whole-byte boundary must be zero
                    // padding, never data.
                    if (((bitBuffer >> bitCount) & 0xFF) != 0)
                    {
                        return false;
                    }

                    continue;
                }

                buffer[index++] = (byte)((bitBuffer >> bitCount) & 0xFF);
            }
        }

        if (index != buffer.Length)
        {
            return false;
        }

        data = buffer;
        return true;
    }

    /// <summary>
    /// Upper-cases, strips separators, and maps the glyphs humans confuse for
    /// alphabet members.
    /// </summary>
    public static string Normalize(string input)
    {
        var buffer = new char[input.Length];
        var length = 0;

        foreach (var raw in input)
        {
            var c = char.ToUpperInvariant(raw);
            c = c switch
            {
                '0' => 'O',
                '1' => 'I',
                '8' => 'B',
                _ => c,
            };

            if (c is '-' or ' ' or '_')
            {
                continue;
            }

            buffer[length++] = c;
        }

        return new string(buffer, 0, length);
    }

    /// <summary>
    /// Luhn mod-32 check character. Typo detection only, zero security value:
    /// it turns a mistyped group into "check digit wrong" rather than a
    /// baffling "pairing failed".
    /// </summary>
    public static char ComputeCheckCharacter(string payload)
    {
        var factor = 2;
        var sum = 0;

        for (var i = payload.Length - 1; i >= 0; i--)
        {
            var codePoint = Alphabet.IndexOf(payload[i], StringComparison.Ordinal);
            if (codePoint < 0)
            {
                throw new ArgumentException($"'{payload[i]}' is not a base32 character.", nameof(payload));
            }

            var addend = factor * codePoint;
            factor = factor == 2 ? 1 : 2;
            addend = (addend / 32) + (addend % 32);
            sum += addend;
        }

        var remainder = sum % 32;
        return Alphabet[(32 - remainder) % 32];
    }
}
