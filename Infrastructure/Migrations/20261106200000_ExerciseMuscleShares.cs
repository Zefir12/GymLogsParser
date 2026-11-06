using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Weighted muscle contributions (#35). New table <c>exercise_definition_muscles</c>
    /// holds each catalog exercise's secondary muscle groups with a share (a set counts
    /// share × 1 set for that group); the primary group stays in
    /// <c>exercise_definitions.MuscleGroup</c> and always counts 1.
    /// Also adds the Traps and Neck groups: shrug and neck exercises move there, and
    /// four catalog entries are seeded for them when no entry has the same ID or name.
    /// </summary>
    public partial class ExerciseMuscleShares : Migration
    {
        /// <summary>New catalog entries: (ID, name, group, barbell).</summary>
        private static readonly (string Id, string Name, string Group, bool Barbell)[] NewEntries =
        [
            ("barbell-shrug",  "Barbell Shrug",  "Traps", true),
            ("dumbbell-shrug", "Dumbbell Shrug", "Traps", false),
            ("neck-curl",      "Neck Curl",      "Neck",  false),
            ("neck-extension", "Neck Extension", "Neck",  false)
        ];

        /// <summary>
        /// Secondary muscles of the seeded catalog entries, as "Group share, ...", from
        /// zefir's table on #35 (rows: Upper Back 1, Lats 0.5; pulldowns and pull-ups:
        /// Lats 1, Upper Back 0.5; deadlift: 0.3 to both, ...). The primary group always
        /// counts 1, so deadlift (Lower Back) and dips (Chest) count 1 there, not 0.8 / 0.5.
        /// Entries not listed (flyes, raises, leg curls, triceps isolation, calves, abs,
        /// cardio, warm-ups, ...) only count towards their primary group.
        /// </summary>
        private static readonly (string Id, string Muscles)[] Shares =
        [
            ("barbell-bench-press",                 "Triceps 0.5, Front Delts 0.5"),
            ("incline-barbell-bench-press",         "Triceps 0.5, Front Delts 0.5"),
            ("decline-barbell-bench-press",         "Triceps 0.5, Front Delts 0.5"),
            ("close-grip-bench-press",              "Chest 0.75, Front Delts 0.5"),
            ("dumbbell-bench-press",                "Triceps 0.5, Front Delts 0.5"),
            ("incline-dumbbell-press",              "Triceps 0.5, Front Delts 0.5"),
            ("decline-dumbbell-press",              "Triceps 0.5, Front Delts 0.5"),
            ("dumbbell-floor-press",                "Triceps 0.5, Front Delts 0.5"),
            ("push-up",                             "Triceps 0.5, Front Delts 0.5"),
            ("incline-push-up",                     "Triceps 0.5, Front Delts 0.5"),
            ("decline-push-up",                     "Triceps 0.5, Front Delts 0.5"),
            ("diamond-push-up",                     "Chest 0.5, Front Delts 0.5"),
            ("dip",                                 "Triceps 0.5, Front Delts 0.5"),
            ("assisted-dip",                        "Triceps 0.5, Front Delts 0.5"),
            ("barbell-deadlift",                    "Hamstrings 0.5, Glutes 0.5, Traps 0.5, Lats 0.3, Upper Back 0.3, Forearms 0.25, Abs 0.25"),
            ("sumo-deadlift",                       "Lower Back 0.8, Hamstrings 0.5, Quadriceps 0.5, Adductors 0.5, Traps 0.5, Lats 0.3, Upper Back 0.3, Forearms 0.25, Abs 0.25"),
            ("romanian-deadlift",                   "Lower Back 0.8, Glutes 0.5"),
            ("stiff-leg-deadlift",                  "Lower Back 0.8, Glutes 0.5"),
            ("rack-pull",                           "Traps 0.5, Glutes 0.5, Hamstrings 0.3, Lats 0.3, Upper Back 0.3, Forearms 0.25"),
            ("barbell-row",                         "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25, Lower Back 0.3"),
            ("pendlay-row",                         "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25, Lower Back 0.3"),
            ("t-bar-row",                           "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25, Lower Back 0.3"),
            ("dumbbell-row",                        "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25"),
            ("chest-supported-dumbbell-row",        "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25"),
            ("seated-cable-row",                    "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25"),
            ("single-arm-cable-row",                "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25"),
            ("lat-pulldown",                        "Upper Back 0.5, Biceps 0.5"),
            ("close-grip-lat-pulldown",             "Upper Back 0.5, Biceps 0.5"),
            ("wide-grip-lat-pulldown",              "Upper Back 0.5, Biceps 0.5"),
            ("pull-up",                             "Upper Back 0.5, Biceps 0.5"),
            ("chin-up",                             "Upper Back 0.5, Biceps 0.75"),
            ("assisted-pull-up",                    "Upper Back 0.5, Biceps 0.5"),
            ("neutral-grip-pull-up",                "Upper Back 0.5, Biceps 0.5"),
            ("machine-row",                         "Lats 0.5, Rear Delts 0.5, Biceps 0.5, Traps 0.5, Forearms 0.25"),
            ("overhead-press",                      "Triceps 0.5, Side Delts 0.3"),
            ("seated-barbell-overhead-press",       "Triceps 0.5, Side Delts 0.3"),
            ("dumbbell-shoulder-press",             "Triceps 0.5, Side Delts 0.3"),
            ("arnold-press",                        "Triceps 0.5, Side Delts 0.3"),
            ("machine-shoulder-press",              "Triceps 0.5, Side Delts 0.3"),
            ("upright-row",                         "Traps 0.5"),
            ("barbell-curl",                        "Forearms 0.25"),
            ("ez-bar-curl",                         "Forearms 0.25"),
            ("dumbbell-curl",                       "Forearms 0.25"),
            ("alternating-dumbbell-curl",           "Forearms 0.25"),
            ("hammer-curl",                         "Forearms 0.5"),
            ("cross-body-hammer-curl",              "Forearms 0.5"),
            ("incline-dumbbell-curl",               "Forearms 0.25"),
            ("preacher-curl",                       "Forearms 0.25"),
            ("preacher-barbell-curl",               "Forearms 0.25"),
            ("cable-curl",                          "Forearms 0.25"),
            ("concentration-curl",                  "Forearms 0.25"),
            ("bench-dip",                           "Front Delts 0.25"),
            ("barbell-squat",                       "Glutes 0.5, Adductors 0.5, Lower Back 0.3, Abs 0.25"),
            ("front-squat",                         "Glutes 0.5, Adductors 0.5, Lower Back 0.3, Abs 0.25"),
            ("hack-squat",                          "Glutes 0.5"),
            ("leg-press",                           "Glutes 0.5"),
            ("single-leg-press",                    "Glutes 0.5"),
            ("bulgarian-split-squat",               "Glutes 0.75, Adductors 0.5"),
            ("walking-lunge",                       "Glutes 0.75, Adductors 0.5"),
            ("reverse-lunge",                       "Glutes 0.75, Adductors 0.5"),
            ("forward-lunge",                       "Glutes 0.75, Adductors 0.5"),
            ("dumbbell-lunge",                      "Glutes 0.75, Adductors 0.5"),
            ("step-up",                             "Glutes 0.75"),
            ("goblet-squat",                        "Glutes 0.5, Adductors 0.5, Abs 0.25"),
            ("bodyweight-squat",                    "Glutes 0.5"),
            ("sumo-squat",                          "Quadriceps 0.75, Glutes 0.5"),
            ("cable-pull-through",                  "Hamstrings 0.5"),
            ("good-morning",                        "Lower Back 0.8, Glutes 0.5"),
            ("bird-dog",                            "Lower Back 0.3, Glutes 0.25"),
            ("farmer-walk",                         "Traps 0.5, Abs 0.25"),
            ("farmers-carry",                       "Traps 0.5, Abs 0.25"),
            ("suitcase-carry",                      "Forearms 0.5, Traps 0.25"),
            ("kettlebell-swing",                    "Hamstrings 0.5, Lower Back 0.5, Forearms 0.25"),
            ("kettlebell-clean",                    "Hamstrings 0.5, Lower Back 0.5, Traps 0.5, Forearms 0.25"),
            ("kettlebell-snatch",                   "Hamstrings 0.5, Lower Back 0.5, Traps 0.5, Front Delts 0.25, Forearms 0.25"),
            ("turkish-get-up",                      "Front Delts 0.5, Glutes 0.25"),
            ("burpee",                              "Chest 0.5, Triceps 0.25, Front Delts 0.25"),
            ("battle-rope",                         "Abs 0.25, Forearms 0.25"),
            ("sled-push",                           "Glutes 0.5, Calves 0.25"),
            ("sled-pull",                           "Glutes 0.5"),
            ("power-clean",                         "Hamstrings 0.5, Quadriceps 0.5, Traps 0.5, Lower Back 0.5, Upper Back 0.3, Forearms 0.25"),
            ("clean-and-jerk",                      "Hamstrings 0.5, Quadriceps 0.5, Traps 0.5, Lower Back 0.5, Upper Back 0.3, Forearms 0.25, Front Delts 0.5, Triceps 0.25"),
            ("barbell-snatch",                      "Hamstrings 0.5, Quadriceps 0.5, Traps 0.5, Lower Back 0.5, Upper Back 0.3, Forearms 0.25, Front Delts 0.25"),
            ("hang-clean",                          "Hamstrings 0.5, Quadriceps 0.5, Traps 0.5, Lower Back 0.5, Upper Back 0.3, Forearms 0.25"),
            ("push-press",                          "Triceps 0.5, Side Delts 0.3, Quadriceps 0.25"),
            ("push-jerk",                           "Triceps 0.5, Side Delts 0.3, Quadriceps 0.25"),
            ("bodyweight-good-morning",             "Lower Back 0.5, Glutes 0.5"),
            ("chest-press",                         "Triceps 0.5, Front Delts 0.5"),
            ("barbell-shrug",                       "Forearms 0.25"),
            ("dumbbell-shrug",                      "Forearms 0.25")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercise_definition_muscles",
                columns: table => new
                {
                    ExerciseDefinitionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MuscleGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Share = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_definition_muscles", x => new { x.ExerciseDefinitionId, x.MuscleGroup });
                    table.ForeignKey(
                        name: "FK_exercise_definition_muscles_exercise_definitions_ExerciseDefinitionId",
                        column: x => x.ExerciseDefinitionId,
                        principalTable: "exercise_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 1. Shrug and neck exercises the user already added move to Traps / Neck.
            migrationBuilder.Sql("""
                UPDATE exercise_definitions
                SET "MuscleGroup" = 'Traps'
                WHERE "Category" = 'Strength'
                  AND ("Id" LIKE '%shrug%' OR lower("Name") LIKE '%shrug%' OR lower("Name") LIKE '%szrug%');

                UPDATE exercise_definitions
                SET "MuscleGroup" = 'Neck'
                WHERE "Category" = 'Strength'
                  AND ("Id" LIKE 'neck-%' OR lower("Name") LIKE 'neck %' OR lower("Name") LIKE '%szyj%');
                """);

            // 2. Seed shrugs and neck work, unless an entry with that ID or name exists.
            migrationBuilder.Sql($"""
                INSERT INTO exercise_definitions ("Id", "Name", "Category", "MuscleGroup", "Barbell", "Aliases")
                SELECT v.id, v.name, 'Strength', v.grp, v.barbell, ARRAY[]::text[]
                FROM (VALUES {string.Join(", ", NewEntries.Select(e => $"('{e.Id}', '{e.Name}', '{e.Group}', {(e.Barbell ? "TRUE" : "FALSE")})"))}) AS v(id, name, grp, barbell)
                WHERE NOT EXISTS (
                    SELECT 1 FROM exercise_definitions AS d
                    WHERE d."Id" = v.id OR lower(d."Name") = lower(v.name));
                """);

            // 3. Logged exercises follow their catalog entry into Traps / Neck.
            migrationBuilder.Sql("""
                UPDATE exercises AS e
                SET "MuscleGroup" = d."MuscleGroup"
                FROM exercise_definitions AS d
                WHERE e."ExerciseId" = d."Id"
                  AND d."MuscleGroup" IN ('Traps', 'Neck')
                  AND e."MuscleGroup" IS DISTINCT FROM d."MuscleGroup";
                """);

            // 4. Secondary muscles of the seeded entries. Skipped for an entry whose
            // primary is now that same group or that became a cardio entry.
            migrationBuilder.Sql($"""
                INSERT INTO exercise_definition_muscles ("ExerciseDefinitionId", "MuscleGroup", "Share")
                SELECT v.id, v.grp, v.share
                FROM (VALUES {string.Join(", ", ShareRows().Select(r => $"('{r.Id}', '{r.Group}', {r.Share.ToString(CultureInfo.InvariantCulture)})"))}) AS v(id, grp, share)
                JOIN exercise_definitions AS d ON d."Id" = v.id
                WHERE d."Category" = 'Strength'
                  AND d."MuscleGroup" NOT IN ('Cardio', 'Warm-up')
                  AND d."MuscleGroup" <> v.grp
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_definition_muscles");

            migrationBuilder.Sql($"""
                DELETE FROM exercise_definitions
                WHERE "Id" IN ({string.Join(", ", NewEntries.Select(e => $"'{e.Id}'"))});
                """);

            // Traps and Neck no longer exist: closest group from before.
            migrationBuilder.Sql("""
                UPDATE exercise_definitions SET "MuscleGroup" = 'Upper Back' WHERE "MuscleGroup" IN ('Traps', 'Neck');
                UPDATE exercises SET "MuscleGroup" = 'Upper Back' WHERE "MuscleGroup" IN ('Traps', 'Neck');
                """);
        }

        // All values come from the constants in this file (no quotes inside), so
        // building the SQL by string concatenation is safe.
        private static IEnumerable<(string Id, string Group, decimal Share)> ShareRows() =>
            from entry in Shares
            from part in entry.Muscles.Split(", ")
            let space = part.LastIndexOf(' ')
            select (entry.Id, part[..space], decimal.Parse(part[(space + 1)..], CultureInfo.InvariantCulture));
    }
}
