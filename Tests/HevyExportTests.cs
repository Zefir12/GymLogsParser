using System.Text;
using Core.Models;
using GymLogsParser.Controllers;
using Infrastructure.Entities;
using Infrastructure.Helpers;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Tests;

/// <summary>
/// #80: exporting workouts as a Hevy CSV. The file must import with the Hevy import
/// (ours, and embr's, which uses the same parser) with no bad rows and give back the
/// same workouts.
/// </summary>
public sealed class HevyExportTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    private static readonly string Sample =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "hevy-sample.csv"));

    private static readonly Dictionary<string, string?> SampleMappings = new()
    {
        ["Triceps Pushdown"] = "cable-triceps-pushdown",
        ["Mystery Machine Thing"] = null
    };

    [Fact]
    public async Task Export_ThenImport_RoundTrips()
    {
        await ImportAsync(TestUsers.Alice, Sample, SampleMappings, dryRun: false);

        var csv = await ExportAsync(TestUsers.Alice);
        Assert.Equal(Sample.Split('\n')[0].TrimEnd('\r'), csv.Split('\n')[0]);
        Assert.Empty(HevyCsv.Parse(csv).Errors);

        // Exercise titles are catalog names, so everything matches without mappings.
        var preview = await ImportAsync(TestUsers.Bob, csv, null, dryRun: true);
        Assert.Equal(0, preview.ErrorCount);
        Assert.Equal(0, preview.UnmappedSets);
        Assert.Equal(3, preview.WorkoutsInFile);
        Assert.Equal(13, preview.Sets);
        Assert.All(preview.Exercises, e => Assert.True(e.AutoMatched));

        var imported = await ImportAsync(TestUsers.Bob, csv, null, dryRun: false);
        Assert.Equal(3, imported.Workouts);

        Assert.Equal(await SnapshotAsync(TestUsers.Alice), await SnapshotAsync(TestUsers.Bob));
    }

    [Fact]
    public async Task Export_OnlyHasTheCallersWorkouts_AndHonoursTheRange()
    {
        await ImportAsync(TestUsers.Alice, Sample, SampleMappings, dryRun: false);

        await using var context = Database.CreateDbContext();
        Assert.IsType<NotFoundObjectResult>(await Controller(context, TestUsers.Bob).Hevy(null, null, Ct));
        Assert.IsType<BadRequestObjectResult>(await Controller(context, TestUsers.Alice).Hevy(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 1), Ct));

        var csv = await ExportAsync(TestUsers.Alice, new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 6));
        var rows = HevyCsv.Parse(csv).Rows;
        Assert.Equal(new[] { new DateTime(2026, 11, 4, 7, 15, 0), new DateTime(2026, 11, 6, 18, 48, 0) }, rows.Select(r => r.Start).Distinct());
    }

    [Fact]
    public void Writer_KeepsEveryRowImportable()
    {
        var day = new DateOnly(2026, 11, 10);
        Exercise Bench(params Set[] sets) => new() { ExerciseId = "barbell-bench-press", Name = "Barbell Bench Press", Sets = [.. sets] };

        var workouts = new[]
        {
            // No title, no start time: twice on one day, so the second gets " (2)".
            new Workout { Date = day, Exercises = [Bench(new Set { SetNumber = 1, Weight = 50, Reps = 5 })] },
            new Workout { Date = day, Title = "  ", Exercises = [Bench(new Set { SetNumber = 1, Weight = 60, Reps = 5 })] },
            new Workout
            {
                Date = day,
                Title = "Late \"night\", session",
                Notes = "line one\nline two",
                StartTime = new TimeOnly(23, 30, 15),
                EndTime = new TimeOnly(0, 40),
                Exercises =
                [
                    Bench(
                        new Set { SetNumber = 2, Weight = 2000, Reps = 5, Rpe = 11, Notes = "To failure" },
                        new Set { SetNumber = 1, Weight = 100.50m, Reps = 3, Rpe = 9.5m, Warmup = true },
                        new Set { SetNumber = 3, Notes = "just a note" },
                        new Set { SetNumber = 4, Notes = "Drop set, 45 s" }),
                    new Exercise
                    {
                        ExerciseId = "treadmill-running",
                        Name = "Treadmill Running",
                        Category = ExerciseCategory.Cardio,
                        Cardio = [new Cardio { DurationSeconds = 600, DistanceKm = 1.5m }, new Cardio { DurationSeconds = 100_000 }]
                    }
                ]
            },
            new Workout { Title = "No date", Exercises = [Bench(new Set { SetNumber = 1, Weight = 70, Reps = 5 })] }
        };

        var export = HevyCsvWriter.Write(workouts, e => e.Name);

        Assert.Equal(3, export.Workouts);
        Assert.Equal(6, export.Rows);
        Assert.Equal(2, export.SkippedSets);

        var parsed = HevyCsv.Parse(export.Csv);
        Assert.Empty(parsed.Errors);
        Assert.Equal(6, parsed.Rows.Count);
        Assert.Equal(new[] { "Workout", "Workout (2)" }, parsed.Rows.Take(2).Select(r => r.Title));
        Assert.All(parsed.Rows.Take(2), r => Assert.Equal(day.ToDateTime(TimeOnly.MinValue), r.Start));

        var late = parsed.Rows.Skip(2).ToList();
        Assert.All(late, r => Assert.Equal("Late \"night\", session", r.Title));
        Assert.Equal(new DateTime(2026, 11, 10, 23, 30, 0), late[0].Start);
        Assert.Equal(new DateTime(2026, 11, 11, 0, 40, 0), late[0].End);
        Assert.Equal("line one\nline two", late[0].Description);

        Assert.Equal("warmup", late[0].SetType);
        Assert.Equal(100.5m, late[0].WeightKg);
        Assert.Equal(3, late[0].Reps);
        Assert.Equal(9.5m, late[0].Rpe);

        // Out of range: the weight and RPE are dropped, the reps keep the set.
        Assert.Equal("failure", late[1].SetType);
        Assert.Null(late[1].WeightKg);
        Assert.Equal(5, late[1].Reps);
        Assert.Null(late[1].Rpe);

        Assert.Equal("dropset", late[2].SetType);
        Assert.Equal(45, late[2].DurationSeconds);

        Assert.Equal("Treadmill Running", late[3].ExerciseTitle);
        Assert.Equal(1.5m, late[3].DistanceKm);
        Assert.Equal(600, late[3].DurationSeconds);
    }

    /// <summary>The user's workouts in a form that compares by value, ignoring IDs and exercise order.</summary>
    private async Task<List<string>> SnapshotAsync(string owner)
    {
        await using var context = Database.CreateDbContext();
        var workouts = await context.Workouts
            .AsNoTracking()
            .Where(w => w.OwnerId == owner)
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Include(w => w.Exercises).ThenInclude(e => e.Cardio)
            .ToListAsync(Ct);

        static string N(decimal? d) => d?.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture) ?? "-";

        return workouts
            .Select(w =>
            {
                var text = new StringBuilder($"{w.Date} {w.StartTime}-{w.EndTime} {w.Title} [{w.Notes}]");
                foreach (var e in w.Exercises.OrderBy(e => e.ExerciseId))
                {
                    text.Append($"\n  {e.ExerciseId} {e.Name} {e.Category} [{e.Notes}]");
                    foreach (var s in e.Sets.OrderBy(s => s.SetNumber))
                    {
                        text.Append($"\n    {s.SetNumber} {N(s.Weight)}x{s.Reps} rpe {N(s.Rpe)} warmup {s.Warmup} [{s.Notes}]");
                    }

                    foreach (var c in e.Cardio)
                    {
                        text.Append($"\n    {c.Activity} {c.DurationSeconds} s {N(c.DistanceKm)} km [{c.Notes}]");
                    }
                }

                return text.ToString();
            })
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<string> ExportAsync(string user, DateOnly? from = null, DateOnly? to = null)
    {
        await using var context = Database.CreateDbContext();
        var file = Assert.IsType<FileContentResult>(await Controller(context, user).Hevy(from, to, Ct));
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.EndsWith(".csv", file.FileDownloadName);
        Assert.False(file.FileContents.AsSpan().StartsWith(Encoding.UTF8.Preamble), "no BOM");
        return Encoding.UTF8.GetString(file.FileContents);
    }

    private async Task<HevyImportSummary> ImportAsync(string user, string csv, Dictionary<string, string?>? mappings, bool dryRun)
    {
        await using var context = Database.CreateDbContext();
        var controller = new ImportsController(new HevyImportService(context, Catalog(context))).As(user);
        var request = new HevyImportRequest { Csv = csv, Mappings = mappings };
        var result = dryRun ? await controller.PreviewHevy(request, Ct) : await controller.ImportHevy(request, Ct);
        return Assert.IsType<HevyImportSummary>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static ExportsController Controller(Infrastructure.AppDbContext context, string user) =>
        new ExportsController(new HevyExportService(context, Catalog(context))).As(user);

    private static ExerciseCatalogService Catalog(Infrastructure.AppDbContext context) =>
        new(context, new MemoryCache(new MemoryCacheOptions()));
}
