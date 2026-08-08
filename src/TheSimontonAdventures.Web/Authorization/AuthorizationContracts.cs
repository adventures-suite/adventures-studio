namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Classifies a safe, non-disclosing authorization denial.</summary>
public enum AuthorizationDenialReason
{
    /// <summary>No usable actor identity was supplied.</summary>
    Unauthenticated,
    /// <summary>The actor has no active membership applicable to the Creator.</summary>
    MembershipRequired,
    /// <summary>The actor lacks the requested operation permission.</summary>
    PermissionRequired,
    /// <summary>Authoritative ownership does not match the requested scope.</summary>
    ResourceScopeMismatch,
    /// <summary>The membership or delegated relationship is stale or inactive.</summary>
    AccessRevoked,
    /// <summary>The caller's authorization state is older than authoritative membership state.</summary>
    StaleAuthorizationContext,
    /// <summary>The operation requires a human actor.</summary>
    HumanActorRequired,
    /// <summary>This policy slice has no approved authorization basis for the actor type.</summary>
    ActorTypeUnsupported,
    /// <summary>The request is invalid without revealing protected resource state.</summary>
    InvalidRequest
}

/// <summary>Describes one provider-independent authorization evaluation.</summary>
public sealed record AuthorizationRequest
{
    /// <summary>Initializes a protected operation request.</summary>
    public AuthorizationRequest(
        ActorIdentity? actor,
        Permission permission,
        AuthorizationResourceScope resource,
        ActorIdentity? initiatingActor = null,
        long? membershipVersion = null)
    {
        Actor = actor;
        if (permission == default)
        {
            throw new ArgumentException("A valid permission is required.", nameof(permission));
        }

        Permission = permission;
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        if (initiatingActor is not null && initiatingActor.Type != ActorType.Human)
        {
            throw new ArgumentException(
                "An initiating actor must be an ordinary authenticated human.",
                nameof(initiatingActor));
        }

        if (actor is null && initiatingActor is not null)
        {
            throw new ArgumentException(
                "An unauthenticated request cannot carry an initiating actor.",
                nameof(initiatingActor));
        }

        if (initiatingActor is not null
            && actor!.Type is not (ActorType.System or ActorType.BackgroundJob))
        {
            throw new ArgumentException(
                "Initiating-human attribution is valid only for system or background execution.",
                nameof(initiatingActor));
        }

        InitiatingActor = initiatingActor;
        if (membershipVersion is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(membershipVersion),
                "Membership versions must be positive when supplied.");
        }

        MembershipVersion = membershipVersion;
    }

    /// <summary>Gets the principal performing the operation.</summary>
    public ActorIdentity? Actor { get; }
    /// <summary>Gets the operation permission being evaluated.</summary>
    public Permission Permission { get; }
    /// <summary>Gets the Creator-owned collection or instance scope.</summary>
    public AuthorizationResourceScope Resource { get; }
    /// <summary>Gets the human who initiated non-human work, when applicable.</summary>
    public ActorIdentity? InitiatingActor { get; }
    /// <summary>Gets the membership version observed by the caller, when applicable.</summary>
    public long? MembershipVersion { get; }
}

/// <summary>Describes durable audit behavior required after authorization.</summary>
public enum AuthorizationAuditRequirement
{
    /// <summary>No durable audit intent is required by this policy.</summary>
    None,
    /// <summary>The protected mutation and audit intent must commit atomically.</summary>
    RequiredMutation,
    /// <summary>The sensitive read must fail closed if audit intent cannot be recorded.</summary>
    RequiredSensitiveRead
}

/// <summary>Returns an authorization outcome without protected resource details.</summary>
public sealed record AuthorizationDecision
{
    private AuthorizationDecision(
        bool isAllowed,
        AuthorizationDenialReason? denialReason,
        AuthorizationAuditRequirement auditRequirement)
    {
        IsAllowed = isAllowed;
        DenialReason = denialReason;
        AuditRequirement = auditRequirement;
    }

    /// <summary>Gets whether the requested operation is authorized.</summary>
    public bool IsAllowed { get; }
    /// <summary>Gets a safe reason category when authorization is denied.</summary>
    public AuthorizationDenialReason? DenialReason { get; }
    /// <summary>Gets the durable audit behavior required for an allowed operation.</summary>
    public AuthorizationAuditRequirement AuditRequirement { get; }

    /// <summary>Creates an allowed decision.</summary>
    public static AuthorizationDecision Allow(
        AuthorizationAuditRequirement auditRequirement = AuthorizationAuditRequirement.None)
    {
        if (!Enum.IsDefined(auditRequirement))
        {
            throw new ArgumentOutOfRangeException(nameof(auditRequirement));
        }

        return new(true, null, auditRequirement);
    }

    /// <summary>Creates a denied decision with a non-disclosing reason.</summary>
    public static AuthorizationDecision Deny(AuthorizationDenialReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return new(false, reason, AuthorizationAuditRequirement.None);
    }
}

/// <summary>Evaluates resource-aware authorization independently of any web framework.</summary>
public interface IAuthorizationPolicyEvaluator
{
    /// <summary>Evaluates one explicit actor, permission, and resource scope.</summary>
    Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
