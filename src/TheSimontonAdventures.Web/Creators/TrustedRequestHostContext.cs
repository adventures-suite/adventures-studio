namespace TheSimontonAdventures.Web.Creators;

/// <summary>Classifies one request after exact trusted-host resolution.</summary>
public enum TrustedRequestHostType
{
    /// <summary>The request may serve published content for one Creator.</summary>
    PublicCreator,

    /// <summary>The request may enter the private platform workspace pipeline.</summary>
    PlatformWorkspace
}

/// <summary>Contains the immutable trusted-host classification for one request.</summary>
public sealed record TrustedRequestHostContext
{
    /// <summary>Initializes a validated request-host classification.</summary>
    public TrustedRequestHostContext(
        TrustedRequestHostType type,
        CreatorContext? creator = null)
    {
        if (!Enum.IsDefined(type)
            || (type == TrustedRequestHostType.PublicCreator && creator is null)
            || (type == TrustedRequestHostType.PlatformWorkspace && creator is not null))
        {
            throw new ArgumentException("The trusted-host classification is inconsistent.");
        }

        Type = type;
        Creator = creator;
    }

    /// <summary>Gets the trusted request-host category.</summary>
    public TrustedRequestHostType Type { get; }

    /// <summary>Gets the public Creator context, or null for the workspace.</summary>
    public CreatorContext? Creator { get; }
}

/// <summary>Provides request-scoped access to the established trusted-host classification.</summary>
public interface ITrustedRequestHostContextAccessor
{
    /// <summary>Gets the current classification after host middleware succeeds.</summary>
    TrustedRequestHostContext Current { get; }
}

/// <summary>Stores one trusted-host classification for the current request scope.</summary>
public sealed class TrustedRequestHostContextAccessor : ITrustedRequestHostContextAccessor
{
    private TrustedRequestHostContext? current;

    /// <inheritdoc />
    public TrustedRequestHostContext Current => current
        ?? throw new InvalidOperationException("Trusted request-host context has not been established.");

    /// <summary>Gets whether the current request has already been classified.</summary>
    public bool IsEstablished => current is not null;

    /// <summary>Establishes the immutable classification exactly once.</summary>
    public void Establish(TrustedRequestHostContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (current is not null)
        {
            throw new InvalidOperationException("Trusted request-host context is already established.");
        }

        current = context;
    }
}
