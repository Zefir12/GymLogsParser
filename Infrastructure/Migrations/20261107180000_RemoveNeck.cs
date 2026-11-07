using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Removes the Neck muscle group (#70); data only, the model doesn't change.
    /// Catalog entries with Neck as their primary group (neck-curl and neck-extension,
    /// seeded by #35, plus any the user added) move to Traps, the closest group that
    /// still exists: catalog IDs never change and there is no delete. Logged exercises
    /// stored as Neck move to Traps too. Neck secondary shares are deleted, as are
    /// secondary shares that now repeat an entry's new primary group (Traps), and every
    /// user's Neck weekly target.
    /// </summary>
    public partial class RemoveNeck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE exercise_definitions SET "MuscleGroup" = 'Traps' WHERE "MuscleGroup" = 'Neck';

                DELETE FROM exercise_definition_muscles WHERE "MuscleGroup" = 'Neck';

                DELETE FROM exercise_definition_muscles AS m
                USING exercise_definitions AS d
                WHERE m."ExerciseDefinitionId" = d."Id"
                  AND m."MuscleGroup" = d."MuscleGroup";

                UPDATE exercises SET "MuscleGroup" = 'Traps' WHERE "MuscleGroup" = 'Neck';

                DELETE FROM muscle_targets WHERE "MuscleGroup" = 'Neck';
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Gives every user who has target rows the old Neck default (0 to 8 sets) and
        /// moves neck exercises back by the rule #35 used to find them (ID starts with
        /// <c>neck-</c>, or the name starts with "neck " or contains "szyj"); logged
        /// exercises follow their catalog entry. Deleted secondary shares are not restored.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO muscle_targets ("OwnerId", "MuscleGroup", "MinSets", "MaxSets")
                SELECT DISTINCT "OwnerId", 'Neck', 0, 8 FROM muscle_targets
                ON CONFLICT DO NOTHING;

                UPDATE exercise_definitions
                SET "MuscleGroup" = 'Neck'
                WHERE "Category" = 'Strength'
                  AND "MuscleGroup" = 'Traps'
                  AND ("Id" LIKE 'neck-%' OR lower("Name") LIKE 'neck %' OR lower("Name") LIKE '%szyj%');

                UPDATE exercises AS e
                SET "MuscleGroup" = 'Neck'
                FROM exercise_definitions AS d
                WHERE e."ExerciseId" = d."Id"
                  AND d."MuscleGroup" = 'Neck'
                  AND e."MuscleGroup" = 'Traps';
                """);
        }
    }
}
