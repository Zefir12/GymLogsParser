using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Weekly target ranges per muscle group (#37). New table <c>muscle_targets</c>
    /// (muscle group, min and max working sets per week), seeded with zefir's defaults
    /// from #35. Cardio and Warm-up have no target.
    /// </summary>
    public partial class MuscleTargets : Migration
    {
        /// <summary>
        /// A copy of Core.Models.MuscleTargetDefaults as of this migration, so later
        /// changes to the defaults don't change what this migration seeds.
        /// </summary>
        private static readonly (string Group, int Min, int Max)[] Defaults =
        [
            ("Chest",       10, 20),
            ("Lats",        10, 20),
            ("Upper Back",  10, 20),
            ("Traps",        4, 12),
            ("Lower Back",   4, 10),
            ("Front Delts",  6, 12),
            ("Side Delts",  10, 20),
            ("Rear Delts",   8, 16),
            ("Biceps",       8, 16),
            ("Triceps",      8, 16),
            ("Forearms",     4, 10),
            ("Abs",          6, 12),
            ("Quadriceps",  10, 20),
            ("Hamstrings",   8, 16),
            ("Glutes",       8, 16),
            ("Calves",       8, 16),
            ("Adductors",    4, 10),
            ("Abductors",    4, 10),
            ("Neck",         0,  8)
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "muscle_targets",
                columns: table => new
                {
                    MuscleGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinSets = table.Column<int>(type: "integer", nullable: false),
                    MaxSets = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_muscle_targets", x => x.MuscleGroup);
                });

            // All values are constants from this file (no quotes inside), so building
            // the SQL by string concatenation is safe.
            migrationBuilder.Sql($"""
                INSERT INTO muscle_targets ("MuscleGroup", "MinSets", "MaxSets")
                VALUES {string.Join(", ", Defaults.Select(d => $"('{d.Group}', {d.Min}, {d.Max})"))};
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "muscle_targets");
        }
    }
}
