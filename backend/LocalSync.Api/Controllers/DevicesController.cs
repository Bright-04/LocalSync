using LocalSync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LocalSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceManager _deviceManager;

    public DevicesController(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    [HttpGet]
    public IActionResult GetDevices()
    {
        return Ok(_deviceManager.GetDevices());
    }
}
