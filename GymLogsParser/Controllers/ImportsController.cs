using System.Text.RegularExpressions;
using Core.Models;
using GymLogsParser.Extensions;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

/// <summary>
/// Imports workout history from other apps into the signed-in user's log (#77).
/// Everything is scoped to the caller's OIDC <c>sub</c>: duplicates are only checked
/// against, and workouts only written to, their own log.
/// </summary>
[ApiController]
[Authorize]
[Route("api/imports")]
public sealed partial class ImportsController(IHevyImportService hevy) : ControllerBase
{
    /// <summary>10 million characters is far more than years of Hevy history (2,000 sets ≈ 250 kB).</summary>
    public const int MaxCsvLength = 10_000_000;

    public const int MaxMappings = 1000;

    /// <summary>
    /// Dry run of <see cref="ImportHevy"/>: parses the file and answers what would be
    /// imported (new and duplicate workouts, sets, bad rows) and every exercise title with
    /// its automatic or requested catalog match. Saves nothing.
    /// </summary>
    [HttpPost("hevy/preview")]
    [RequestSizeLimit(32_000_000)]
    public Task<ActionResult<HevyImportSummary>> PreviewHevy([FromBody] HevyImportRequest? request, CancellationToken cancellationToken) =>
        Run(request, dryRun: true, cancellationToken);

    /// <summary>
    /// Imports a Hevy CSV export (<c>csv</c>: the file's text; <c>mappings</c>: Hevy
    /// exercise title → catalog ID, null to skip). Bad rows are reported and skipped,
    /// workouts the caller already has (same date, start time and title) are skipped,
    /// and the rest is saved in one transaction.
    /// </summary>
    [HttpPost("hevy")]
    [RequestSizeLimit(32_000_000)]
    public Task<ActionResult<HevyImportSummary>> ImportHevy([FromBody] HevyImportRequest? request, CancellationToken cancellationToken) =>
        Run(request, dryRun: false, cancellationToken);

    private async Task<ActionResult<HevyImportSummary>> Run(HevyImportRequest? request, bool dryRun, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        if (Validate(request) is { } error) return BadRequest(new { error });

        var outcome = await hevy.ImportAsync(ownerId, request!.Csv!, request.Mappings ?? new Dictionary<string, string?>(), dryRun, cancellationToken);
        if (outcome.Error is not null) return BadRequest(new { error = outcome.Error });

        return Ok(outcome.Summary);
    }

    private static string? Validate(HevyImportRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Csv))
        {
            return "Send the Hevy export as { \"csv\": \"<file contents>\" }.";
        }

        if (request.Csv.Length > MaxCsvLength)
        {
            return $"The file is larger than {MaxCsvLength / 1_000_000} MB. Split the export into smaller files.";
        }

        if (request.Mappings is { } mappings)
        {
            if (mappings.Count > MaxMappings) return $"Send at most {MaxMappings} exercise mappings.";

            foreach (var (title, id) in mappings)
            {
                if (title.Length > HevyCsv.MaxTitleLength)
                {
                    return $"Exercise titles in mappings can be at most {HevyCsv.MaxTitleLength} characters.";
                }

                if (!string.IsNullOrEmpty(id) && (id.Length > 100 || !IdPattern().IsMatch(id)))
                {
                    return "Map each exercise title to a kebab-case catalog ID (like \"barbell-bench-press\"), or to null to skip it.";
                }
            }
        }

        return null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();
}
