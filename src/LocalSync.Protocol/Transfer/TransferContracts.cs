using System.Text.Json.Serialization;

namespace LocalSync.Protocol.Transfer;

/// <summary>Request to open a transfer session.</summary>
public sealed class CreateSessionRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("targetDeviceId")]
    public string? TargetDeviceId { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}

public sealed class CreateSessionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}

public sealed class ChunkAcceptedResponse
{
    [JsonPropertyName("transferredSize")]
    public long TransferredSize { get; set; }
}

/// <summary>A transfer as rendered to a client.</summary>
public sealed class TransferView
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("transferredSize")]
    public long TransferredSize { get; set; }

    /// <summary>
    /// The state name, never its ordinal. Serialising the enum as an integer
    /// makes adding a state a silent breaking change for every client.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("targetDeviceId")]
    public string? TargetDeviceId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public sealed class PeerView
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; } = string.Empty;

    [JsonPropertyName("mnemonic")]
    public string Mnemonic { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = "desktop";

    /// <summary>"LocalSync" or "LocalSendCompat" - drives the trust badge in the UI.</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("lastSeen")]
    public DateTimeOffset LastSeen { get; set; }
}

/// <summary>Identity and capability summary for this daemon.</summary>
public sealed class DaemonInfo
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; } = string.Empty;

    [JsonPropertyName("mnemonic")]
    public string Mnemonic { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("receiveDirectory")]
    public string ReceiveDirectory { get; set; } = string.Empty;
}

public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
