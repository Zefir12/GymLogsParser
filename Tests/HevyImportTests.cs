using Core.Models;
using GymLogsParser.Controllers;
using Infrastructure.Helpers;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Tests;

/// <summary>
/// #77: importing a Hevy CSV export. Fixtures/hevy-sample.csv is a small made-up export
/// in Hevy's format: three workouts, a warm-up, a drop set, a to-failure set, a quoted
/// note with a comma, quotes and a line break, an exercise the catalog doesn't know, a
/// cardio row and three bad rows (lines 5, 18 and 19).
/// </summary>
public sealed class HevyImportTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    private static readonly string Sample =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "hevy-sample.csv"));

    [Fact]
    public void CsvReader_HandlesQuotesEscapedQuotesLineBreaksAndBom()
    {
        var records = CsvReader.Read("﻿a,\"b,c\",\"say \"\"hi\"\"\"\r\n\"multi\nline\",,x\n\n").ToList();

        Assert.Equal(2, records.Count);
        Assert.Equal(new[] { "a", "b,c", "say \"hi\"" }, records[0].Fields);
        Assert.Equal(1, records[0].Line);
        Assert.Equal(new[] { "multi\nline", "", "x" }, records[1].Fields);
        Assert.Equal(2, records[1].Line);
    }

    [Fact]
    public void CsvReader_UnclosedQuote_Throws()
    {
        Assert.Throws<FormatException>(() => CsvReader.Read("a,\"b\nc").ToList());
    }

    [Fact]
    public void Parse_ReportsBadRowsWithTheirLine_AndKeepsTheRest()
    {
        var result = HevyCsv.Parse(Sample);

        Assert.Equal(14, result.Rows.Count);
        Assert.Equal(new[] { 5, 18, 19 }, result.Errors.Select(e => e.Line));
        Assert.Contains("weight_kg", result.Errors[0].Error);

        var note = Assert.Single(result.Rows, r => r.ExerciseNotes is not null);
        Assert.Equal("Rope, then \"bar\"\nslow eccentric", note.ExerciseNotes);
        Assert.Equal(6, note.Line);

        var first = result.Rows[0];
        Assert.Equal("Evening workout 🏋️", first.Title);
        Assert.Equal(new DateTime(2026, 11, 6, 18, 48, 0), first.Start);
        Assert.Equal("warmup", first.SetType);
    }

    [Fact]
    public void Parse_NotAHevyExport_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => HevyCsv.Parse("date,exercise,weight\n2026-11-02,bench,100\n"));
        Assert.Contains("title", ex.Message);
    }

    [Theory]
    [InlineData("Bench Press (Barbell)", "Barbell Bench Press")]
    [InlineData("Pull Up", "Pull-Up")]
    [InlineData("Lying Leg Curl (Machine)", "Lying Leg Curl")]
    [InlineData("Seated Cable Row - Bar Grip", "Seated Cable Row")]
    public void MatchKeys_FindCatalogNames(string hevy, string catalog)
    {
        Assert.Contains(HevyCsv.NameKey(catalog), HevyCsv.MatchKeys(hevy));
    }

    [Fact]
    public void MatchKeys_KeepBarbellAndDumbbell()
    {
        Assert.DoesNotContain(HevyCsv.NameKey("Overhead Press"), HevyCsv.MatchKeys("Overhead Press (Dumbbell)"));
    }

    [Fact]
    public async Task Preview_SavesNothing_AndListsMatches()
    {
        var summary = await RunAsync(TestUsers.Alice, dryRun: true);

        Assert.False(summary.Imported);
        Assert.Equal(3, summary.WorkoutsInFile);
        Assert.Equal(3, summary.Workouts);
        Assert.Equal(11, summary.Sets);
        Assert.Equal(3, summary.UnmappedSets);
        Assert.Equal(3, summary.ErrorCount);

        var byTitle = summary.Exercises.ToDictionary(e => e.Title);
        Assert.Equal("barbell-bench-press", byTitle["Bench Press (Barbell)"].ExerciseId);
        Assert.True(byTitle["Bench Press (Barbell)"].AutoMatched);
        Assert.Equal(3, byTitle["Bench Press (Barbell)"].Sets);
        Assert.Equal("lying-leg-curl", byTitle["Lying Leg Curl (Machine)"].ExerciseId);
        Assert.Equal("pull-up", byTitle["Pull Up"].ExerciseId);
        Assert.Equal("seated-cable-row", byTitle["Seated Cable Row - Bar Grip"].ExerciseId);
        Assert.Equal("treadmill-running", byTitle["Treadmill Running"].ExerciseId);
        Assert.Null(byTitle["Triceps Pushdown"].ExerciseId);
        Assert.Null(byTitle["Mystery Machine Thing"].ExerciseId);

        await using var check = Database.CreateDbContext();
        Assert.False(await check.Workouts.AnyAsync(Ct));
    }

    [Fact]
    public async Task Import_StoresWorkoutsForTheCaller_WithMappings()
    {
        var mappings = new Dictionary<string, string?>
        {
            ["Triceps Pushdown"] = "cable-triceps-pushdown",
            ["Mystery Machine Thing"] = null
        };

        var summary = await RunAsync(TestUsers.Alice, dryRun: false, mappings);

        Assert.True(summary.Imported);
        Assert.Equal(3, summary.Workouts);
        Assert.Equal(13, summary.Sets);
        Assert.Equal(1, summary.UnmappedSets);
        Assert.False(summary.Exercises.Single(e => e.Title == "Triceps Pushdown").AutoMatched);

        await using var check = Database.CreateDbContext();
        var workouts = await check.Workouts
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Include(w => w.Exercises).ThenInclude(e => e.Cardio)
            .ToListAsync(Ct);
        Assert.All(workouts, w => Assert.Equal(TestUsers.Alice, w.OwnerId));

        var evening = Assert.Single(workouts, w => w.Date == new DateOnly(2026, 11, 6));
        Assert.Equal("Evening workout 🏋️", evening.Title);
        Assert.Equal(new TimeOnly(18, 48), evening.StartTime);
        Assert.Equal(new TimeOnly(19, 37), evening.EndTime);

        var bench = Assert.Single(evening.Exercises, e => e.ExerciseId == "barbell-bench-press");
        Assert.Equal("Barbell Bench Press", bench.Name);
        var sets = bench.Sets.OrderBy(s => s.SetNumber).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, sets.Select(s => s.SetNumber));
        Assert.Equal(new bool?[] { true, false, false }, sets.Select(s => s.Warmup));
        Assert.Equal(82.5m, sets[2].Weight);
        Assert.Equal(8m, sets[1].Rpe);

        var pushdown = Assert.Single(evening.Exercises, e => e.ExerciseId == "cable-triceps-pushdown");
        Assert.Equal("Rope, then \"bar\"\nslow eccentric", pushdown.Notes);
        Assert.Equal("Drop set", pushdown.Sets.Single(s => s.SetNumber == 2).Notes);

        var morning = Assert.Single(workouts, w => w.Date == new DateOnly(2026, 11, 4));
        Assert.Equal("Legs, easy", morning.Notes);
        Assert.Equal(2, morning.Exercises.Count);

        var treadmill = workouts.SelectMany(w => w.Exercises).Single(e => e.ExerciseId == "treadmill-running");
        Assert.Equal(ExerciseCategory.Cardio, treadmill.Category);
        var cardio = Assert.Single(treadmill.Cardio);
        Assert.Equal(600, cardio.DurationSeconds);
        Assert.Equal(1.5m, cardio.DistanceKm);
    }

    [Fact]
    public async Task Reimport_SkipsWorkoutsTheCallerAlreadyHas_ButNotOtherUsers()
    {
        await RunAsync(TestUsers.Alice, dryRun: false);

        var again = await RunAsync(TestUsers.Alice, dryRun: false);
        Assert.Equal(0, again.Workouts);
        Assert.Equal(3, again.DuplicateWorkouts);

        var bob = await RunAsync(TestUsers.Bob, dryRun: false);
        Assert.Equal(3, bob.Workouts);

        await using var check = Database.CreateDbContext();
        Assert.Equal(3, await check.Workouts.CountAsync(w => w.OwnerId == TestUsers.Alice, Ct));
        Assert.Equal(3, await check.Workouts.CountAsync(w => w.OwnerId == TestUsers.Bob, Ct));
    }

    [Fact]
    public async Task Import_MappingToUnknownId_IsBadRequest_AndSavesNothing()
    {
        await using (var context = Database.CreateDbContext())
        {
            var result = await Controller(context, TestUsers.Alice).ImportHevy(
                new HevyImportRequest { Csv = Sample, Mappings = new() { ["Triceps Pushdown"] = "no-such-exercise" } }, Ct);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        await using var check = Database.CreateDbContext();
        Assert.False(await check.Workouts.AnyAsync(Ct));
    }

    [Fact]
    public async Task Import_EmptyOrNonHevyFile_IsBadRequest()
    {
        await using var context = Database.CreateDbContext();
        var controller = Controller(context, TestUsers.Alice);

        Assert.IsType<BadRequestObjectResult>((await controller.ImportHevy(new HevyImportRequest { Csv = "" }, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.ImportHevy(new HevyImportRequest { Csv = "a,b\n1,2\n" }, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.ImportHevy(
            new HevyImportRequest { Csv = Sample, Mappings = new() { ["Pull Up"] = "Not Kebab" } }, Ct)).Result);
    }

    private async Task<HevyImportSummary> RunAsync(string user, bool dryRun, Dictionary<string, string?>? mappings = null)
    {
        await using var context = Database.CreateDbContext();
        var controller = Controller(context, user);
        var request = new HevyImportRequest { Csv = Sample, Mappings = mappings };
        var result = dryRun ? await controller.PreviewHevy(request, Ct) : await controller.ImportHevy(request, Ct);
        return Assert.IsType<HevyImportSummary>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static ImportsController Controller(Infrastructure.AppDbContext context, string user) =>
        new ImportsController(new HevyImportService(context, new ExerciseCatalogService(context, new MemoryCache(new MemoryCacheOptions()))))
            .As(user);
}
