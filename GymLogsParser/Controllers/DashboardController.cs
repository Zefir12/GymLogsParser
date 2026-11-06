using Core.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// Per-day, per-exercise working-set aggregates for the last 53 calendar weeks,
    /// ending with the week of <paramref name="to"/> (the client's local today;
    /// defaults to the server's today).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardData>> Get([FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var data = await dashboardService.GetAsync(to ?? DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        return Ok(data);
    }
}
