using Core.Models;
using GymLogsParser.Extensions;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

/// <summary>
/// Weekly target ranges (working sets per week) per muscle group, shown on the
/// dashboard's weekly sets per body part. Cardio and Warm-up have no target.
/// Each user has their own ranges.
/// </summary>
[ApiController]
[Authorize]
[Route("api/muscle-targets")]
public sealed class MuscleTargetsController(IMuscleTargetService targets) : ControllerBase
{
    /// <summary>Every muscle group in display order, with its range and its default range.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MuscleTarget>>> List(CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }

        return Ok(await targets.GetAsync(ownerId, cancellationToken));
    }

    /// <summary>
    /// Sets the ranges of the listed groups (<c>muscleGroup</c>, <c>minSets</c>,
    /// <c>maxSets</c>); groups not listed keep theirs. "Reset to default" is a PUT
    /// of the default ranges from the GET response.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<MuscleTarget>>> Update([FromBody] List<MuscleTarget>? request, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        if (request is null) return BadRequest(new { error = "Targets are required." });

        var normalized = new List<MuscleTarget>(request.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in request)
        {
            if (item is null) return BadRequest(new { error = "Targets must not contain null entries." });

            var group = MuscleGroups.Canonical(item.MuscleGroup);
            if (group is null || !MuscleGroups.IsMuscle(group))
            {
                return BadRequest(new { error = $"\"{item.MuscleGroup}\" has no target. Use one of: {string.Join(", ", MuscleGroups.All.Where(MuscleGroups.IsMuscle))}." });
            }

            if (!seen.Add(group)) return BadRequest(new { error = $"{group} is listed more than once." });

            if (item.MinSets < 0 || item.MaxSets > MuscleTargetDefaults.MaxSetsLimit || item.MinSets > item.MaxSets)
            {
                return BadRequest(new { error = $"{group}: the range must be whole sets with 0 ≤ min ≤ max ≤ {MuscleTargetDefaults.MaxSetsLimit}." });
            }

            normalized.Add(new MuscleTarget { MuscleGroup = group, MinSets = item.MinSets, MaxSets = item.MaxSets });
        }

        return Ok(await targets.UpdateAsync(ownerId, normalized, cancellationToken));
    }
}
