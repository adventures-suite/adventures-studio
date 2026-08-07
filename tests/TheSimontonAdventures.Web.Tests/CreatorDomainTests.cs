using System.Text.Json;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies Creator identity serialization and manifest isolation invariants.
/// </summary>
public sealed class CreatorDomainTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Ensures the committed flagship manifest loads and passes all Phase 1
    /// validation rules without changing the existing volume content layout.
    /// </summary>
    [Fact]
    public async Task FlagshipManifest_LoadsAndValidates()
    {
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Creators",
            "the-simonton-adventures",
            "creator.json");

        await using var stream = File.OpenRead(manifestPath);
        var creator = await JsonSerializer.DeserializeAsync<Creator>(
            stream,
            SerializerOptions);

        Assert.NotNull(creator);
        CreatorManifestValidator.Validate(creator, AppContext.BaseDirectory);
        Assert.Equal("creator_tsa_01", creator.Id.Value);
        Assert.Equal(CreatorStatus.Active, creator.Status);
        Assert.Equal("Content/Volumes", creator.ContentRoot);
    }

    /// <summary>
    /// Ensures Creator identity is represented as one stable JSON string rather
    /// than an object derived from mutable Creator properties.
    /// </summary>
    [Fact]
    public void CreatorId_SerializesAsCanonicalString()
    {
        var creatorId = new CreatorId("creator_tsa_01");

        var json = JsonSerializer.Serialize(creatorId, SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<CreatorId>(
            json,
            SerializerOptions);

        Assert.Equal("\"creator_tsa_01\"", json);
        Assert.Equal(creatorId, roundTrip);
    }

    /// <summary>
    /// Ensures public construction rejects identities that could represent an
    /// absent or ambiguous Creator scope.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Creator_TSA_01")]
    [InlineData("creator-tsa-01")]
    [InlineData("1_creator")]
    [InlineData("ab")]
    public void CreatorId_InvalidValue_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new CreatorId(value));
    }

    /// <summary>
    /// Ensures the default value of the identity struct cannot establish a
    /// Creator ownership boundary.
    /// </summary>
    [Fact]
    public void Validate_DefaultCreatorId_ThrowsInvalidDataException()
    {
        var creator = new Creator
        {
            Id = default,
            Slug = "test-creator",
            DisplayName = "Test Creator",
            Status = CreatorStatus.Active,
            PrimaryDomain = "example.com",
            Domains = ["example.com"],
            ContentRoot = "Content/Volumes"
        };

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>
    /// Ensures approved domains remain unique after host normalization.
    /// </summary>
    [Fact]
    public void Validate_DuplicateNormalizedDomains_ThrowsInvalidDataException()
    {
        var creator = CreateValidCreator(
            domains:
            [
                "example.com",
                "EXAMPLE.COM."
            ]);

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>
    /// Ensures a primary domain cannot identify a host outside the Creator's
    /// explicit approved-domain registration.
    /// </summary>
    [Fact]
    public void Validate_UnapprovedPrimaryDomain_ThrowsInvalidDataException()
    {
        var creator = CreateValidCreator(
            primaryDomain: "other.example.com");

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>
    /// Ensures domain registrations contain host names rather than URLs or
    /// host-and-port combinations.
    /// </summary>
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("example.com:443")]
    [InlineData("not a host")]
    public void Validate_InvalidDomain_ThrowsInvalidDataException(string domain)
    {
        var creator = CreateValidCreator(
            primaryDomain: domain,
            domains: [domain]);

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>
    /// Ensures a Creator manifest cannot select an absolute storage location.
    /// </summary>
    [Fact]
    public void Validate_AbsoluteContentRoot_ThrowsInvalidDataException()
    {
        var creator = CreateValidCreator(
            contentRoot: Path.GetPathRoot(AppContext.BaseDirectory)!);

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>
    /// Ensures a Creator manifest cannot traverse parent directories even when
    /// the normalized result would return beneath the application root.
    /// </summary>
    [Fact]
    public void Validate_TraversingContentRoot_ThrowsInvalidDataException()
    {
        var creator = CreateValidCreator(
            contentRoot: "Content/Creators/../Volumes");

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>Ensures arbitrary CSS cannot enter structured color tokens.</summary>
    [Fact]
    public void Validate_InvalidBrandColor_ThrowsInvalidDataException()
    {
        var creator = CreateValidCreator(
            brand: new CreatorBrand { PrimaryColor = "red; display:none" });

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>Ensures invalid locales fail before presentation rendering.</summary>
    [Fact]
    public void Validate_InvalidLocale_ThrowsInvalidDataException()
    {
        var creator = CreateValidCreator(locale: "not_a_real_locale");

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    /// <summary>Ensures invalid time zones fail before local-time conversion.</summary>
    [Fact]
    public void Validate_InvalidTimeZone_ThrowsInvalidDataException()
    {
        var creator = CreateValidCreator(timeZone: "Mars/Olympus_Mons");

        Assert.Throws<InvalidDataException>(() =>
            CreatorManifestValidator.Validate(
                creator,
                AppContext.BaseDirectory));
    }

    private static Creator CreateValidCreator(
        string primaryDomain = "example.com",
        IReadOnlyList<string>? domains = null,
        string contentRoot = "Content/Volumes",
        CreatorBrand? brand = null,
        string locale = "en-US",
        string timeZone = "UTC")
    {
        return new Creator
        {
            Id = new CreatorId("creator_test_01"),
            Slug = "test-creator",
            DisplayName = "Test Creator",
            Status = CreatorStatus.Active,
            PrimaryDomain = primaryDomain,
            Domains = domains ?? ["example.com"],
            Brand = brand ?? new CreatorBrand(),
            Locale = locale,
            TimeZone = timeZone,
            ContentRoot = contentRoot
        };
    }
}
