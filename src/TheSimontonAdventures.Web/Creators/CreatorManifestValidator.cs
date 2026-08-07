using System.Globalization;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Enforces Creator manifest identity, domain, and storage-boundary invariants.
/// </summary>
public static class CreatorManifestValidator
{
    /// <summary>
    /// Validates a Creator manifest before it is made available to platform
    /// capabilities.
    /// </summary>
    /// <param name="creator">The deserialized Creator manifest.</param>
    /// <param name="applicationContentRoot">
    /// The trusted application directory against which the Creator content path
    /// is resolved.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="creator"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="applicationContentRoot"/> is empty.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// The manifest violates an identity, domain, or storage-boundary invariant.
    /// </exception>
    public static void Validate(
        Creator creator,
        string applicationContentRoot)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationContentRoot);

        if (creator.Id == default)
        {
            throw new InvalidDataException("Creator identity is required.");
        }

        ValidateRequiredValue(creator.Slug, "Creator slug");
        ValidateRequiredValue(creator.DisplayName, "Creator display name");
        ValidateRequiredValue(creator.Locale, "Creator locale");
        ValidateRequiredValue(creator.TimeZone, "Creator time zone");
        ValidateLocaleAndTimeZone(creator);

        if (creator.Brand is null)
        {
            throw new InvalidDataException("Creator brand configuration is required.");
        }

        ValidateBrand(creator.Brand);

        if (creator.Features is null)
        {
            throw new InvalidDataException("Creator feature configuration is required.");
        }

        ValidateDomains(creator);
        ValidateContentRoot(creator.ContentRoot, applicationContentRoot);
    }

    private static void ValidateBrand(CreatorBrand brand)
    {
        ValidateRequiredValue(brand.SiteName, "Creator brand site name");
        ValidateRequiredValue(brand.Tagline, "Creator brand tagline");
        ValidateRequiredValue(
            brand.HomeHeroImageUrl,
            "Creator homepage hero image URL");
        ValidateRequiredValue(
            brand.HomeHeroImageAlt,
            "Creator homepage hero image alternative text");
        ValidateRequiredValue(
            brand.HomeHeroHeadline,
            "Creator homepage hero headline");
        ValidateRequiredValue(
            brand.HomeHeroDescription,
            "Creator homepage hero description");
        ValidateRequiredValue(
            brand.HomeHeroActionLabel,
            "Creator homepage hero action label");
        ValidateHexColor(brand.PrimaryColor, "Creator brand primary color");
        ValidateHexColor(brand.AccentColor, "Creator brand accent color");

        if (!Enum.IsDefined(brand.Typography))
        {
            throw new InvalidDataException(
                "Creator brand typography must be an approved token.");
        }
    }

    private static void ValidateLocaleAndTimeZone(Creator creator)
    {
        try
        {
            _ = CultureInfo.GetCultureInfo(creator.Locale);
        }
        catch (CultureNotFoundException exception)
        {
            throw new InvalidDataException(
                $"Creator locale '{creator.Locale}' is invalid.",
                exception);
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(creator.TimeZone);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new InvalidDataException(
                $"Creator time zone '{creator.TimeZone}' is invalid.",
                exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new InvalidDataException(
                $"Creator time zone '{creator.TimeZone}' is invalid.",
                exception);
        }
    }

    private static void ValidateHexColor(string? value, string name)
    {
        if (value is null
            || value.Length != 7
            || value[0] != '#'
            || value[1..].Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                $"{name} must be a six-digit hexadecimal color.");
        }
    }

    private static void ValidateDomains(Creator creator)
    {
        var primaryDomain = NormalizeDomain(creator.PrimaryDomain);

        if (creator.Domains is null || creator.Domains.Count == 0)
        {
            throw new InvalidDataException(
                "A Creator must register at least one approved domain.");
        }

        var approvedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var domain in creator.Domains)
        {
            var normalizedDomain = NormalizeDomain(domain);

            if (!approvedDomains.Add(normalizedDomain))
            {
                throw new InvalidDataException(
                    $"Creator domain '{domain}' is registered more than once.");
            }
        }

        if (!approvedDomains.Contains(primaryDomain))
        {
            throw new InvalidDataException(
                "The Creator primary domain must be an approved domain.");
        }
    }

    private static string NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new InvalidDataException("Creator domains cannot be empty.");
        }

        var normalizedDomain = domain.Trim().TrimEnd('.').ToLowerInvariant();

        if (normalizedDomain.Contains("://", StringComparison.Ordinal)
            || normalizedDomain.Contains('/')
            || normalizedDomain.Contains('\\')
            || normalizedDomain.Contains(':')
            || Uri.CheckHostName(normalizedDomain) == UriHostNameType.Unknown)
        {
            throw new InvalidDataException(
                $"Creator domain '{domain}' is not a valid host name.");
        }

        return normalizedDomain;
    }

    private static void ValidateContentRoot(
        string contentRoot,
        string applicationContentRoot)
    {
        ValidateRequiredValue(contentRoot, "Creator content root");

        var segments = contentRoot.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(contentRoot)
            || segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException(
                "Creator content root must be a non-traversing relative path.");
        }

        var trustedRoot = Path.GetFullPath(applicationContentRoot);
        var trustedRootPrefix = Path.TrimEndingDirectorySeparator(trustedRoot)
            + Path.DirectorySeparatorChar;
        var resolvedContentRoot = Path.GetFullPath(
            Path.Combine(trustedRoot, contentRoot));

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!resolvedContentRoot.StartsWith(trustedRootPrefix, pathComparison))
        {
            throw new InvalidDataException(
                "Creator content root resolves outside the application content root.");
        }

        if (!Directory.Exists(resolvedContentRoot))
        {
            throw new InvalidDataException(
                $"Creator content root '{contentRoot}' does not exist.");
        }
    }

    private static void ValidateRequiredValue(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} is required.");
        }
    }
}
