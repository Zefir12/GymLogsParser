using Core.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// Per-day, per-exercise working-set aggregates for whole calendar weeks ending with
    /// the week of <paramref name="to"/> (the client's local today; defaults to the
    /// server's today). Always at least 53 weeks; <paramref name="range"/> (3M, 6M, 1Y,
    /// 5Y or 10Y, default 1Y) widens the window when it needs more history.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardData>> Get(
        [FromQuery] DateOnly? to,
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        range = string.IsNullOrWhiteSpace(range) ? DashboardRange.Default : range.Trim();
        if (!DashboardRange.TryGetDays(range, out var days))
        {
            ModelState.AddModelError(nameof(range), $"Unknown range '{range}'. Use one of: {string.Join(", ", DashboardRange.All)}.");
            return ValidationProblem(ModelState);
        }

        var data = await dashboardService.GetAsync(to ?? DateOnly.FromDateTime(DateTime.Today), days, cancellationToken);
        return Ok(data);
    }
}
