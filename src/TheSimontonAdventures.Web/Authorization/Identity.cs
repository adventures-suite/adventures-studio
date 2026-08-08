namespace TheSimontonAdventures.Web.Authorization;

internal static class AuthorizationIdentity
{
    public static string Require(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 3 or > 64
            || value[0] is < 'a' or > 'z'
            || value.Any(character => character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9') and not '_'))
        {
            throw new ArgumentException(
                "Authorization identities must contain 3-64 lowercase letters, digits, or underscores and begin with a letter.",
                parameterName);
        }

        return value;
    }
}

/// <summary>Identifies one human platform user independently of provider claims.</summary>
public readonly record struct UserId
{
    /// <summary>Initializes a stable platform user identity.</summary>
    public UserId(string value) => Value = AuthorizationIdentity.Require(value, nameof(value));

    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one revocable relationship between a user and Creator.</summary>
public readonly record struct CreatorMembershipId
{
    /// <summary>Initializes a stable Creator membership identity.</summary>
    public CreatorMembershipId(string value) => Value = AuthorizationIdentity.Require(value, nameof(value));

    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Classifies the principal performing a protected operation.</summary>
public enum ActorType
{
    /// <summary>An authenticated human user.</summary>
    Human,
    /// <summary>An AdventuresSuite system process without a human identity.</summary>
    System,
    /// <summary>A queued or scheduled background operation.</summary>
    BackgroundJob,
    /// <summary>An authenticated support user acting under elevated controls.</summary>
    Support
}

/// <summary>Identifies the human or non-human principal performing an operation.</summary>
public sealed record ActorIdentity
{
    /// <summary>Initializes and validates an actor identity.</summary>
    public ActorIdentity(ActorType type, string actorId, UserId? userId = null)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        var isHumanActor = type is ActorType.Human or ActorType.Support;
        if ((isHumanActor && (!userId.HasValue || userId.Value == default))
            || (!isHumanActor && userId.HasValue))
        {
            throw new ArgumentException(
                "Human and support actors require a User identity; system and background actors cannot carry one.",
                nameof(userId));
        }

        Type = type;
        ActorId = AuthorizationIdentity.Require(actorId, nameof(actorId));
        UserId = userId;
    }

    /// <summary>Gets the actor classification.</summary>
    public ActorType Type { get; }

    /// <summary>Gets the stable actor identity used in authorization and audit.</summary>
    public string ActorId { get; }

    /// <summary>Gets the human platform identity when this actor represents a person.</summary>
    public UserId? UserId { get; }

    /// <summary>Gets whether this actor is backed by an authenticated person.</summary>
    public bool RepresentsPerson => Type is ActorType.Human or ActorType.Support;

    /// <summary>
    /// Gets whether this is an ordinary human actor eligible for customer
    /// consent, approval, and other human-only decisions.
    /// </summary>
    public bool IsHuman => Type == ActorType.Human;
}
