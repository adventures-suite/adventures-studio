using System.Collections.Frozen;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Companion.Application;

/// <summary>Names fields that a closed Companion information profile may expose.</summary>
public enum CompanionInformationField
{
    /// <summary>The opaque Adventure identity.</summary>
    AdventureId,
    /// <summary>The explicitly approved traveler-visible Adventure title.</summary>
    AdventureTitle,
    /// <summary>The traveler-facing Adventure status.</summary>
    AdventureStatus,
    /// <summary>The Adventure-local start and end dates.</summary>
    AdventureDates,
    /// <summary>The primary IANA time-zone identifier.</summary>
    PrimaryTimeZone,
    /// <summary>Derived countdown inputs without persisted ticks or invented times.</summary>
    CountdownInputs,
    /// <summary>The opaque destination-visit identity.</summary>
    DestinationVisitId,
    /// <summary>The explicitly approved traveler-visible destination name.</summary>
    DestinationName,
    /// <summary>The destination visit's local start and end dates.</summary>
    DestinationDates,
    /// <summary>The destination visit's IANA time-zone identifier.</summary>
    DestinationTimeZone,
    /// <summary>The stable destination presentation sequence.</summary>
    DestinationSequence
}

/// <summary>Defines one immutable code-reviewed Companion field profile.</summary>
public sealed record CompanionInformationProfile
{
    /// <summary>Initializes a closed profile definition.</summary>
    public CompanionInformationProfile(
        string key,
        long definitionVersion,
        IEnumerable<CompanionInformationField> allowedFields,
        bool usesGenericDescription,
        bool usesGenericReadiness,
        bool includesCapabilityLinks,
        bool includesHeroResources,
        bool includesNextItem)
    {
        if (string.IsNullOrWhiteSpace(key)
            || key.Length > 64
            || key != key.Trim()
            || key.Any(character => character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9') and not '_'))
            throw new ArgumentException("A closed lowercase profile key is required.", nameof(key));
        if (definitionVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(definitionVersion));

        var fields = (allowedFields ?? throw new ArgumentNullException(nameof(allowedFields))).ToFrozenSet();
        if (fields.Count == 0 || fields.Any(field => !Enum.IsDefined(field)))
            throw new ArgumentException("At least one known field is required.", nameof(allowedFields));

        Key = key;
        DefinitionVersion = definitionVersion;
        AllowedFields = fields;
        UsesGenericDescription = usesGenericDescription;
        UsesGenericReadiness = usesGenericReadiness;
        IncludesCapabilityLinks = includesCapabilityLinks;
        IncludesHeroResources = includesHeroResources;
        IncludesNextItem = includesNextItem;
    }

    /// <summary>Gets the closed profile key.</summary>
    public string Key { get; }
    /// <summary>Gets the positive code-definition version.</summary>
    public long DefinitionVersion { get; }
    /// <summary>Gets the exact field allowlist.</summary>
    public IReadOnlySet<CompanionInformationField> AllowedFields { get; }
    /// <summary>Gets whether detail uses a generic non-plan description.</summary>
    public bool UsesGenericDescription { get; }
    /// <summary>Gets whether detail uses a generic non-sensitive readiness summary.</summary>
    public bool UsesGenericReadiness { get; }
    /// <summary>Gets whether capability links may be emitted.</summary>
    public bool IncludesCapabilityLinks { get; }
    /// <summary>Gets whether hero Resource metadata may be emitted.</summary>
    public bool IncludesHeroResources { get; }
    /// <summary>Gets whether a next-item summary may be emitted.</summary>
    public bool IncludesNextItem { get; }
}

/// <summary>Resolves only code-defined Companion information profiles.</summary>
public interface ICompanionInformationProfileCatalog
{
    /// <summary>Gets an exact profile without normalization or fallback.</summary>
    bool TryGet(string key, out CompanionInformationProfile? profile);
}

/// <summary>Provides the single approved v1 Adventure-overview profile.</summary>
public sealed class CompanionInformationProfileCatalog : ICompanionInformationProfileCatalog
{
    /// <summary>Gets the only approved v1 profile key.</summary>
    public const string AdventureOverviewV1 = "companion_adventure_overview_v1";

    private static readonly CompanionInformationProfile Profile = new(
        AdventureOverviewV1,
        definitionVersion: 1,
        allowedFields:
        [
            CompanionInformationField.AdventureId,
            CompanionInformationField.AdventureTitle,
            CompanionInformationField.AdventureStatus,
            CompanionInformationField.AdventureDates,
            CompanionInformationField.PrimaryTimeZone,
            CompanionInformationField.CountdownInputs,
            CompanionInformationField.DestinationVisitId,
            CompanionInformationField.DestinationName,
            CompanionInformationField.DestinationDates,
            CompanionInformationField.DestinationTimeZone,
            CompanionInformationField.DestinationSequence
        ],
        usesGenericDescription: true,
        usesGenericReadiness: true,
        includesCapabilityLinks: false,
        includesHeroResources: false,
        includesNextItem: false);

    /// <inheritdoc />
    public bool TryGet(string key, out CompanionInformationProfile? profile)
    {
        if (string.Equals(key, AdventureOverviewV1, StringComparison.Ordinal))
        {
            profile = Profile;
            return true;
        }

        profile = null;
        return false;
    }
}

/// <summary>Classifies the lifecycle of one explicit Adventure/traveler profile assignment.</summary>
public enum CompanionInformationPolicyAssignmentStatus
{
    /// <summary>The assignment may be evaluated inside its effective window.</summary>
    Active,
    /// <summary>The assignment was explicitly revoked and cannot authorize disclosure.</summary>
    Revoked
}

/// <summary>Represents an explicit Creator-owned profile assignment to one participation.</summary>
public sealed record CompanionInformationPolicyAssignment
{
    /// <summary>Initializes one exact, versioned assignment.</summary>
    public CompanionInformationPolicyAssignment(
        CreatorId creatorId,
        string adventureId,
        string travelerId,
        long participationVersion,
        string profileKey,
        long version,
        CompanionInformationPolicyAssignmentStatus status,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset? expiresAtUtc = null)
    {
        if (creatorId == default)
            throw new ArgumentException("A Creator identity is required.", nameof(creatorId));
        AdventureId = RequireIdentity(adventureId, nameof(adventureId));
        TravelerId = RequireIdentity(travelerId, nameof(travelerId));
        if (participationVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(participationVersion));
        if (string.IsNullOrWhiteSpace(profileKey) || profileKey.Length > 64 || profileKey != profileKey.Trim())
            throw new ArgumentException("A bounded exact profile key is required.", nameof(profileKey));
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (effectiveFromUtc.Offset != TimeSpan.Zero
            || (expiresAtUtc.HasValue && expiresAtUtc.Value.Offset != TimeSpan.Zero)
            || (expiresAtUtc.HasValue && expiresAtUtc.Value <= effectiveFromUtc))
            throw new ArgumentException("Assignment timestamps must be ordered UTC values.");

        CreatorId = creatorId;
        ParticipationVersion = participationVersion;
        ProfileKey = profileKey;
        Version = version;
        Status = status;
        EffectiveFromUtc = effectiveFromUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the owning Creator.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the exact Adventure identity.</summary>
    public string AdventureId { get; }
    /// <summary>Gets the exact traveler identity.</summary>
    public string TravelerId { get; }
    /// <summary>Gets the participation version to which the assignment was approved.</summary>
    public long ParticipationVersion { get; }
    /// <summary>Gets the exact code-defined profile key.</summary>
    public string ProfileKey { get; }
    /// <summary>Gets the positive assignment version.</summary>
    public long Version { get; }
    /// <summary>Gets the assignment lifecycle.</summary>
    public CompanionInformationPolicyAssignmentStatus Status { get; }
    /// <summary>Gets when the assignment becomes effective in UTC.</summary>
    public DateTimeOffset EffectiveFromUtc { get; }
    /// <summary>Gets when the assignment expires in UTC, when bounded.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; }

    private static string RequireIdentity(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > CompanionAccessContextLimits.MaximumAdventureIdLength
            || value != value.Trim()
            || value.Any(char.IsControl))
            throw new ArgumentException("A bounded exact identity is required.", parameterName);
        return value;
    }
}

/// <summary>Loads an explicit assignment without defining persistence technology.</summary>
public interface ICompanionInformationPolicyAssignmentProvider
{
    /// <summary>Gets the exact assignment for one Adventure/traveler participation.</summary>
    Task<CompanionInformationPolicyAssignment?> GetAsync(
        CreatorId creatorId,
        string adventureId,
        string travelerId,
        CancellationToken cancellationToken = default);
}

/// <summary>Keeps assignment lookup closed until a separately reviewed persistence slice.</summary>
public sealed class ClosedCompanionInformationPolicyAssignmentProvider
    : ICompanionInformationPolicyAssignmentProvider
{
    /// <inheritdoc />
    public Task<CompanionInformationPolicyAssignment?> GetAsync(
        CreatorId creatorId,
        string adventureId,
        string travelerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<CompanionInformationPolicyAssignment?>(null);
    }
}

/// <summary>Evaluates only exact, active assignments to code-defined profiles.</summary>
public sealed class AssignedCompanionInformationPolicy : ICompanionInformationPolicy
{
    private readonly ICompanionInformationProfileCatalog catalog;
    private readonly ICompanionInformationPolicyAssignmentProvider assignments;

    /// <summary>Initializes the closed evaluator with code catalog and authoritative assignments.</summary>
    public AssignedCompanionInformationPolicy(
        ICompanionInformationProfileCatalog catalog,
        ICompanionInformationPolicyAssignmentProvider assignments)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.assignments = assignments ?? throw new ArgumentNullException(nameof(assignments));
    }

    /// <inheritdoc />
    public async Task<CompanionInformationPolicyDecision> EvaluateAsync(
        CompanionInformationPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var assignment = await assignments.GetAsync(
            request.CreatorId,
            request.AdventureId,
            request.TravelerId,
            cancellationToken);
        if (assignment is null
            || request.RequiredPermission != Permissions.AdventurePlanView
            || assignment.CreatorId != request.CreatorId
            || !string.Equals(assignment.AdventureId, request.AdventureId, StringComparison.Ordinal)
            || !string.Equals(assignment.TravelerId, request.TravelerId, StringComparison.Ordinal)
            || assignment.ParticipationVersion != request.ParticipationVersion
            || assignment.Status != CompanionInformationPolicyAssignmentStatus.Active
            || request.EvaluatedAtUtc.Offset != TimeSpan.Zero
            || request.EvaluatedAtUtc < assignment.EffectiveFromUtc
            || (assignment.ExpiresAtUtc.HasValue && request.EvaluatedAtUtc >= assignment.ExpiresAtUtc.Value)
            || !catalog.TryGet(assignment.ProfileKey, out var profile)
            || profile is null)
        {
            return CompanionInformationPolicyDecision.Closed;
        }

        return CompanionInformationPolicyDecision.Allow(
            $"info_overview_v1_d{profile.DefinitionVersion}_a{assignment.Version}");
    }
}
