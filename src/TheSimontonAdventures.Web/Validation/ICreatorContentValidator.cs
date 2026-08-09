using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Validation;

/// <summary>Validates content and media within an explicit Creator boundary.</summary>
public interface ICreatorContentValidator
{
    /// <summary>Validates one Creator's immutable deployed content snapshot.</summary>
    /// <param name="creatorId">The Creator identity defining the boundary.</param>
    /// <param name="cancellationToken">A token used to cancel validation.</param>
    /// <returns>The complete Creator-scoped validation result.</returns>
    Task<CreatorContentValidationResult> ValidateAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);
}
