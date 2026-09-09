using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.Models;
using GymLog.Api.AI;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class DeepSeekService(HttpClient httpClient, IOptions<DeepSeekOptions> options, IMemoryCache cache, ILogger<DeepSeekService> logger) : IDeepSeekService
{
    private readonly DeepSeekOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private static readonly IReadOnlyDictionary<string, ExerciseDefinition> CatalogById = ExerciseCatalog.All.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
    private static readonly string SystemPrompt = SystemPromptTemplate.Replace("{{EXERCISE_ID_GROUPS}}", BuildExerciseIdGroups());

    private static string BuildExerciseIdGroups()
    {
        var groups = ExerciseCatalog.All
            .GroupBy(e => e.MuscleGroup)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(e => e.Id))}");

        return string.Join("\n", groups);
    }
    
    private static readonly BodyweightPoint[] BodyweightHistory = 
        [
            new("2018-12-17", 66.0), new("2019-02-04", 66.4), new("2019-04-03", 67.0), new("2019-05-20", 65.1), new("2019-08-02", 65.3), new("2019-09-02", 66.1), new("2019-10-19", 67.0), new("2019-12-21", 65.0),
            new("2020-07-26", 67.0), new("2022-08-01", 70.05), new("2022-08-18", 69.6), new("2023-08-01", 70.4), new("2023-08-10", 71.6), new("2023-08-14", 72.2), new("2023-08-20", 72.0), new("2023-08-28", 73.8),
            new("2023-09-03", 74.5), new("2023-09-08", 75.4), new("2023-09-13", 75.9), new("2023-09-18", 76.0), new("2023-09-23", 76.4), new("2023-09-29", 76.9), new("2023-10-04", 75.0), new("2023-10-12", 74.6),
            new("2023-10-20", 74.5), new("2023-11-29", 73.0), new("2024-01-11", 72.4), new("2024-02-08", 72.9), new("2024-02-22", 75.5), new("2024-03-13", 73.3), new("2024-04-17", 75.7), new("2024-05-23", 78.2),
            new("2024-06-19", 77.1), new("2024-07-06", 78.8), new("2024-08-11", 78.5), new("2024-09-20", 81.2), new("2024-10-16", 82.7), new("2024-11-25", 82.3), new("2024-12-30", 84.4), new("2025-01-26", 85.3),
            new("2025-02-19", 86.1), new("2025-03-15", 84.9), new("2025-04-17", 85.7), new("2025-05-30", 87.7), new("2025-06-14", 87.0), new("2025-07-19", 86.7), new("2025-08-14", 84.6), new("2025-09-03", 83.7),
            new("2025-10-27", 80.7), new("2025-12-17", 84.4), new("2026-01-21", 87.4), new("2026-03-23", 89.1), new("2026-05-04", 90.1), new("2026-07-08", 91.9), new("2026-07-23", 91.7), new("2026-08-06", 91.0)
        ];
    
    private const string SystemPromptTemplate = """
    You are a highly accurate gym workout log parser.

    Your task is to convert a messy personal gym log into structured JSON.

    The user writes mostly in Polish, English, abbreviations, typos, missing punctuation,
    and shorthand. You must understand this notation without requiring clean grammar.

    OUTPUT RULES 
    ------------ 
    1. Output ONLY valid JSON. 
    2. Never output markdown. 
    3. Never output explanations outside JSON. 
    4. Never invent a weight, rep count, duration, distance, RIR or RPE. 
    5. Preserve information whenever possible. 
    6. If information is unknown, OMIT the key entirely. Never write "key": null. 
    7. Never silently discard a comment. 
    8. Comments attached to an exercise belong in that exercise's "notes". 
    9. Comments attached to a specific set belong in that set's "notes". 
    10. General workout comments belong in workout "notes". 
    11. "r0", "r1", "r2", "r3", "r4" means RIR, NOT reps. 
    12. "x", "×" can mean multiplication/set notation depending on context. 
    13. "2setsx14repsx15kg" means 2 sets, 14 reps, 15 kg. 
    14. "20kgx20repsx3sets" means 3 sets, 20 kg, 20 reps. 
    15. If "x10, x8, x6" follows an already established weight, reuse that weight. 
    16. "0kg" or "0x" generally means bodyweight/no added weight. 
    17. When bodyweight is available below, it may be used for bodyweight exercises. 
    18. "jeden" / "one" used before a weight means one set. 
    19. Preserve unusual comments and observations. 
    
    BODYWEIGHT 
    ---------- 
    A bodyweight value may be supplied in the user message as: 
    
    bodyweightKg = 86.7 
    
    This is the user's estimated bodyweight for the workout date. 
    Use this value ONLY for exercises that are genuinely performed with the user's bodyweight, such as:
     - pull-ups / chin-ups - dips - push-ups - bodyweight squats - other explicitly bodyweight movements 
    If a bodyweight exercise has no added external weight: - output weight = bodyweightKg - this 
    represents the user's total bodyweight contribution/load - do NOT output 0 merely because 
    the exercise is bodyweight If the log explicitly specifies additional weight for a bodyweight exercise, 
    preserve the distinction: - the written weight is the ADDED external load - do not replace it 
    with bodyweightKg - do not silently add bodyweightKg to the written number unless the output semantics 
    explicitly require total load IMPORTANT: The bodyweight value is an estimate derived from 
    historical measurements. It must NOT override an explicit bodyweight stated in the workout log. 
    If bodyweightKg is unavailable, do not invent bodyweight. Examples: "pullups x8" with 
    bodyweightKg = 86.7 -> weight = 86.7 "dips 10, 8, 7" with bodyweightKg = 86.7 -> weight = 86.7 for 
    each set "pullups +10kg x6" with bodyweightKg = 86.7 -> weight = 10 "dips +20kg x5" with 
    bodyweightKg = 86.7 -> weight = 20 "pushups 3x15" with bodyweightKg = 86.7 -> weight = 86.7 
    Never invent bodyweight when bodyweightKg is unavailable.

    EXERCISE MATCHING
    -----------------
    Every exercise MUST be mapped to exactly one ID from ALLOWED EXERCISE IDS
    below. Never invent an ID, never use an ID not in that list.

    Output ONLY "exerciseId" for exercise identity — do NOT output "name",
    "muscleGroup", or "category". These are looked up server-side from the ID.

    IDs are descriptive kebab-case English phrases (e.g. "close-grip-bench-press",
    "ez-bar-curl", "seated-cable-row") — match by movement/equipment meaning,
    not by exact string similarity to what the user wrote.

    Use equipment words in the log to pick the right variant family:
    - "sztanga"/barbell mentioned or implied -> a barbell-* id
    - "hantle"/dumbbell mentioned -> a dumbbell-* id
    - "linka"/"wyciąg"/cable mentioned -> a cable-* id
    - "maszyna"/machine mentioned -> a machine-* id
    - bodyweight / no equipment mentioned -> the plain bodyweight id (e.g. push-up, pull-up)

    If the log gives no modifier (no incline/decline/grip/stance info), choose
    the flat/standard/plain variant of that movement, not a specialized one.
    E.g. bare "lawka" with no other detail -> barbell-bench-press, NOT
    close-grip or decline. Only pick a specialized variant when the text
    actually supports it (e.g. "skos" -> an incline-* id).

    Common Polish shorthand seen in these logs (not exhaustive — apply the
    same reasoning to other Polish gym slang):

    ławka/lawka = bench press · skos = incline · zejscie/decline = decline
    martwy (ciąg) = deadlift · siady/siad/przysiad = squat · wiosło = row
    ohp = overhead press · hantle = dumbbell · sztanga = barbell
    biceps = curl (biceps) · triceps = extension/pushdown (triceps)
    jaja byka = rop push down
    rozpiętki = fly · dipy = dip · brzuch/brzuszki = crunch/sit-up
    łydy = calf raise · podciągnięcia/podciąganie = pull-up
    legpress = leg press · hamstringi = leg curl
    farmer walki = farmer walk/carry · bieżnia = treadmill · bieg = running
    spacer = walking · rowerem = cycling · schody = stair climber
    rozgrzewka = warm-up

    If the user's term is still ambiguous after this reasoning, preserve the
    original text in the exercise's "notes" and use the closest reasonable ID.

    ALLOWED EXERCISE IDS (grouped by muscle group; pick one per exercise)
    -----------------------------------------------------------------
    {{EXERCISE_ID_GROUPS}}

    CARDIO
    ------
    Running, walking, cycling, stairs and treadmill activity are CARDIO.

    They MUST NOT become weight-training exercises.

    Examples:

    "bieg 2.3km"
    "run 2.30km w 10:00"
    "bieżnia 15min 2.46km"
    "bieg tempo 11km/h 10min"
    "rowerem 4km"
    "piechtakiem 2km"
    "schody 10min"

    must become cardio entries.

    For cardio:
    - activity = canonical cardio exercise name
    - durationSeconds = duration when known
    - distanceKm = distance when known
    - speedKmh = speed when explicitly stated
    - inclinePercent = treadmill incline when stated
    - notes = remaining relevant information

    Do not create sets for cardio. An exercise object has EITHER "sets" OR
    "cardio" populated, never both, and never an empty array for the type
    that does not apply — omit that key entirely instead.

    A workout can contain both strength and cardio exercises.

    WEIGHT NORMALIZATION
    --------------------
    
    IMPORTANT:
    The "weight" in the output is ALWAYS the FINAL TOTAL LOAD IN KG.
    
    The user's written number may need conversion.
    
    BARBELL SETTING
    ---------------
    The user message contains exactly one setting:
    
    barbellWeightsArePerSide = true
    OR
    barbellWeightsArePerSide = false
    
    You MUST use this setting.
    
    WHEN barbellWeightsArePerSide = true:
    
    For EVERY BARBELL exercise:
    user weight = weight on ONE SIDE.
    
    Convert it to total load:
    
        total weight = (user weight × 2) + 20
    
    The 20 kg is the Olympic bar.
    
    Examples:
    - "bench 40kg x 8" → weight = 100
    - "bench 60kg x 5" → weight = 140
    - "bench 100kg x 5" → weight = 220
    - "deadlift 50kg x 5" → weight = 120
    
    Set:
    "weightEntryMode": "PerSide"
    
    WHEN barbellWeightsArePerSide = false:
    
    For EVERY BARBELL exercise:
    user weight = TOTAL LOAD.
    
    Do NOT double it.
    Do NOT add 20 kg.
    
    Examples:
    - "bench 40kg x 8" → weight = 40
    - "bench 60kg x 5" → weight = 60
    - "bench 100kg x 5" → weight = 100
    
    Set:
    "weightEntryMode": "Total"
    
    IMPORTANT:
    The conversion applies ONLY to BARBELL exercises.
    
    Never apply the barbell conversion to:
    - dumbbells
    - cables
    - machines
    - leg press
    - bodyweight exercises
    - cardio
    - farmer walks
    
    DUMBBELLS:
    "22.5kg dumbbell press" → weight = 22.5
    
    Do NOT convert 22.5 to 45.
    
    EMPTY BAR:
    If the log explicitly says the bar itself is 20 kg, output weight = 20.
    
    If a barbell set says "0kg" and it means an empty Olympic bar:
    - per-side mode: weight = 20
    - total mode: weight = 20
    
    If "0kg" means bodyweight/no external load, output weight = 0.
    
    Never invent bodyweight.

    WARMUPS
    -------
    "warmup", "rozgrzewka", "rozgrzewkowy", "warm-up" means warmup=true.

    Do not infer warmup merely because the weight is low.

    RIR / RPE
    ---------
    "r4" = RIR 4
    "r3" = RIR 3
    "r2" = RIR 2
    "r1" = RIR 1
    "r0" = RIR 0

    "RPE 8" = RPE 8.

    A comment such as:

    "15x8r5"

    means:
    weight 15
    reps 8
    RIR 5

    START / END TIME
    -----------------
    "start 17:16", "start 17:30" etc. mean the workout "startTime", formatted "HH:MM".
    "koniec 18:46", "koniec 18:48" etc. mean the workout "endTime", formatted "HH:MM".
    Do not confuse a date written near "koniec" with the end time.
    Omit "startTime"/"endTime" entirely if not present in the log.

    PERSONS
    -------
    Detect training partners mentioned in the log, e.g. "z karolem i markie",
    "Z karolem i markiem", "z Anią". These are typically Polish inflected forms
    (instrumental case) of names — normalize each to its base/nominative form,
    e.g. "karolem" -> "Karol", "markiem"/"markie" -> "Marek", "anią" -> "Ania".
    "z Zabo/Żabą" -> "Żaba"
    Output unique normalized names as a "persons" array on the workout.
    Do not include the log's author in this list.
    Omit "persons" entirely if no one else is mentioned — do not output an
    empty array.

    PERSONAL NOTATION EXAMPLES
    --------------------------
    The user's historical logs use notation such as:

    "lawka 0x6, 10kg x6, 20kgx10, 22.5kgx6"

    "deadlift 15kgx4(warmup), 25kgx4, 25kgx3,25kgx2, 25kgx1"

    "legpress 2setsx14repsx15kg"

    "lydy sitting 20kgx20repsx3sets[palce outside i palce inside, full range of motion stopping, 2 dni potem sore AF]"

    "Run 2.30km w 10:00"

    "Dynamiczna rozgrzewka 2 min"

    "Incline Dumbbel press 1, słownie "jeden" x 22.5kg"

    "lat pulldown 39x20, 45x15r3, 45x15r1"

    "biceps hantle 10kgx15, 10kgx15r3, 10kgx12r0"

    "bieżnia bieg 3min 0.38km"

    "barbell wiosło 0(15kgsztanga)x15, 5kx15, 10kx15, 10kx15r1"

    "ohp 15ksztnaga 0x10, 10kx5r0, 5kx8"

    Interpret these according to the rules above.

    DATE
    ----
    Recognize dates such as:
    24.06.2022
    10.08.2023
    14.08.2023

    Normalize to YYYY-MM-DD.

    OUTPUT JSON
    -----------
    Return exactly this structure. Every field marked optional below is OMITTED
    entirely when unknown — never emit it with a null value.

    {
      "date": "YYYY-MM-DD, optional",
      "title": "string, optional",
      "notes": "string, optional",
      "startTime": "HH:MM, optional",
      "endTime": "HH:MM, optional",
      "persons": ["Karol", "Marek"],
      "exercises": [
        {
          "exerciseId": "canonical ID",
          "notes": "string, optional",
          "sets": [
            {
              "weight": 100,
              "reps": 8,
              "rir": 2,
              "rpe": 8,
              "warmup": true,
              "notes": "string, optional"
            }
          ]
        },
        {
          "exerciseId": "treadmill-running",
          "cardio": [
            {
              "activity": "Treadmill Running",
              "durationSeconds": 600,
              "distanceKm": 1.65,
              "speedKmh": 11,
              "inclinePercent": 3,
              "notes": "string, optional"
            }
          ]
        }
      ]
    }

    Return JSON only.
    """;

    public async Task<ParseWorkoutResult> ParseWorkoutAsync(string rawText, bool barbellWeightsArePerSide, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(rawText)) throw new ArgumentException("Workout text is required.", nameof(rawText));

        var normalized = rawText.Trim();
        
        var workoutDate = TryExtractWorkoutDate(normalized); 
        var bodyweightKg = workoutDate.HasValue ? GetInterpolatedBodyweight(workoutDate.Value) : (double?)null;
        logger.LogInformation("Pre-cache setup: {Ms}ms", sw.ElapsedMilliseconds);
        
        var cacheKey = $"workout-ai:{ComputeHash(normalized)}:{barbellWeightsArePerSide}";
        if (cache.TryGetValue<ParseWorkoutResult>(cacheKey, out var cached)) { return cached!; }
        logger.LogInformation("Cache check: {Ms}ms", sw.ElapsedMilliseconds);
        
        var bodyweightPrompt = bodyweightKg.HasValue
            ? $"""
                workoutDate = {workoutDate:yyyy-MM-dd} bodyweightKg = {bodyweightKg.Value.ToString("0.0", CultureInfo.InvariantCulture)} 
               This bodyweight was estimated from the user's historical weight measurements by linearly interpolating between 
               the two surrounding historical anchor dates. Use it for unweighted bodyweight exercises when appropriate. 
               """
            : """ workoutDate = unavailable bodyweightKg = unavailable No reliable workout date was found in the log, so bodyweight must NOT be inferred or invented. """;

        var request = new DeepSeekChatRequest
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Stream = false,
            Thinking = new DeepSeekThinking
            {
                Type = "disabled"
            },
            ResponseFormat = new DeepSeekResponseFormat
            {
                Type = "json_object"
            },
            Messages =
                [
                    new DeepSeekMessage { Role = "system", Content = SystemPrompt },
                    new DeepSeekMessage
                    {
                        Role = "user",
                        Content =
                            $$""" Parse the following gym workout log. PARSING SETTING — MUST FOLLOW barbellWeightsArePerSide = {{barbellWeightsArePerSide.ToString().ToLowerInvariant()}} {{bodyweightPrompt}} Barbell setting: - If true, barbell weights are PER SIDE and must be converted to total load using (weight × 2) + 20. - If false, barbell weights are already TOTAL LOAD. - This setting applies only to barbells. Bodyweight: - Use bodyweightKg only for genuinely unweighted bodyweight exercises such as pull-ups, dips, push-ups, etc. - If an exercise has explicit added weight, preserve that added weight instead. - Never invent bodyweight when bodyweightKg is unavailable. WORKOUT LOG ----------- {{normalized}} """
                    }
                ]
        };

        logger.LogInformation("Request built: {Ms}ms", sw.ElapsedMilliseconds);
        
        using var response = await httpClient.PostAsJsonAsync("chat/completions", request, JsonOptions, cancellationToken);
        logger.LogInformation("HTTP call done: {Ms}ms", sw.ElapsedMilliseconds);
        
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogInformation("Body read: {Ms}ms", sw.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("DeepSeek request failed. Status={StatusCode}, Body={Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"DeepSeek returned {(int)response.StatusCode}: {responseBody}");
        }

        var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekChatResponse>(responseBody, JsonOptions);

        var content = deepSeekResponse?
            .Choices
            .FirstOrDefault()?
            .Message?
            .Content;

        if (string.IsNullOrWhiteSpace(content)) { throw new InvalidOperationException("DeepSeek returned an empty response."); }
        var workout = JsonSerializer.Deserialize<WorkoutLog>(content, JsonOptions);
        if (workout is null) { throw new InvalidOperationException("DeepSeek returned JSON that could not be parsed as WorkoutLog."); }

        HydrateFromCatalog(workout);

        var usage = deepSeekResponse.Usage;

        var result = new ParseWorkoutResult(
            workout,
            usage?.PromptCacheHitTokens > 0,
            usage?.PromptCacheHitTokens ?? 0,
            usage?.PromptCacheMissTokens ?? 0,
            usage?.PromptTokens ?? 0,
            usage?.CompletionTokens ?? 0);

        cache.Set(cacheKey, result, TimeSpan.FromMinutes(_options.CacheMinutes));
        logger.LogInformation("Total: {Ms}ms", sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    /// Fills in everything the model no longer outputs directly: exercise
    /// name/muscleGroup/category from the app's own catalog, category derived
    /// from whether sets or cardio was populated, and set numbers from array
    /// position. Keeping this server-side is what lets the prompt above skip
    /// these fields and cuts a large share of the completion tokens.
    /// </summary>
    private void HydrateFromCatalog(WorkoutLog workout)
    {
        foreach (var exercise in workout.Exercises)
        {
            if (CatalogById.TryGetValue(exercise.ExerciseId, out var catalogEntry))
            {
                exercise.Name = catalogEntry.Name;
                exercise.MuscleGroup = catalogEntry.MuscleGroup;
                exercise.Category = string.Equals(catalogEntry.Category, "Cardio", StringComparison.OrdinalIgnoreCase)
                    ? ExerciseCategory.Cardio
                    : ExerciseCategory.Strength;
            }
            else
            {
                // Model returned an ID that isn't in the catalog. Don't silently drop
                // the exercise — fall back to sets-vs-cardio to guess category, and
                // log loudly so the prompt or catalog can be fixed.
                logger.LogWarning("DeepSeek returned unknown exerciseId={ExerciseId}", exercise.ExerciseId);
                exercise.Name = exercise.ExerciseId;
                exercise.Category = exercise.Cardio.Count > 0
                    ? ExerciseCategory.Cardio
                    : ExerciseCategory.Strength;
            }

            for (var i = 0; i < exercise.Sets.Count; i++)
            {
                exercise.Sets[i].SetNumber = i + 1;
            }
        }
    }

    
    private static DateOnly? TryExtractWorkoutDate(string text) { // Prefer ISO dates: 2026-08-06
    var isoMatch = Regex.Match( text, @"(?<!\d)(\d{4})-(\d{1,2})-(\d{1,2})(?!\d)"); if (isoMatch.Success && int.TryParse(isoMatch.Groups[1].Value, out var isoYear) && int.TryParse(isoMatch.Groups[2].Value, out var isoMonth) && int.TryParse(isoMatch.Groups[3].Value, out var isoDay)) { try { return new DateOnly(isoYear, isoMonth, isoDay); } catch (ArgumentOutOfRangeException) {  } }
        // Polish/common format: 06.08.2026 / 06/08/2026
        var europeanMatch = Regex.Match( text, @"(?<!\d)(\d{1,2})[./](\d{1,2})[./](\d{4})(?!\d)"); if (europeanMatch.Success && int.TryParse(europeanMatch.Groups[1].Value, out var day) && int.TryParse(europeanMatch.Groups[2].Value, out var month) && int.TryParse(europeanMatch.Groups[3].Value, out var year)) { try { return new DateOnly(year, month, day); } catch (ArgumentOutOfRangeException) {  } } return null; }
        
    private static double GetInterpolatedBodyweight(DateOnly date) { if (date <= BodyweightHistory[0].Date) return BodyweightHistory[0].WeightKg; if (date >= BodyweightHistory[^1].Date) return BodyweightHistory[^1].WeightKg; for (var i = 1; i < BodyweightHistory.Length; i++) { var previous = BodyweightHistory[i - 1]; var next = BodyweightHistory[i]; if (date > next.Date) continue; if (date == next.Date) return next.WeightKg; var totalDays = next.Date.DayNumber - previous.Date.DayNumber; var elapsedDays = date.DayNumber - previous.Date.DayNumber; var fraction = (double)elapsedDays / totalDays; return previous.WeightKg + ((next.WeightKg - previous.WeightKg) * fraction); } return BodyweightHistory[^1].WeightKg; }
    
    
    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
    
    private readonly record struct BodyweightPoint( string DateString, double WeightKg) { public DateOnly Date => DateOnly.ParseExact( DateString, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
}