using System.Text.Json.Serialization;
using LocalSync.Protocol.Discovery;
using LocalSync.Protocol.Transfer;

namespace LocalSync.Protocol;

/// <summary>
/// Source-generated serialisation for every type that crosses the wire.
/// </summary>
/// <remarks>
/// Mandatory rather than an optimisation: reflection-based
/// <c>JsonSerializer</c> overloads are annotated RequiresUnreferencedCode and
/// RequiresDynamicCode, so under NativeAOT they either fail to publish or fail
/// at runtime once trimming has removed the types.
/// <para>
/// No naming policy is configured. Every DTO member declares its own
/// <c>[JsonPropertyName]</c>, because LocalSend's contract is not ours to
/// rename.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Announcement))]
[JsonSerializable(typeof(LocalSyncExtension))]
[JsonSerializable(typeof(CreateSessionRequest))]
[JsonSerializable(typeof(CreateSessionResponse))]
[JsonSerializable(typeof(ChunkAcceptedResponse))]
[JsonSerializable(typeof(TransferView))]
[JsonSerializable(typeof(TransferView[]))]
[JsonSerializable(typeof(PeerView))]
[JsonSerializable(typeof(PeerView[]))]
[JsonSerializable(typeof(DaemonInfo))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class LocalSyncJsonContext : JsonSerializerContext;
