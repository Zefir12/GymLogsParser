using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Per-user data (#45). Adds the owner (OIDC <c>sub</c>, column <c>OwnerId</c>) to
    /// <c>workouts</c> (people, exercises, sets and cardio hang off a workout) and to
    /// <c>muscle_targets</c> (key becomes owner + muscle group), and moves the bodyweight
    /// history, hard-coded in BodyweightHistory.cs until now, into the new table
    /// <c>bodyweight_entries</c>. Every existing row, and the seeded bodyweight history,
    /// gets the placeholder owner <c>legacy</c>; the API hands those rows to the user
    /// configured as <c>Ownership:LegacyOwner</c> on their first signed-in request
    /// (LegacyDataClaimService).
    /// </summary>
    public partial class PerUserData : Migration
    {
        /// <summary>A copy of LegacyDataClaimService.LegacyOwnerId as of this migration.</summary>
        private const string LegacyOwner = "legacy";

        /// <summary>
        /// A copy of the measurements hard-coded in Infrastructure/Helpers/BodyweightHistory.cs
        /// before this migration (zefir's): date and kilograms.
        /// </summary>
        private static readonly (string Date, string WeightKg)[] Bodyweights =
        [
            ("2018-12-17", "66.0"), ("2019-02-04", "66.4"), ("2019-04-03", "67.0"), ("2019-05-20", "65.1"),
            ("2019-08-02", "65.3"), ("2019-09-02", "66.1"), ("2019-10-19", "67.0"), ("2019-12-21", "65.0"),
            ("2020-07-26", "67.0"), ("2022-08-01", "70.05"), ("2022-08-18", "69.6"), ("2023-08-01", "70.4"),
            ("2023-08-10", "71.6"), ("2023-08-14", "72.2"), ("2023-08-20", "72.0"), ("2023-08-28", "73.8"),
            ("2023-09-03", "74.5"), ("2023-09-08", "75.4"), ("2023-09-13", "75.9"), ("2023-09-18", "76.0"),
            ("2023-09-23", "76.4"), ("2023-09-29", "76.9"), ("2023-10-04", "75.0"), ("2023-10-12", "74.6"),
            ("2023-10-20", "74.5"), ("2023-11-29", "73.0"), ("2024-01-11", "72.4"), ("2024-02-08", "72.9"),
            ("2024-02-22", "75.5"), ("2024-03-13", "73.3"), ("2024-04-17", "75.7"), ("2024-05-23", "78.2"),
            ("2024-06-19", "77.1"), ("2024-07-06", "78.8"), ("2024-08-11", "78.5"), ("2024-09-20", "81.2"),
            ("2024-10-16", "82.7"), ("2024-11-25", "82.3"), ("2024-12-30", "84.4"), ("2025-01-26", "85.3"),
            ("2025-02-19", "86.1"), ("2025-03-15", "84.9"), ("2025-04-17", "85.7"), ("2025-05-30", "87.7"),
            ("2025-06-14", "87.0"), ("2025-07-19", "86.7"), ("2025-08-14", "84.6"), ("2025-09-03", "83.7"),
            ("2025-10-27", "80.7"), ("2025-12-17", "84.4"), ("2026-01-21", "87.4"), ("2026-03-23", "89.1"),
            ("2026-05-04", "90.1"), ("2026-07-08", "91.9"), ("2026-07-23", "91.7"), ("2026-08-06", "91.0")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Workouts: existing rows become legacy, then the default goes so every new
            // row must name its owner.
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "workouts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: LegacyOwner);

            migrationBuilder.Sql("""ALTER TABLE workouts ALTER COLUMN "OwnerId" DROP DEFAULT;""");

            migrationBuilder.CreateIndex(
                name: "IX_workouts_OwnerId_Date",
                table: "workouts",
                columns: new[] { "OwnerId", "Date" });

            // Muscle targets: one range per user and muscle group.
            migrationBuilder.DropPrimaryKey(
                name: "PK_muscle_targets",
                table: "muscle_targets");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "muscle_targets",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: LegacyOwner);

            migrationBuilder.Sql("""ALTER TABLE muscle_targets ALTER COLUMN "OwnerId" DROP DEFAULT;""");

            migrationBuilder.AddPrimaryKey(
                name: "PK_muscle_targets",
                table: "muscle_targets",
                columns: new[] { "OwnerId", "MuscleGroup" });

            // Bodyweight history, seeded with the hard-coded measurements.
            migrationBuilder.CreateTable(
                name: "bodyweight_entries",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bodyweight_entries", x => new { x.OwnerId, x.Date });
                });

            // All values are constants from this file (no quotes inside), so building
            // the SQL by string concatenation is safe.
            migrationBuilder.Sql($"""
                INSERT INTO bodyweight_entries ("OwnerId", "Date", "WeightKg")
                VALUES {string.Join(", ", Bodyweights.Select(b => $"('{LegacyOwner}', DATE '{b.Date}', {b.WeightKg})"))};
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Goes back to one shared data set: every user's workouts are kept, and of the
        /// muscle targets only those of the owner with the most workouts survive (legacy if
        /// there are no workouts). The bodyweight table is dropped; the old code has the
        /// history hard-coded again.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bodyweight_entries");

            migrationBuilder.Sql($"""
                DELETE FROM muscle_targets
                WHERE "OwnerId" <> COALESCE(
                    (SELECT "OwnerId" FROM workouts GROUP BY "OwnerId" ORDER BY COUNT(*) DESC, "OwnerId" LIMIT 1),
                    '{LegacyOwner}');
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_muscle_targets",
                table: "muscle_targets");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "muscle_targets");

            migrationBuilder.AddPrimaryKey(
                name: "PK_muscle_targets",
                table: "muscle_targets",
                column: "MuscleGroup");

            migrationBuilder.DropIndex(
                name: "IX_workouts_OwnerId_Date",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "workouts");
        }
    }
}
