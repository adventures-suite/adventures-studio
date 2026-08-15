using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Companion.Application;

/// <summary>Defines conservative bounds for authoritative Companion identity resolution.</summary>
public static class CompanionAccessContextLimits
{
    /// <summary>Gets the maximum supported opaque Adventure identity length.</summary>
    public const int MaximumAdventureIdLength = 64;

    /// <summary>Gets the maximum number of rows accepted for a supposedly unique lookup.</summary>
    public const int MaximumUniqueLookupRows = 2;
}

/// <summary>Contains only the configured provider and validated external identity key.</summary>
public sealed record CompanionExternalIdentity
{
    /// <summary>Initializes a provider-neutral, exact external identity.</summary>
    public CompanionExternalIdentity(
        ExternalIdentityProviderId providerId,
        ExternalIdentityIssuer issuer,
        ExternalIdentitySubject subject)
    {
        if (providerId == default || issuer == default || subject == default)
            throw new ArgumentException("A configured provider and validated issuer and subject are required.");

        ProviderId = providerId;
        Issuer = issuer;
        Subject = subject;
    }

    /// <summary>Gets the configured identity-provider adapter.</summary>
    public ExternalIdentityProviderId ProviderId { get; }

    /// <summary>Gets the exact, case-sensitive issuer.</summary>
    public ExternalIdentityIssuer Issuer { get; }

    /// <summary>Gets the exact, case-sensitive subject.</summary>
    public ExternalIdentitySubject Subject { get; }
}

/// <summary>Classifies a closed authoritative access-context resolution.</summary>
public enum CompanionAccessContextOutcome
{
    /// <summary>Every authoritative fact and policy check succeeded.</summary>
    Resolved,
    /// <summary>No exact external identity mapping exists.</summary>
    Unmapped,
    /// <summary>The external identity or platform user is disabled.</summary>
    Disabled,
    /// <summary>A membership or participation was explicitly revoked.</summary>
    Revoked,
    /// <summary>A required relationship is not currently effective or active.</summary>
    Inactive,
    /// <summary>The actor lacks an accepted relationship or required permission.</summary>
    Unauthorized,
    /// <summary>More than one authoritative candidate matched a unique operation.</summary>
    Ambiguous,
    /// <summary>Authoritative persistence data was unsupported or contradictory.</summary>
    Malformed,
    /// <summary>The authoritative store could not safely complete the operation.</summary>
    OperationallyUnavailable,
    /// <summary>No approved information policy currently permits this projection.</summary>
    InformationPolicyClosed
}

/// <summary>Contains server-owned authorization facts for one Adventure operation.</summary>
public sealed record CompanionAuthoritativeAccessContext(
    UserId UserId,
    long UserSecurityVersion,
    CreatorId CreatorId,
    string AdventureId,
    string TravelerId,
    long MembershipVersion,
    long ParticipationVersion,
    Permission RequiredPermission,
    string InformationPolicyVersion,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>Represents a closed, enumeration-safe access-context result.</summary>
public sealed record CompanionAccessContextResolution
{
    private CompanionAccessContextResolution(
        CompanionAccessContextOutcome outcome,
        CompanionAuthoritativeAccessContext? context)
    {
        if (!Enum.IsDefined(outcome)
            || (outcome == CompanionAccessContextOutcome.Resolved) != (context is not null))
            throw new ArgumentException("A resolved outcome requires exactly one authoritative context.");

        Outcome = outcome;
        Context = context;
    }

    /// <summary>Gets the closed resolution classification.</summary>
    public CompanionAccessContextOutcome Outcome { get; }

    /// <summary>Gets the context only when every check succeeded.</summary>
    public CompanionAuthoritativeAccessContext? Context { get; }

    /// <summary>Creates one successful resolution.</summary>
    public static CompanionAccessContextResolution Resolved(CompanionAuthoritativeAccessContext context) =>
        new(CompanionAccessContextOutcome.Resolved, context ?? throw new ArgumentNullException(nameof(context)));

    /// <summary>Creates one closed resolution without carrying private facts.</summary>
    public static CompanionAccessContextResolution Closed(CompanionAccessContextOutcome outcome) =>
        outcome == CompanionAccessContextOutcome.Resolved
            ? throw new ArgumentException("A resolved result requires a context.", nameof(outcome))
            : new(outcome, null);
}

/// <summary>Contains policy input that has already passed authoritative persistence checks.</summary>
public sealed record CompanionInformationPolicyRequest(
    UserId UserId,
    CreatorId CreatorId,
    string AdventureId,
    string TravelerId,
    long MembershipVersion,
    long ParticipationVersion,
    Permission RequiredPermission,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>Represents an approved, versioned information-policy decision.</summary>
public sealed record CompanionInformationPolicyDecision(bool IsAllowed, string? Version)
{
    /// <summary>Creates a closed decision.</summary>
    public static CompanionInformationPolicyDecision Closed { get; } = new(false, null);
}

/// <summary>Evaluates which minimized information profile may be projected.</summary>
public interface ICompanionInformationPolicy
{
    /// <summary>Evaluates one already-authorized Adventure read.</summary>
    Task<CompanionInformationPolicyDecision> EvaluateAsync(
        CompanionInformationPolicyRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Keeps authoritative projection information closed until policy is approved.</summary>
public sealed class ClosedCompanionInformationPolicy : ICompanionInformationPolicy
{
    /// <inheritdoc />
    public Task<CompanionInformationPolicyDecision> EvaluateAsync(
        CompanionInformationPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CompanionInformationPolicyDecision.Closed);
    }
}

/// <summary>Resolves exact external identity to server-owned Adventure authorization facts.</summary>
public interface ICompanionAuthoritativeAccessContextResolver
{
    /// <summary>Resolves one Adventure without revealing unavailable resource existence.</summary>
    Task<CompanionAccessContextResolution> ResolveAdventureAsync(
        CompanionExternalIdentity identity,
        string adventureId,
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Rechecks the versioned authorization facts in the eventual projection-read boundary.
/// Implementations must not rely on a previously resolved context alone.
/// </summary>
public interface ICompanionProjectionAuthorizationRecheck
{
    /// <summary>Revalidates all server-owned facts while reading an authorized projection.</summary>
    Task<bool> IsCurrentAsync(
        CompanionAuthoritativeAccessContext context,
        CancellationToken cancellationToken = default);
}
