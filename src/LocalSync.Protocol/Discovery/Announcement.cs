using System.Text.Json.Serialization;

namespace LocalSync.Protocol.Discovery;

/// <summary>
/// A discovery announcement, wire-compatible with LocalSend protocol v2.2.
/// </summary>
/// <remarks>
/// Every member carries an explicit <see cref="JsonPropertyNameAttribute"/>.
/// This is a contract we do not own, so a global naming policy must never be
/// what decides these names: changing the policy would silently break interop.
/// <para>
/// Native LocalSync fields live under <see cref="LocalSync"/> so a LocalSend
/// client sees a spec-exact document and ignores the extra key, letting one
/// packet serve both protocols.
/// </para>
/// </remarks>
public sealed class Announcement
{
    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.2";

    [JsonPropertyName("deviceModel")]
    public string? DeviceModel { get; set; }

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = "desktop";

    /// <summary>
    /// LocalSend's device identifier. Under HTTPS it is the SHA-256 of the
    /// certificate; under HTTP it is a random string with no cryptographic
    /// meaning at all, which is why it is never treated as an identity here.
    /// </summary>
    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    /// <summary>"http" or "https".</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "https";

    [JsonPropertyName("download")]
    public bool Download { get; set; }

    /// <summary>
    /// True for an unsolicited announcement, false for a reply to one. A reply
    /// to a reply would loop forever.
    /// </summary>
    [JsonPropertyName("announce")]
    public bool Announce { get; set; }

    [JsonPropertyName("lsx")]
    public LocalSyncExtension? LocalSync { get; set; }
}

/// <summary>Native LocalSync capabilities, namespaced so LocalSend ignores them.</summary>
public sealed class LocalSyncExtension
{
    [JsonPropertyName("v")]
    public int Version { get; set; } = 1;

    /// <summary>Base32 device id: SHA-256 of the certificate's SubjectPublicKeyInfo.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The native listener's port, distinct from the compat port.</summary>
    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("caps")]
    public string[] Capabilities { get; set; } = [];
}
