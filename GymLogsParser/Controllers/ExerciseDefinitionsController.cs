using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

/// <summary>
/// The exercise catalog: what the workout editor's picker offers and what the
/// parsing prompt lists as allowed IDs (with aliases).
/// </summary>
[ApiController]
[Route("api/exercises/definitions")]
public sealed partial class ExerciseDefinitionsController(IExerciseCatalogService catalog) : ControllerBase
{
    private const int MaxAliases = 30;
    private const int MaxAliasLength = 100;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExerciseDefinition>>> List(CancellationToken cancellationToken)
    {
        var snapshot = await catalog.GetAsync(cancellationToken);
        return Ok(snapshot.All);
    }

    /// <summary>
    /// Adds an exercise. <c>id</c> is optional: when empty it is derived from the name
    /// ("Smith Machine Squat" becomes "smith-machine-squat").
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ExerciseDefinition>> Create([FromBody] ExerciseDefinition? request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { error = "Exercise is required." });

        var definition = Normalize(request);
        if (string.IsNullOrEmpty(definition.Id)) definition.Id = Slugify(definition.Name);

        var snapshot = await catalog.GetAsync(cancellationToken);
        var error = Validate(definition, snapshot, existingId: null);
        if (error is not null) return BadRequest(new { error });

        if (snapshot.ById.ContainsKey(definition.Id))
        {
            return Conflict(new { error = $"An exercise with the ID \"{definition.Id}\" already exists." });
        }

        var created = await catalog.CreateAsync(definition, cancellationToken);
        return Created($"/api/exercises/definitions/{Uri.EscapeDataString(created.Id)}", created);
    }

    /// <summary>
    /// Updates an exercise's name, category, muscle group, barbell flag, aliases and
    /// muscle shares.
    /// The ID never changes: logged workouts refer to it.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ExerciseDefinition>> Update(string id, [FromBody] ExerciseDefinition? request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { error = "Exercise is required." });

        var snapshot = await catalog.GetAsync(cancellationToken);
        if (!snapshot.ById.TryGetValue(id, out var existing)) return NotFound();

        var definition = Normalize(request);
        definition.Id = existing.Id;

        var error = Validate(definition, snapshot, existingId: existing.Id);
        if (error is not null) return BadRequest(new { error });

        var updated = await catalog.UpdateAsync(existing.Id, definition, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    private static ExerciseDefinition Normalize(ExerciseDefinition request)
    {
        var muscleGroup = CanonicalOrTyped(request.MuscleGroup);

        return new ExerciseDefinition
        {
            Id = (request.Id ?? string.Empty).Trim().ToLowerInvariant(),
            Name = CollapseWhitespace(request.Name),
            Category = request.Category,
            MuscleGroup = muscleGroup,
            Barbell = request.Barbell,
            Aliases = (request.Aliases ?? [])
                .Select(CollapseWhitespace)
                .Where(a => a.Length > 0)
                .DistinctBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            // The primary always counts 1, so an entry for it is dropped; Validate checks the rest.
            Muscles = (request.Muscles ?? [])
                .Where(m => m is not null)
                .Select(m => new MuscleShare { MuscleGroup = CanonicalOrTyped(m.MuscleGroup), Share = Math.Round(m.Share, 2) })
                .Where(m => m.MuscleGroup.Length > 0 && m.MuscleGroup != muscleGroup)
                .ToList()
        };
    }

    /// <summary>Canonical spelling when it is a known group; otherwise kept as typed so Validate can reject it.</summary>
    private static string CanonicalOrTyped(string? group) =>
        MuscleGroups.Canonical(CollapseWhitespace(group)) ?? CollapseWhitespace(group);

    private static string? Validate(ExerciseDefinition definition, ExerciseCatalogSnapshot snapshot, string? existingId)
    {
        if (definition.Name.Length == 0) return "Name is required.";
        if (definition.Name.Length > 200) return "Name must be at most 200 characters.";
        if (definition.MuscleGroup.Length == 0) return "Muscle group is required.";
        if (!MuscleGroups.All.Contains(definition.MuscleGroup)) return $"Muscle group must be one of: {string.Join(", ", MuscleGroups.All)}.";
        if (!Enum.IsDefined(definition.Category)) return "Category must be Strength or Cardio.";

        var musclesError = ValidateMuscles(definition);
        if (musclesError is not null) return musclesError;

        if (definition.Id.Length is 0 or > 100 || !IdPattern().IsMatch(definition.Id))
        {
            return "ID must be lowercase letters and digits separated by single hyphens (e.g. \"smith-machine-squat\"), at most 100 characters.";
        }

        if (definition.Aliases.Count > MaxAliases) return $"At most {MaxAliases} aliases per exercise.";

        foreach (var alias in definition.Aliases)
        {
            if (alias.Length > MaxAliasLength) return $"Alias \"{alias}\" is longer than {MaxAliasLength} characters.";
            // Aliases are quoted inside the parsing prompt.
            if (alias.Contains('"')) return $"Alias \"{alias}\" must not contain double quotes.";

            var clash = snapshot.All.FirstOrDefault(e =>
                !string.Equals(e.Id, existingId, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(e.Name, alias, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(e.Id, alias, StringComparison.OrdinalIgnoreCase) ||
                 e.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase)));

            if (clash is not null) return $"Alias \"{alias}\" already names \"{clash.Name}\".";
        }

        return null;
    }

    /// <summary>Secondary muscles (the primary was already dropped by Normalize).</summary>
    private static string? ValidateMuscles(ExerciseDefinition definition)
    {
        if (definition.Muscles.Count == 0) return null;

        if (definition.Category == ExerciseCategory.Cardio || !MuscleGroups.IsMuscle(definition.MuscleGroup))
        {
            return "Cardio and warm-up exercises can't count towards other muscle groups.";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var muscle in definition.Muscles)
        {
            if (!MuscleGroups.IsMuscle(muscle.MuscleGroup))
            {
                return $"\"{muscle.MuscleGroup}\" can't be a secondary muscle group. Use one of: {string.Join(", ", MuscleGroups.All.Where(MuscleGroups.IsMuscle))}.";
            }

            if (!seen.Add(muscle.MuscleGroup)) return $"{muscle.MuscleGroup} is listed more than once.";
            if (muscle.Share is <= 0m or > 1m) return $"The share of {muscle.MuscleGroup} must be more than 0 and at most 1.";
        }

        return null;
    }

    private static string CollapseWhitespace(string? value) =>
        WhitespacePattern().Replace(value ?? string.Empty, " ").Trim();

    /// <summary>"Smith Machine Squat (wide)" -> "smith-machine-squat-wide"; strips diacritics, "ł" -> "l".</summary>
    private static string Slugify(string name)
    {
        var decomposed = name.ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        }

        var slug = HyphenRunPattern().Replace(builder.ToString(), "-").Trim('-');
        return slug.Length > 100 ? slug[..100].TrimEnd('-') : slug;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex("-{2,}")]
    private static partial Regex HyphenRunPattern();
}
