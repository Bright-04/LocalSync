using System.Text.Json;
using LocalSync.Core.Models;
using LocalSync.Core.Security;
using LocalSync.Protocol;
using LocalSync.Protocol.Discovery;

namespace LocalSync.Infrastructure.Discovery;

/// <summary>Translates between announcement datagrams and peers.</summary>
public static class AnnouncementCodec
{
    /// <summary>Longest accepted alias, in characters, before truncation.</summary>
    public const int MaxAliasLength = 64;

    public static byte[] Encode(Announcement announcement) =>
        JsonSerializer.SerializeToUtf8Bytes(announcement, LocalSyncJsonContext.Default.Announcement);

    public static bool TryDecode(ReadOnlySpan<byte> datagram, out Announcement? announcement)
    {
        announcement = null;

        if (datagram.IsEmpty)
        {
            return false;
        }

        try
        {
            var reader = new Utf8JsonReader(datagram);
            announcement = JsonSerializer.Deserialize(ref reader, LocalSyncJsonContext.Default.Announcement);
        }
        catch (JsonException)
        {
            // Anything else on this port is not ours; a malformed datagram is
            // expected traffic, not an error worth logging at warning level.
            return false;
        }

        return announcement is not null;
    }

    /// <summary>
    /// Converts a received announcement into a peer.
    /// </summary>
    /// <remarks>
    /// A peer only gets a native <see cref="DeviceId"/> when it supplied a
    /// well-formed one under "lsx". A LocalSend peer's <c>fingerprint</c> is
    /// deliberately NOT accepted as an id: under HTTP it is a random string,
    /// and under HTTPS it is unverifiable on first contact. Those peers get a
    /// synthetic id derived from their endpoint and are marked as compat.
    /// </remarks>
    public static Peer? ToPeer(Announcement announcement, string sourceAddress, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        if (string.IsNullOrWhiteSpace(sourceAddress))
        {
            return null;
        }

        var alias = SanitizeAlias(announcement.Alias);

        if (announcement.LocalSync is { } ext
            && DeviceId.TryParse(ext.Id, out var nativeId)
            && ext.Port is > 0 and <= 65535)
        {
            return new Peer
            {
                Id = nativeId,
                Alias = alias,
                Address = sourceAddress,
                Port = ext.Port,
                Protocol = PeerProtocol.LocalSync,
                DeviceModel = SanitizeAlias(announcement.DeviceModel),
                DeviceType = SanitizeAlias(announcement.DeviceType),
                FirstSeen = now,
                LastSeen = now,
            };
        }

        if (announcement.Port is <= 0 or > 65535)
        {
            return null;
        }

        return new Peer
        {
            Id = SyntheticCompatId(sourceAddress, announcement.Port),
            Alias = alias,
            Address = sourceAddress,
            Port = announcement.Port,
            Protocol = PeerProtocol.LocalSendCompat,
            DeviceModel = SanitizeAlias(announcement.DeviceModel),
            DeviceType = SanitizeAlias(announcement.DeviceType),
            FirstSeen = now,
            LastSeen = now,
        };
    }

    /// <summary>
    /// A stable table key for a compat peer, which has no real identity.
    /// </summary>
    /// <remarks>
    /// Endpoint-derived and therefore NOT an identity: two hosts swapping DHCP
    /// leases swap keys. It exists only so the registry can deduplicate, and
    /// <see cref="PeerProtocol.LocalSendCompat"/> is what stops it being
    /// mistaken for a pinned id anywhere else.
    /// </remarks>
    public static DeviceId SyntheticCompatId(string address, int port)
    {
        var material = System.Text.Encoding.UTF8.GetBytes($"localsend-compat|{address}|{port}");
        return DeviceId.FromBytes(System.Security.Cryptography.SHA256.HashData(material));
    }

    /// <summary>
    /// Strips control and bidirectional characters from remote-supplied text.
    /// </summary>
    /// <remarks>
    /// Without this, an alias containing U+202E renders reversed, so
    /// "photo&#x202E;gnp.exe" appears as "photoexe.png". Cheap spoof, cheap fix.
    /// </remarks>
    public static string SanitizeAlias(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown device";
        }

        Span<char> buffer = stackalloc char[Math.Min(raw.Length, MaxAliasLength)];
        var length = 0;

        foreach (var c in raw)
        {
            if (length == buffer.Length)
            {
                break;
            }

            if (char.IsControl(c))
            {
                continue;
            }

            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                == System.Globalization.UnicodeCategory.Format)
            {
                continue;
            }

            buffer[length++] = c;
        }

        var cleaned = new string(buffer[..length]).Trim();
        return cleaned.Length == 0 ? "Unknown device" : cleaned;
    }
}
