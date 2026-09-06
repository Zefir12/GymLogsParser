using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Configuration;
using Core.Models;
using GymLog.Api.Models;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymLog.Api.AI;

public sealed class DeepSeekService(
    HttpClient httpClient,
    IOptions<DeepSeekOptions> options,
    IMemoryCache cache,
    ILogger<DeepSeekService> logger)
    : IDeepSeekService
{
    private readonly DeepSeekOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

   private const string SystemPrompt = """
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
    6. If information is unknown, use null.
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
    17. Do not invent the actual bodyweight.
    18. "jeden" / "one" used before a weight means one set.
    19. Preserve unusual comments and observations.

    EXERCISE MATCHING
    -----------------
    Every exercise MUST be mapped to one of the supplied canonical exercise IDs.

    Never invent an exercise ID.

    The canonical exercise name should be returned in "name".

    The user's spelling does NOT need to match the canonical name.

    Examples:

    "lawka" -> barbell-bench-press
    "ławka" -> barbell-bench-press
    "lawka pozioma" -> barbell-bench-press
    "bench" -> barbell-bench-press
    "skos" -> incline-barbell-bench-press
    "ławka skos" -> incline-barbell-bench-press
    "incline dumbbel press" -> incline-dumbbell-press
    "deadlift" -> barbell-deadlift
    "martwy" -> barbell-deadlift
    "martwy ciąg" -> barbell-deadlift
    "siady" -> barbell-squat
    "siad" -> barbell-squat
    "squats" -> barbell-squat
    "legpress" -> leg-press
    "hamstringi lezaco" -> lying-leg-curl
    "hamstringi leżąco" -> lying-leg-curl
    "leg curl" -> lying-leg-curl
    "lydy" -> calf-raise
    "łydy" -> calf-raise
    "podciogniecia" -> pull-up
    "podciągnięcia" -> pull-up
    "podciąganie" -> pull-up
    "lat pulldown" -> lat-pulldown
    "wioslo" -> barbell-row
    "wiosło" -> barbell-row
    "ohp" -> overhead-press
    "biceps hantle" -> dumbbell-curl
    "biceps sztanga" -> barbell-curl
    "rozpietki" -> chest-fly
    "rozpiętki" -> chest-fly
    "dipy" -> dip
    "skull crushery" -> triceps-skull-crusher
    "brzuch" -> crunch
    "brzuszki" -> crunch
    "farmer walki" -> farmer-walk

    If the user's term is ambiguous, choose the closest canonical exercise
    only when there is enough contextual evidence. Otherwise preserve the text
    in notes and use the closest reasonable canonical exercise.

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

    Do not create sets for cardio.

    A workout can contain both strength and cardio exercises.

    WEIGHTS
    -------
    All weights in the output are kilograms.

    Do NOT output a unit field.

    The parser receives a setting:

    barbellWeightsArePerSide

    When false:
        "100kg" on a barbell exercise means 100 kg total.

    When true:
        for BARBELL exercises:
        "40kg" means 40 kg on EACH SIDE.
        Therefore:
            40 kg + 40 kg + 20 kg bar = 100 kg total.

    When true, set:
        "weightEntryMode": "PerSide"

    When false, set:
        "weightEntryMode": "Total"

    The 20 kg Olympic bar assumption applies ONLY to barbell exercises.

    Do not add 20 kg to:
    - dumbbells
    - machines
    - leg press
    - cables
    - bodyweight
    - cardio
    - other non-barbell exercises

    If the log explicitly says "20kg sztanga" or otherwise clearly refers to
    the empty bar, the resulting total is 20 kg.

    If a barbell exercise says "0", "0kg", or "0x", this represents an empty bar
    or bodyweight/no added load depending on the exercise context.

    IMPORTANT:
    The output "weight" is always the NORMALIZED TOTAL LOAD.

    Example with barbellWeightsArePerSide=true:

    "lawka 40kg x 8"

    becomes:

    weight = 100
    weightEntryMode = "PerSide"

    Example:

    "lawka 100kg x 5"

    becomes:

    weight = 220
    weightEntryMode = "PerSide"

    ONLY if the setting says weights are per-side.

    If the setting says false:

    "lawka 100kg x 5"

    becomes:

    weight = 100
    weightEntryMode = "Total"

    DUMBBELLS
    ---------
    Do not double dumbbell weights.

    If the log says:

    "incline dumbbell press 22.5kg"

    output:

    weight = 22.5

    Do not turn it into 45 kg.

    LEG PRESS
    ---------
    Leg press notation may be ambiguous.

    If the user explicitly says "per side", preserve that information in notes
    and use the user's stated value as the normalized recorded weight unless
    the application has a separate leg-press loading convention.

    Do NOT automatically add a barbell.

    FARMER WALK
    -----------
    For farmer walk, weight normally refers to the load carried in each hand
    if the user explicitly indicates one-hand/dumbbell loading.

    Do not invent a doubled total unless the text explicitly supports it.

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

    DATE / TIME
    -----------
    Recognize dates such as:
    24.06.2022
    10.08.2023
    14.08.2023

    Normalize to YYYY-MM-DD.

    "start 17:16" and "koniec 18:46" are workout metadata.
    If the output schema does not contain start/end time, preserve them in workout notes.

    Do not confuse dates written near "koniec" with the workout date.

    OUTPUT JSON
    -----------
    Return exactly this structure:

    {
      "date": "YYYY-MM-DD or null",
      "title": "string or null",
      "notes": "string or null",
      "exercises": [
        {
          "exerciseId": "canonical ID",
          "name": "canonical exercise name",
          "muscleGroup": "string or null",
          "notes": "string or null",
          "category": "Strength",
          "sets": [
            {
              "setNumber": 1,
              "weight": 100,
              "weightEntryMode": "Total",
              "reps": 8,
              "rir": null,
              "rpe": null,
              "warmup": false,
              "notes": null
            }
          ],
          "cardio": []
        },
        {
          "exerciseId": "treadmill-running",
          "name": "Treadmill Running",
          "muscleGroup": "Cardio",
          "notes": null,
          "category": "Cardio",
          "sets": [],
          "cardio": [
            {
              "activity": "Treadmill Running",
              "durationSeconds": 600,
              "distanceKm": 1.65,
              "speedKmh": 11,
              "inclinePercent": 3,
              "notes": null
            }
          ]
        }
      ]
    }

    For cardio exercises:
    - sets MUST be []
    - cardio MUST contain the cardio data

    For strength exercises:
    - cardio MUST be []

    Return JSON only.
    """;

   public async Task<ParseWorkoutResult> ParseWorkoutAsync(
        string rawText,
        bool barbellWeightsArePerSide,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            throw new ArgumentException("Workout text is required.", nameof(rawText));

        var normalized = rawText.Trim();
        var cacheKey = $"workout-ai:{ComputeHash(normalized)}";

        if (cache.TryGetValue<ParseWorkoutResult>(cacheKey, out var cached))
        {
            return cached!;
        }

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
                new DeepSeekMessage
                {
                    Role = "system",
                    Content = SystemPrompt
                },
                new DeepSeekMessage
                {
                    Role = "user",
                    Content = $"""
                               Parse the following gym workout log.

                               PARSING SETTINGS
                               ----------------
                               barbellWeightsArePerSide = {barbellWeightsArePerSide.ToString().ToLowerInvariant()}

                               If this is true, bare numeric weights on barbell exercises are plates
                               on ONE SIDE of the bar.

                               If this is false, bare numeric weights on barbell exercises are the
                               TOTAL weight including the bar.

                               WORKOUT LOG
                               -----------
                               {normalized}
                               """
                }
            ]
        };

        using var response = await httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            JsonOptions,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "DeepSeek request failed. Status={StatusCode}, Body={Body}",
                response.StatusCode,
                responseBody);

            throw new HttpRequestException(
                $"DeepSeek returned {(int)response.StatusCode}: {responseBody}");
        }

        var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekChatResponse>(
            responseBody,
            JsonOptions);

        var content = deepSeekResponse?
            .Choices
            .FirstOrDefault()?
            .Message?
            .Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "DeepSeek returned an empty response.");
        }

        var workout = JsonSerializer.Deserialize<WorkoutLog>(
            content,
            JsonOptions);

        if (workout is null)
        {
            throw new InvalidOperationException(
                "DeepSeek returned JSON that could not be parsed as WorkoutLog.");
        }

        var usage = deepSeekResponse.Usage;

        var result = new ParseWorkoutResult(
            workout,
            usage?.PromptCacheHitTokens > 0,
            usage?.PromptCacheHitTokens ?? 0,
            usage?.PromptCacheMissTokens ?? 0,
            usage?.PromptTokens ?? 0,
            usage?.CompletionTokens ?? 0);

        cache.Set(
            cacheKey,
            result,
            TimeSpan.FromMinutes(_options.CacheMinutes));

        return result;
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes);
    }
}