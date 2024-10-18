using DialogportenAdapter.Models;
using DialogportenAdapter.Services;
using Microsoft.AspNetCore.Mvc;

namespace DialogportenAdapter.Controllers;

[Route("dialpgportensync/api/v1")]
[ApiController]
public class InstanceSyncController : Controller
{
    private readonly IInstanceSyncService _instanceSyncService;

    public InstanceSyncController(IInstanceSyncService instanceSyncService)
    {
        _instanceSyncService = instanceSyncService;
    }

    [HttpPost("synchronize")]
    public IActionResult Synchronize([FromBody] SynchronizationRequest request)
    {
        return View();
    }
}
