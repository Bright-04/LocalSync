using LocalSync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LocalSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncManager _syncManager;

    public SyncController(ISyncManager syncManager)
    {
        _syncManager = syncManager;
    }

    [HttpPost("start")]
    public IActionResult StartSync([FromBody] SyncRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FolderPath)) return BadRequest("Invalid folder path");
        
        _syncManager.StartSync(request.FolderPath, request.TargetDeviceId);
        return Ok();
    }

    [HttpPost("stop")]
    public IActionResult StopSync([FromBody] SyncRequest request)
    {
        _syncManager.StopSync(request.FolderPath);
        return Ok();
    }

    [HttpGet("active")]
    public IActionResult GetActiveSyncs()
    {
        return Ok(_syncManager.GetActiveSyncs());
    }
}

public class SyncRequest
{
    public string FolderPath { get; set; } = string.Empty;
    public Guid TargetDeviceId { get; set; }
}
