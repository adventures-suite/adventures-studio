using Microsoft.Extensions.Options;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>Defines explicitly approved hosts for the public AdventuresSuite product site.</summary>
public sealed class PlatformHostOptions
{
    /// <summary>Identifies the configuration section.</summary>
    public const string SectionName = "PlatformHosts";

    /// <summary>Gets public platform hosts approved for deployed environments.</summary>
    public string[] Hosts { get; init; } = [];

    /// <summary>Gets local public platform aliases used only in Development.</summary>
    public string[] DevelopmentHosts { get; init; } = [];

    /// <summary>Gets the optional public Creator experience featured by the platform site.</summary>
    public string FeaturedAdventureUrl { get; init; } = string.Empty;

    /// <summary>Gets the canonical Creator workspace sign-in URL shown on the public platform.</summary>
    public string WorkspaceSignInUrl { get; init; } = string.Empty;

    /// <summary>Gets the public platform image illustrating the beginning of an Adventure.</summary>
    public string JourneyImageUrl { get; init; } = "/images/platform/adventures-begin-passports.jpeg";

    /// <summary>Gets the public photograph shown in the platform hero.</summary>
    public string HeroImageUrl { get; init; } = "/images/platform/platform-hero-santorini.jpeg";

    /// <summary>Gets the public photograph accompanying the Adventures Studio story.</summary>
    public string StoryImageUrl { get; init; } = "/images/platform/adventures-studio-founders.jpeg";

    /// <summary>Gets the public preview image for the featured Creator experience.</summary>
    public string FeaturedImageUrl { get; init; } = "/images/platform/simonton-featured-experience.jpeg";
}

/// <summary>Classifies an exact request host as the public platform entrance.</summary>
public interface IPlatformHostClassifier
{
    /// <summary>Determines whether a host is explicitly approved for the public platform.</summary>
    bool IsPublicPlatformHost(HostString host);
}

/// <summary>Provides configuration-backed, fail-closed public platform host classification.</summary>
public sealed class PlatformHostClassifier : IPlatformHostClassifier
{
    private readonly HashSet<string> hosts;

    /// <summary>Initializes the classifier from environment-owned host configuration.</summary>
    public PlatformHostClassifier(
        IHostEnvironment environment,
        IOptions<PlatformHostOptions> options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddValidated(options.Value.Hosts);
        if (environment.IsDevelopment())
        {
            AddValidated(options.Value.DevelopmentHosts);
        }

        if (!string.IsNullOrWhiteSpace(options.Value.FeaturedAdventureUrl)
            && (!Uri.TryCreate(options.Value.FeaturedAdventureUrl, UriKind.Absolute, out var featuredAdventure)
                || (featuredAdventure.Scheme != Uri.UriSchemeHttp
                    && featuredAdventure.Scheme != Uri.UriSchemeHttps)))
        {
            throw new InvalidDataException(
                "The featured Adventure URL must be an absolute HTTP or HTTPS URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.Value.WorkspaceSignInUrl)
            && (!Uri.TryCreate(options.Value.WorkspaceSignInUrl, UriKind.Absolute, out var workspaceSignIn)
                || workspaceSignIn.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidDataException(
                "The deployed workspace sign-in URL must be an absolute HTTPS URL.");
        }
    }

    /// <inheritdoc />
    public bool IsPublicPlatformHost(HostString host) =>
        CreatorHost.TryNormalize(host.Host, out var normalizedHost)
        && hosts.Contains(normalizedHost);

    private void AddValidated(IEnumerable<string> configuredHosts)
    {
        foreach (var configuredHost in configuredHosts)
        {
            if (!CreatorHost.TryNormalize(configuredHost, out var normalizedHost))
            {
                throw new InvalidDataException(
                    $"Public platform host '{configuredHost}' is invalid.");
            }

            if (!hosts.Add(normalizedHost))
            {
                throw new InvalidDataException(
                    $"Public platform host '{configuredHost}' is duplicated.");
            }
        }
    }
}
