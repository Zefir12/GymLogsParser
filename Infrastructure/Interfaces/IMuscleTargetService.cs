using Core.Models;

namespace Infrastructure.Interfaces;

/// <summary>Each user's weekly target ranges per muscle group, stored in <c>muscle_targets</c>.</summary>
public interface IMuscleTargetService
{
    /// <summary>
    /// Every muscle group (not Cardio or Warm-up) in display order, with its stored
    /// range for <paramref name="ownerId"/>, or its default when nothing is stored for it.
    /// </summary>
    Task<IReadOnlyList<MuscleTarget>> GetAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="ownerId"/>'s ranges of the given groups; groups not listed keep theirs. The caller
    /// has validated them. Returns the full list, like <see cref="GetAsync"/>.
    /// </summary>
    Task<IReadOnlyList<MuscleTarget>> UpdateAsync(string ownerId, IReadOnlyList<MuscleTarget> targets, CancellationToken cancellationToken = default);
}
