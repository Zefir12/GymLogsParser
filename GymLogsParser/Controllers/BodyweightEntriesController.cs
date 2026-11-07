using System.Globalization;
using Core.Models;
using GymLogsParser.Extensions;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

/// <summary>
/// The signed-in user's bodyweight measurements (#46). They feed bodyweight and DOTS on
/// the progress charts and the bodyweight in the parsing prompt. Every action is scoped
/// to the caller's OIDC <c>sub</c>: another user's measurement is a 404, never a 403.
/// A measurement is identified by its date (<c>yyyy-MM-dd</c>), one per day.
/// </summary>
[ApiController]
[Authorize]
[Route("api/bodyweight-entries")]
public sealed class BodyweightEntriesController(IBodyweightService bodyweights) : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>The caller's measurements, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BodyweightEntry>>> List(CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }

        return Ok(await bodyweights.ListAsync(ownerId, cancellationToken));
    }

    /// <summary>Adds a measurement (<c>date</c>, <c>weightKg</c>). 409 when the caller already has one on that date.</summary>
    [HttpPost]
    public async Task<ActionResult<BodyweightEntry>> Create([FromBody] BodyweightEntry? request, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        if (Validate(request) is { } error) return BadRequest(new { error });

        var entry = new BodyweightEntry { Date = request!.Date, WeightKg = request.WeightKg };
        return await bodyweights.AddAsync(ownerId, entry, cancellationToken) switch
        {
            BodyweightWriteResult.Saved => Created($"/api/bodyweight-entries/{Format(entry.Date)}", entry),
            _ => DateTaken(entry.Date)
        };
    }

    /// <summary>
    /// Changes the caller's measurement on <paramref name="date"/>. The body may carry
    /// another date to move it there; 409 when the caller already has one on that date.
    /// </summary>
    [HttpPut("{date}")]
    public async Task<ActionResult<BodyweightEntry>> Update(string date, [FromBody] BodyweightEntry? request, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        if (ParseDate(date) is not { } existingDate) return BadDate();
        if (Validate(request) is { } error) return BadRequest(new { error });

        var entry = new BodyweightEntry { Date = request!.Date, WeightKg = request.WeightKg };
        return await bodyweights.UpdateAsync(ownerId, existingDate, entry, cancellationToken) switch
        {
            BodyweightWriteResult.Saved => Ok(entry),
            BodyweightWriteResult.NotFound => NotFoundOn(existingDate),
            _ => DateTaken(entry.Date)
        };
    }

    /// <summary>Deletes the caller's measurement on <paramref name="date"/>.</summary>
    [HttpDelete("{date}")]
    public async Task<IActionResult> Delete(string date, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        if (ParseDate(date) is not { } existingDate) return BadDate();

        return await bodyweights.DeleteAsync(ownerId, existingDate, cancellationToken)
            ? NoContent()
            : NotFoundOn(existingDate);
    }

    private static string? Validate(BodyweightEntry? request)
    {
        if (request is null) return "Send a measurement as { \"date\": \"yyyy-MM-dd\", \"weightKg\": 82.5 }.";

        var latest = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        if (request.Date < BodyweightEntry.MinDate || request.Date > latest)
        {
            return $"The date must be between {Format(BodyweightEntry.MinDate)} and today, as yyyy-MM-dd.";
        }

        if (request.WeightKg < BodyweightEntry.MinKg || request.WeightKg > BodyweightEntry.MaxKg)
        {
            return $"The weight must be between {BodyweightEntry.MinKg} and {BodyweightEntry.MaxKg} kg.";
        }

        if (decimal.Round(request.WeightKg, 2) != request.WeightKg)
        {
            return "The weight may have at most two decimals, for example 82.45.";
        }

        return null;
    }

    private static DateOnly? ParseDate(string value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    private BadRequestObjectResult BadDate() =>
        BadRequest(new { error = "The date in the URL must be yyyy-MM-dd, for example 2026-11-07." });

    private NotFoundObjectResult NotFoundOn(DateOnly date) =>
        NotFound(new { error = $"You have no measurement on {Format(date)}. List them with GET /api/bodyweight-entries." });

    private ConflictObjectResult DateTaken(DateOnly date) =>
        Conflict(new { error = $"You already have a measurement on {Format(date)}. Edit that one or pick another date." });
}
