using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Data-only migration (#33): one flat level of muscle groups. "Legs",
    /// "Shoulders", "Back", "Core" and "Full Body" are gone; every catalog entry
    /// gets its primary target (Lats, Upper Back, Front/Side/Rear Delts, ...), and
    /// logged exercises take the group of their catalog entry, falling back to a
    /// mapping from the old group for exercises no longer in the catalog.
    /// No schema change, so the model snapshot is unchanged.
    /// </summary>
    public partial class FlatMuscleGroups : Migration
    {
        /// <summary>(catalog ID, group seeded in ExerciseDefinitions, new group).</summary>
        private static readonly (string Id, string Old, string New)[] Catalog =
        [
            ("barbell-bench-press",                  "Chest", "Chest"),
            ("incline-barbell-bench-press",          "Chest", "Chest"),
            ("decline-barbell-bench-press",          "Chest", "Chest"),
            ("close-grip-bench-press",               "Chest", "Triceps"),
            ("dumbbell-bench-press",                 "Chest", "Chest"),
            ("incline-dumbbell-press",               "Chest", "Chest"),
            ("decline-dumbbell-press",               "Chest", "Chest"),
            ("dumbbell-floor-press",                 "Chest", "Chest"),
            ("chest-fly",                            "Chest", "Chest"),
            ("dumbbell-fly",                         "Chest", "Chest"),
            ("incline-dumbbell-fly",                 "Chest", "Chest"),
            ("cable-crossover",                      "Chest", "Chest"),
            ("cable-chest-fly",                      "Chest", "Chest"),
            ("pec-deck",                             "Chest", "Chest"),
            ("push-up",                              "Chest", "Chest"),
            ("incline-push-up",                      "Chest", "Chest"),
            ("decline-push-up",                      "Chest", "Chest"),
            ("diamond-push-up",                      "Chest", "Triceps"),
            ("dip",                                  "Chest", "Chest"),
            ("assisted-dip",                         "Chest", "Chest"),
            ("barbell-deadlift",                     "Back", "Lower Back"),
            ("sumo-deadlift",                        "Back", "Glutes"),
            ("romanian-deadlift",                    "Hamstrings", "Hamstrings"),
            ("stiff-leg-deadlift",                   "Hamstrings", "Hamstrings"),
            ("rack-pull",                            "Back", "Lower Back"),
            ("barbell-row",                          "Back", "Upper Back"),
            ("pendlay-row",                          "Back", "Upper Back"),
            ("t-bar-row",                            "Back", "Upper Back"),
            ("dumbbell-row",                         "Back", "Upper Back"),
            ("chest-supported-dumbbell-row",         "Back", "Upper Back"),
            ("seated-cable-row",                     "Back", "Upper Back"),
            ("single-arm-cable-row",                 "Back", "Upper Back"),
            ("lat-pulldown",                         "Back", "Lats"),
            ("close-grip-lat-pulldown",              "Back", "Lats"),
            ("wide-grip-lat-pulldown",               "Back", "Lats"),
            ("pull-up",                              "Back", "Lats"),
            ("chin-up",                              "Back", "Lats"),
            ("assisted-pull-up",                     "Back", "Lats"),
            ("neutral-grip-pull-up",                 "Back", "Lats"),
            ("straight-arm-pulldown",                "Back", "Lats"),
            ("machine-row",                          "Back", "Upper Back"),
            ("overhead-press",                       "Shoulders", "Front Delts"),
            ("seated-barbell-overhead-press",        "Shoulders", "Front Delts"),
            ("dumbbell-shoulder-press",              "Shoulders", "Front Delts"),
            ("arnold-press",                         "Shoulders", "Front Delts"),
            ("machine-shoulder-press",               "Shoulders", "Front Delts"),
            ("dumbbell-lateral-raise",               "Shoulders", "Side Delts"),
            ("cable-lateral-raise",                  "Shoulders", "Side Delts"),
            ("machine-lateral-raise",                "Shoulders", "Side Delts"),
            ("front-raise",                          "Shoulders", "Front Delts"),
            ("dumbbell-front-raise",                 "Shoulders", "Front Delts"),
            ("rear-delt-fly",                        "Shoulders", "Rear Delts"),
            ("reverse-pec-deck",                     "Shoulders", "Rear Delts"),
            ("face-pull",                            "Shoulders", "Rear Delts"),
            ("upright-row",                          "Shoulders", "Side Delts"),
            ("barbell-curl",                         "Biceps", "Biceps"),
            ("ez-bar-curl",                          "Biceps", "Biceps"),
            ("dumbbell-curl",                        "Biceps", "Biceps"),
            ("alternating-dumbbell-curl",            "Biceps", "Biceps"),
            ("hammer-curl",                          "Biceps", "Biceps"),
            ("cross-body-hammer-curl",               "Biceps", "Biceps"),
            ("incline-dumbbell-curl",                "Biceps", "Biceps"),
            ("preacher-curl",                        "Biceps", "Biceps"),
            ("preacher-barbell-curl",                "Biceps", "Biceps"),
            ("cable-curl",                           "Biceps", "Biceps"),
            ("concentration-curl",                   "Biceps", "Biceps"),
            ("triceps-skull-crusher",                "Triceps", "Triceps"),
            ("lying-triceps-extension",              "Triceps", "Triceps"),
            ("triceps-extension",                    "Triceps", "Triceps"),
            ("cable-triceps-pushdown",               "Triceps", "Triceps"),
            ("rope-triceps-pushdown",                "Triceps", "Triceps"),
            ("overhead-triceps-extension",           "Triceps", "Triceps"),
            ("cable-overhead-triceps-extension",     "Triceps", "Triceps"),
            ("dumbbell-overhead-triceps-extension",  "Triceps", "Triceps"),
            ("triceps-kickback",                     "Triceps", "Triceps"),
            ("bench-dip",                            "Triceps", "Triceps"),
            ("barbell-squat",                        "Legs", "Quadriceps"),
            ("front-squat",                          "Legs", "Quadriceps"),
            ("hack-squat",                           "Legs", "Quadriceps"),
            ("leg-press",                            "Legs", "Quadriceps"),
            ("single-leg-press",                     "Legs", "Quadriceps"),
            ("leg-extension",                        "Quadriceps", "Quadriceps"),
            ("bulgarian-split-squat",                "Legs", "Quadriceps"),
            ("walking-lunge",                        "Legs", "Quadriceps"),
            ("reverse-lunge",                        "Legs", "Quadriceps"),
            ("forward-lunge",                        "Legs", "Quadriceps"),
            ("dumbbell-lunge",                       "Legs", "Quadriceps"),
            ("step-up",                              "Legs", "Quadriceps"),
            ("goblet-squat",                         "Legs", "Quadriceps"),
            ("bodyweight-squat",                     "Legs", "Quadriceps"),
            ("sumo-squat",                           "Legs", "Adductors"),
            ("lying-leg-curl",                       "Hamstrings", "Hamstrings"),
            ("seated-leg-curl",                      "Hamstrings", "Hamstrings"),
            ("standing-leg-curl",                    "Hamstrings", "Hamstrings"),
            ("barbell-hip-thrust",                   "Glutes", "Glutes"),
            ("glute-bridge",                         "Glutes", "Glutes"),
            ("single-leg-glute-bridge",              "Glutes", "Glutes"),
            ("cable-kickback",                       "Glutes", "Glutes"),
            ("glute-kickback-machine",               "Glutes", "Glutes"),
            ("cable-pull-through",                   "Glutes", "Glutes"),
            ("good-morning",                         "Hamstrings", "Hamstrings"),
            ("calf-raise",                           "Calves", "Calves"),
            ("standing-calf-raise",                  "Calves", "Calves"),
            ("seated-calf-raise",                    "Calves", "Calves"),
            ("leg-press-calf-raise",                 "Calves", "Calves"),
            ("crunch",                               "Abs", "Abs"),
            ("sit-up",                               "Abs", "Abs"),
            ("weighted-sit-up",                      "Abs", "Abs"),
            ("cable-crunch",                         "Abs", "Abs"),
            ("reverse-crunch",                       "Abs", "Abs"),
            ("leg-raise",                            "Abs", "Abs"),
            ("hanging-leg-raise",                    "Abs", "Abs"),
            ("hanging-knee-raise",                   "Abs", "Abs"),
            ("ab-wheel-rollout",                     "Abs", "Abs"),
            ("plank",                                "Core", "Abs"),
            ("side-plank",                           "Core", "Abs"),
            ("russian-twist",                        "Abs", "Abs"),
            ("bicycle-crunch",                       "Abs", "Abs"),
            ("dead-bug",                             "Core", "Abs"),
            ("bird-dog",                             "Core", "Abs"),
            ("farmer-walk",                          "Full Body", "Forearms"),
            ("farmers-carry",                        "Full Body", "Forearms"),
            ("suitcase-carry",                       "Full Body", "Abs"),
            ("kettlebell-swing",                     "Full Body", "Glutes"),
            ("kettlebell-clean",                     "Full Body", "Glutes"),
            ("kettlebell-snatch",                    "Full Body", "Glutes"),
            ("turkish-get-up",                       "Full Body", "Abs"),
            ("burpee",                               "Full Body", "Quadriceps"),
            ("mountain-climber",                     "Full Body", "Abs"),
            ("battle-rope",                          "Full Body", "Front Delts"),
            ("sled-push",                            "Full Body", "Quadriceps"),
            ("sled-pull",                            "Full Body", "Hamstrings"),
            ("treadmill-running",                    "Cardio", "Cardio"),
            ("treadmill-walking",                    "Cardio", "Cardio"),
            ("incline-treadmill-walking",            "Cardio", "Cardio"),
            ("outdoor-running",                      "Cardio", "Cardio"),
            ("outdoor-walking",                      "Cardio", "Cardio"),
            ("cycling",                              "Cardio", "Cardio"),
            ("stationary-bike",                      "Cardio", "Cardio"),
            ("spin-bike",                            "Cardio", "Cardio"),
            ("stair-climber",                        "Cardio", "Cardio"),
            ("elliptical",                           "Cardio", "Cardio"),
            ("rowing-machine",                       "Cardio", "Cardio"),
            ("jump-rope",                            "Cardio", "Cardio"),
            ("jumping-jacks",                        "Cardio", "Cardio"),
            ("high-knees",                           "Cardio", "Cardio"),
            ("power-clean",                          "Full Body", "Glutes"),
            ("clean-and-jerk",                       "Full Body", "Glutes"),
            ("barbell-snatch",                       "Full Body", "Glutes"),
            ("hang-clean",                           "Full Body", "Glutes"),
            ("push-press",                           "Shoulders", "Front Delts"),
            ("push-jerk",                            "Full Body", "Front Delts"),
            ("dynamic-warmup",                       "Warm-up", "Warm-up"),
            ("jumping-jack-warmup",                  "Warm-up", "Warm-up"),
            ("arm-circles",                          "Shoulders", "Warm-up"),
            ("hip-circles",                          "Hips", "Warm-up"),
            ("bodyweight-good-morning",              "Hamstrings", "Hamstrings"),
            ("worlds-greatest-stretch",              "Full Body", "Warm-up"),
            ("chest-press",                          "Chest", "Chest"),
            ("good-girl",                            "Glutes", "Adductors"),
            ("bad-girl",                             "Glutes", "Abductors")
        ];

        /// <summary>Old group -> new group, for user-added catalog entries and logged exercises without a catalog match.</summary>
        private static readonly (string Old, string New)[] Fallback =
        [
            ("Legs", "Quadriceps"),
            ("Shoulders", "Front Delts"),
            ("Back", "Upper Back"),
            ("Core", "Abs"),
            ("Full Body", "Glutes"),
            ("Hips", "Glutes")
        ];

        /// <summary>New group -> old group, for Down.</summary>
        private static readonly (string Old, string New)[] NewToOld =
        [
            ("Back", "Lats"),
            ("Back", "Upper Back"),
            ("Back", "Lower Back"),
            ("Shoulders", "Front Delts"),
            ("Shoulders", "Side Delts"),
            ("Shoulders", "Rear Delts"),
            ("Legs", "Adductors"),
            ("Legs", "Abductors")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Seeded catalog entries, by ID.
            migrationBuilder.Sql(UpdateCatalog(Catalog.Select(c => (c.Id, c.New))));

            // 2. User-added catalog entries still on an old group.
            migrationBuilder.Sql(UpdateByGroup("exercise_definitions", Fallback));

            // 3. Logged exercises: the group of their catalog entry.
            migrationBuilder.Sql(CopyGroupFromCatalog);

            // 4. Logged exercises whose ID is not in the catalog.
            migrationBuilder.Sql(UpdateByGroup("exercises", Fallback));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UpdateCatalog(Catalog.Select(c => (c.Id, c.Old))));
            migrationBuilder.Sql(UpdateByGroup("exercise_definitions", NewToOld.Select(r => (r.New, r.Old))));
            migrationBuilder.Sql(CopyGroupFromCatalog);
            migrationBuilder.Sql(UpdateByGroup("exercises", NewToOld.Select(r => (r.New, r.Old))));
        }

        private const string CopyGroupFromCatalog = """
            UPDATE exercises AS e
            SET "MuscleGroup" = d."MuscleGroup"
            FROM exercise_definitions AS d
            WHERE e."ExerciseId" = d."Id"
              AND e."MuscleGroup" IS DISTINCT FROM d."MuscleGroup";
            """;

        // All values below are compile-time constants from this file (no quotes inside),
        // so building the SQL by string concatenation is safe.
        private static string UpdateCatalog(IEnumerable<(string Id, string Group)> rows) =>
            $"""
            UPDATE exercise_definitions AS d
            SET "MuscleGroup" = v.grp
            FROM (VALUES {string.Join(", ", rows.Select(r => $"('{r.Id}', '{r.Group}')"))}) AS v(id, grp)
            WHERE d."Id" = v.id;
            """;

        /// <summary>Case-insensitive rename of groups in <paramref name="table"/>'s MuscleGroup column.</summary>
        private static string UpdateByGroup(string table, IEnumerable<(string From, string To)> map)
        {
            var pairs = map.ToList();
            return $"""
                UPDATE {table} AS t
                SET "MuscleGroup" = v.grp
                FROM (VALUES {string.Join(", ", pairs.Select(p => $"('{p.From.ToLowerInvariant()}', '{p.To}')"))}) AS v(src, grp)
                WHERE lower(trim(t."MuscleGroup")) = v.src;
                """;
        }
    }
}
