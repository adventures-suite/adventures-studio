namespace TheSimontonAdventures.Web.Resources;

/// <summary>Resolves resources already stored in the application's shared public web root.</summary>
public sealed class LocalPublicResourceProvider : IResourceProvider
{
    private readonly string _webRoot;

    /// <summary>Initializes the provider for the active application web root.</summary>
    /// <param name="environment">The web-host environment.</param>
    public LocalPublicResourceProvider(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _webRoot = Path.GetFullPath(environment.WebRootPath);
    }

    /// <inheritdoc />
    public string Key => "local-public";

    /// <inheritdoc />
    public string GetPublicUrl(ResourceRecord resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.PublicationStatus != ResourcePublicationStatus.Published)
        {
            throw new InvalidDataException("Draft resources cannot use shared public storage.");
        }

        var location = resource.StorageLocation;
        if (string.IsNullOrWhiteSpace(location) || !location.StartsWith('/') || location.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Resource '{resource.Id}' must use a safe root-relative public path.");
        }

        var relativePath = location.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.GetFullPath(Path.Combine(_webRoot, relativePath));
        if (!physicalPath.StartsWith(_webRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !File.Exists(physicalPath))
        {
            throw new InvalidDataException($"Resource '{resource.Id}' does not resolve to an existing public file.");
        }

        return location;
    }
}
