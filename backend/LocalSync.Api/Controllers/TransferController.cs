using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace LocalSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransferController : ControllerBase
{
    private readonly ITransferManager _transferManager;
    private readonly ILogger<TransferController> _logger;
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "LocalSync");

    public TransferController(ITransferManager transferManager, ILogger<TransferController> logger)
    {
        _transferManager = transferManager;
        _logger = logger;
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    [HttpPost("session")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var filePath = Path.Combine(_storagePath, request.FileName);
        if (System.IO.File.Exists(filePath))
        {
            bool isIdentical = false;
            
            if (request.LastModified.HasValue)
            {
                var localLastWrite = System.IO.File.GetLastWriteTimeUtc(filePath);
                if (request.LastModified.Value <= localLastWrite)
                {
                    isIdentical = true;
                }
            }

            if (!string.IsNullOrEmpty(request.FileHash))
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                using var stream = System.IO.File.OpenRead(filePath);
                var hashBytes = md5.ComputeHash(stream);
                var localHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                
                if (localHash == request.FileHash) isIdentical = true;
            }

            if (isIdentical)
            {
                _logger.LogInformation("Conflict: Local file {FileName} is identical or newer.", request.FileName);
                return Conflict(new { Message = "File is identical or newer." });
            }
        }

        var session = await _transferManager.CreateSessionAsync(request.TargetDeviceId, request.FileName, request.TotalSize);
        return Ok(session);
    }

    [HttpGet("session/{id}")]
    public IActionResult GetSession(Guid id)
    {
        var session = _transferManager.GetSession(id);
        if (session == null) return NotFound();
        return Ok(session);
    }

    [HttpGet("sessions")]
    public IActionResult GetAllSessions()
    {
        return Ok(_transferManager.GetAllSessions());
    }

    [HttpPost("chunk")]
    public async Task<IActionResult> UploadChunk([FromForm] Guid sessionId, [FromForm] long offset, [FromForm] bool isLastChunk, IFormFile chunk)
    {
        if (chunk == null || chunk.Length == 0) return BadRequest("Empty chunk");

        var session = _transferManager.GetSession(sessionId);
        if (session == null) return NotFound("Session not found");

        var filePath = Path.Combine(_storagePath, $"{session.Id}_{session.FileName}");

        try
        {
            await using var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            stream.Seek(offset, SeekOrigin.Begin);
            await chunk.CopyToAsync(stream);
            
            await _transferManager.UpdateProgressAsync(sessionId, chunk.Length);

            if (session.State == TransferState.Pending)
            {
                await _transferManager.UpdateSessionStateAsync(sessionId, TransferState.Active);
            }

            return Ok(new { TransferredSize = session.TransferredSize });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write chunk for session {SessionId}", sessionId);
            await _transferManager.UpdateSessionStateAsync(sessionId, TransferState.Failed);
            return StatusCode(500, "Failed to write chunk");
        }
    }
}

public class CreateSessionRequest
{
    public Guid TargetDeviceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public DateTime? LastModified { get; set; }
    public string? FileHash { get; set; }
}
