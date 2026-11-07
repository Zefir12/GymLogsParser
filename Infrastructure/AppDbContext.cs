using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<People> WorkoutPersons => Set<People>();
    public DbSet<Exercise> WorkoutExercises => Set<Exercise>();
    public DbSet<Set> WorkoutSets => Set<Set>();
    public DbSet<Cardio> CardioEntries => Set<Cardio>();
    public DbSet<ExerciseDefinitionEntity> ExerciseDefinitions => Set<ExerciseDefinitionEntity>();
    public DbSet<MuscleTargetEntity> MuscleTargets => Set<MuscleTargetEntity>();
    public DbSet<BodyweightEntryEntity> BodyweightEntries => Set<BodyweightEntryEntity>();

    /// <summary>Length of the <c>OwnerId</c> columns (the OIDC <c>sub</c>).</summary>
    public const int OwnerIdMaxLength = 255;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Workout>(entity =>
        {
            entity.ToTable("workouts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id) .ValueGeneratedOnAdd();
            entity.Property(x => x.OwnerId).HasMaxLength(OwnerIdMaxLength).IsRequired();
            entity.Property(x => x.Title) .HasMaxLength(200);
            entity.Property(x => x.Notes);
            entity.HasMany(x => x.Persons).WithOne(x => x.Workout).HasForeignKey(x => x.WorkoutId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Exercises).WithOne(x => x.Workout).HasForeignKey(x => x.WorkoutId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.OwnerId, x.Date });
        });

        modelBuilder.Entity<People>(entity =>
        {
            entity.ToTable("people");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.WorkoutId);
        });

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("exercises");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExerciseId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MuscleGroup).HasMaxLength(100);
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(x => x.WorkoutId);
            entity.HasMany(x => x.Sets).WithOne(x => x.WorkoutExercise).HasForeignKey(x => x.WorkoutExerciseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Cardio).WithOne(x => x.WorkoutExercise).HasForeignKey(x => x.WorkoutExerciseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Set>(entity =>
        {
            entity.ToTable("sets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Weight).HasPrecision(10, 2);
            entity.Property(x => x.Rir).HasPrecision(5, 2);
            entity.Property(x => x.Rpe).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.WorkoutExerciseId, x.SetNumber });
        });

        modelBuilder.Entity<Cardio>(entity =>
        {
            entity.ToTable("cardio");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Activity).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DistanceKm).HasPrecision(10, 3);
            entity.Property(x => x.SpeedKmh).HasPrecision(10, 3);
            entity.Property(x => x.InclinePercent).HasPrecision(10, 3);
            entity.HasIndex(x => x.WorkoutExerciseId);
        });

        modelBuilder.Entity<ExerciseDefinitionEntity>(entity =>
        {
            entity.ToTable("exercise_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.MuscleGroup).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Aliases).HasColumnType("text[]");
            entity.HasMany(x => x.Muscles).WithOne().HasForeignKey(x => x.ExerciseDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExerciseDefinitionMuscleEntity>(entity =>
        {
            entity.ToTable("exercise_definition_muscles");
            entity.HasKey(x => new { x.ExerciseDefinitionId, x.MuscleGroup });
            entity.Property(x => x.ExerciseDefinitionId).HasMaxLength(100);
            entity.Property(x => x.MuscleGroup).HasMaxLength(100);
            entity.Property(x => x.Share).HasPrecision(3, 2);
        });

        modelBuilder.Entity<MuscleTargetEntity>(entity =>
        {
            entity.ToTable("muscle_targets");
            entity.HasKey(x => new { x.OwnerId, x.MuscleGroup });
            entity.Property(x => x.OwnerId).HasMaxLength(OwnerIdMaxLength);
            entity.Property(x => x.MuscleGroup).HasMaxLength(100);
        });

        modelBuilder.Entity<BodyweightEntryEntity>(entity =>
        {
            entity.ToTable("bodyweight_entries");
            entity.HasKey(x => new { x.OwnerId, x.Date });
            entity.Property(x => x.OwnerId).HasMaxLength(OwnerIdMaxLength);
            entity.Property(x => x.WeightKg).HasPrecision(5, 2);
        });
    }
}