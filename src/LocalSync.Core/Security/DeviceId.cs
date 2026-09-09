using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace LocalSync.Core.Security;

/// <summary>
/// A device's stable identity: 32 bytes, rendered as unpadded base32.
/// </summary>
/// <remarks>
/// In Phase 2a this becomes SHA-256 of the device certificate's
/// SubjectPublicKeyInfo. Pinning the SPKI rather than the certificate is what
/// lets certificates rotate without invalidating every existing pairing -
/// LocalSend pins the certificate hash and therefore cannot rotate.
/// <para>
/// Until then the bytes are random and persisted, which is enough to be stable
/// across restarts. Only the derivation changes, not the type or its wire form.
/// </para>
/// </remarks>
public readonly struct DeviceId : IEquatable<DeviceId>
{
    public const int SizeInBytes = 32;
    private const int EncodedLength = 52;

    private readonly string _value;

    private DeviceId(string canonical) => _value = canonical;

    public bool IsEmpty => string.IsNullOrEmpty(_value);

    public static DeviceId FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SizeInBytes)
        {
            throw new ArgumentException($"A device id is exactly {SizeInBytes} bytes.", nameof(bytes));
        }

        return new DeviceId(Base32.Encode(bytes));
    }

    /// <summary>Derives an id from a certificate's SubjectPublicKeyInfo.</summary>
    public static DeviceId FromSubjectPublicKeyInfo(ReadOnlySpan<byte> spki) =>
        FromBytes(SHA256.HashData(spki));

    public static DeviceId CreateRandom() =>
        FromBytes(RandomNumberGenerator.GetBytes(SizeInBytes));

    public static bool TryParse(string? text, out DeviceId id)
    {
        id = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = Base32.Normalize(text);
        if (normalized.Length != EncodedLength)
        {
            return false;
        }

        if (!Base32.TryDecode(normalized, out var bytes) || bytes.Length != SizeInBytes)
        {
            return false;
        }

        id = new DeviceId(normalized);
        return true;
    }

    public byte[] ToByteArray() =>
        Base32.TryDecode(_value, out var bytes) ? bytes : [];

    /// <summary>The canonical 52-character form used on the wire.</summary>
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// Grouped with per-group check characters, for a human to transcribe.
    /// </summary>
    public string ToDisplayString()
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var groups = new List<string>(4);
        for (var i = 0; i < _value.Length; i += 13)
        {
            var group = _value.Substring(i, Math.Min(13, _value.Length - i));
            groups.Add(group + Base32.ComputeCheckCharacter(group));
        }

        return string.Join('-', groups);
    }

    /// <summary>
    /// A short mnemonic for the pairing screen. Humans compare words reliably
    /// and base32 unreliably, so this is what a confirmation prompt shows while
    /// <see cref="ToDisplayString"/> is what someone types.
    /// </summary>
    public string ToMnemonic()
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var bytes = ToByteArray();
        var words = new string[MnemonicWordCount];
        for (var i = 0; i < MnemonicWordCount; i++)
        {
            // One byte per word against a 256-word list, so every word is
            // equally likely. Indexing a non-power-of-two list would skew it.
            words[i] = MnemonicWords.Words[bytes[i]];
        }

        return string.Join('-', words);
    }

    /// <summary>Five words drawn from a 256-word list: 40 bits of display entropy.</summary>
    private const int MnemonicWordCount = 5;

    public bool Equals(DeviceId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is DeviceId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(DeviceId left, DeviceId right) => left.Equals(right);

    public static bool operator !=(DeviceId left, DeviceId right) => !left.Equals(right);
}
