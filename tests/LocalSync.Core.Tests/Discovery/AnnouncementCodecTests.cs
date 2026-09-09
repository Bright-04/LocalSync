using System.Text;
using System.Text.Json;
using LocalSync.Core.Models;
using LocalSync.Core.Security;
using LocalSync.Infrastructure.Discovery;
using LocalSync.Protocol.Discovery;

namespace LocalSync.Core.Tests.Discovery;

public class AnnouncementCodecTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private static Announcement NativeAnnouncement(DeviceId id) => new()
    {
        Alias = "Quang's Mac",
        DeviceType = "desktop",
        Fingerprint = id.ToString(),
        Port = 53317,
        Protocol = "http",
        Announce = true,
        LocalSync = new LocalSyncExtension { Version = 1, Id = id.ToString(), Port = 53318 },
    };

    [Fact]
    public void RoundTripsThroughTheWireFormat()
    {
        var id = DeviceId.CreateRandom();

        Assert.True(AnnouncementCodec.TryDecode(
            AnnouncementCodec.Encode(NativeAnnouncement(id)), out var decoded));

        Assert.NotNull(decoded);
        Assert.Equal("Quang's Mac", decoded.Alias);
        Assert.Equal(53318, decoded.LocalSync!.Port);
        Assert.Equal(id.ToString(), decoded.LocalSync.Id);
    }

    [Fact]
    public void EmitsExactlyTheLocalSendV22FieldNames()
    {
        var json = Encoding.UTF8.GetString(AnnouncementCodec.Encode(NativeAnnouncement(DeviceId.CreateRandom())));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // These names are LocalSend's contract, not ours. A rename here breaks
        // interop silently, so assert them literally.
        foreach (var required in new[]
                 {
                     "alias", "version", "deviceType", "fingerprint",
                     "port", "protocol", "download", "announce",
                 })
        {
            Assert.True(root.TryGetProperty(required, out _), $"missing '{required}'");
        }

        Assert.Equal("2.2", root.GetProperty("version").GetString());
    }

    [Fact]
    public void NativeFieldsAreNamespacedSoLocalSendCanIgnoreThem()
    {
        var json = Encoding.UTF8.GetString(AnnouncementCodec.Encode(NativeAnnouncement(DeviceId.CreateRandom())));
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("lsx", out var ext));
        Assert.True(ext.TryGetProperty("id", out _));
        Assert.True(ext.TryGetProperty("port", out _));
    }

    [Fact]
    public void DecodesAPlainLocalSendAnnouncementThatHasNoExtension()
    {
        const string json = """
            {"alias":"Pixel","version":"2.2","deviceModel":"Pixel 8","deviceType":"mobile",
             "fingerprint":"abc123","port":53317,"protocol":"https","download":false,"announce":true}
            """;

        Assert.True(AnnouncementCodec.TryDecode(Encoding.UTF8.GetBytes(json), out var decoded));
        Assert.NotNull(decoded);
        Assert.Null(decoded.LocalSync);

        var peer = AnnouncementCodec.ToPeer(decoded, "192.168.1.40", Now);

        Assert.NotNull(peer);
        Assert.Equal(PeerProtocol.LocalSendCompat, peer.Protocol);
        Assert.Equal(53317, peer.Port);
    }

    [Fact]
    public void ALocalSendFingerprintIsNeverAcceptedAsANativeIdentity()
    {
        // Under HTTP the fingerprint is a random string with no cryptographic
        // meaning; under HTTPS it is unverifiable on first contact. Either way
        // it must not become a pinned device id.
        const string json = """
            {"alias":"Impostor","version":"2.2","deviceType":"desktop",
             "fingerprint":"NOTAREALDEVICEIDBUTLOOKSLIKEONEAAAAAAAAAAAAAAAAAAAAA",
             "port":53317,"protocol":"https","announce":true}
            """;

        Assert.True(AnnouncementCodec.TryDecode(Encoding.UTF8.GetBytes(json), out var decoded));
        var peer = AnnouncementCodec.ToPeer(decoded!, "192.168.1.50", Now);

        Assert.NotNull(peer);
        Assert.Equal(PeerProtocol.LocalSendCompat, peer.Protocol);
        Assert.Equal(AnnouncementCodec.SyntheticCompatId("192.168.1.50", 53317), peer.Id);
    }

    [Fact]
    public void ANativeExtensionWithAMalformedIdFallsBackToCompat()
    {
        var announcement = NativeAnnouncement(DeviceId.CreateRandom());
        announcement.LocalSync!.Id = "not-a-device-id";

        var peer = AnnouncementCodec.ToPeer(announcement, "192.168.1.60", Now);

        Assert.NotNull(peer);
        Assert.Equal(PeerProtocol.LocalSendCompat, peer.Protocol);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void PortsOutsideTheValidRangeAreRejected(int port)
    {
        var announcement = NativeAnnouncement(DeviceId.CreateRandom());
        announcement.LocalSync = null;
        announcement.Port = port;

        Assert.Null(AnnouncementCodec.ToPeer(announcement, "192.168.1.70", Now));
    }

    [Fact]
    public void AnEmptySourceAddressIsRejected() =>
        Assert.Null(AnnouncementCodec.ToPeer(NativeAnnouncement(DeviceId.CreateRandom()), "", Now));

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x00, 0x01, 0x02 })]
    public void TryDecode_RejectsNonJsonDatagrams(byte[] datagram) =>
        Assert.False(AnnouncementCodec.TryDecode(datagram, out _));

    [Fact]
    public void TryDecode_RejectsTruncatedJson() =>
        Assert.False(AnnouncementCodec.TryDecode(Encoding.UTF8.GetBytes("{\"alias\":"), out _));

    [Theory]
    // U+202E reverses what follows, so "photo<RLO>gnp.exe" reads as
    // "photoexe.png" to anyone looking at the peer list.
    [InlineData("photo‮gnp.exe", "photognp.exe")]
    [InlineData("a‎b", "ab")]
    [InlineData("normal name", "normal name")]
    [InlineData("  padded  ", "padded")]
    public void SanitizeAlias_StripsBidiAndFormatControls(string raw, string expected) =>
        Assert.Equal(expected, AnnouncementCodec.SanitizeAlias(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("‮‎")]
    public void SanitizeAlias_FallsBackWhenNothingUsableRemains(string? raw) =>
        Assert.Equal("Unknown device", AnnouncementCodec.SanitizeAlias(raw));

    [Fact]
    public void SanitizeAlias_CapsLength()
    {
        var sanitized = AnnouncementCodec.SanitizeAlias(new string('x', 500));

        Assert.Equal(AnnouncementCodec.MaxAliasLength, sanitized.Length);
    }

    [Fact]
    public void SyntheticCompatId_IsStablePerEndpointAndDistinctAcrossThem()
    {
        var a = AnnouncementCodec.SyntheticCompatId("192.168.1.10", 53317);

        Assert.Equal(a, AnnouncementCodec.SyntheticCompatId("192.168.1.10", 53317));
        Assert.NotEqual(a, AnnouncementCodec.SyntheticCompatId("192.168.1.11", 53317));
        Assert.NotEqual(a, AnnouncementCodec.SyntheticCompatId("192.168.1.10", 53318));
    }
}
