using System.Globalization;
using System.Text;
using GymLogsParser.Extensions;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

/// <summary>
/// Exports the signed-in user's workouts to files other apps import (#80). Only the
/// caller's own workouts (OIDC <c>sub</c>) are exported.
/// </summary>
[ApiController]
[Authorize]
[Route("api/exports")]
public sealed class ExportsController(IHevyExportService hevy) : ControllerBase
{
    /// <summary>
    /// Largest file the Hevy imports take: ours and embr's both refuse more than
    /// 10,000,000 characters, and embr's UI checks the file's bytes against the same
    /// number, so the export is held to 10,000,000 UTF-8 bytes.
    /// </summary>
    public const int MaxBytes = ImportsController.MaxCsvLength;

    /// <summary>
    /// The caller's workouts as a Hevy workout CSV (UTF-8, no BOM), oldest first, for
    /// embr's Hevy import (Training → Import) or ours. <c>from</c>/<c>to</c>
    /// (<c>yyyy-MM-dd</c>, inclusive, both optional) limit the dates. 404 when nothing
    /// in the range has a set to export; 400 when the file would be larger than the
    /// imports accept (10 MB or 100,000 sets), so a shorter range is needed.
    /// </summary>
    [HttpGet("hevy")]
    public async Task<IActionResult> Hevy([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }

        if (from is { } f && to is { } t && f > t)
        {
            return BadRequest(new { error = "The from date is after the to date. Send from on or before to (yyyy-MM-dd)." });
        }

        var export = await hevy.ExportAsync(ownerId, from, to, cancellationToken);
        if (export.Rows == 0)
        {
            return NotFound(new { error = "You have no workouts with sets in that date range. Pick another range, or leave from and to empty to export everything." });
        }

        var bytes = Encoding.UTF8.GetBytes(export.Csv);
        if (export.Rows > HevyCsv.MaxRows || bytes.Length > MaxBytes)
        {
            return BadRequest(new
            {
                error = string.Create(CultureInfo.InvariantCulture, $"The export has {export.Rows:N0} sets ({bytes.Length / 1_000_000.0:0.#} MB); Hevy imports take at most {HevyCsv.MaxRows:N0} sets and {MaxBytes / 1_000_000} MB per file. Pick a shorter date range (from and to) and export it in parts.")
            });
        }

        var name = $"gymlogs-hevy-{DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";
        return File(bytes, "text/csv; charset=utf-8", name);
    }
}
