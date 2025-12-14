namespace LocalSync.Core.Models;

public class FileChunk
{
    public Guid SessionId { get; set; }
    public long Offset { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public bool IsLastChunk { get; set; }
}
