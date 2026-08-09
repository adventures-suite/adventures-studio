namespace TheSimontonAdventures.Web.Validation;

/// <summary>
/// Records whether required startup validation completed successfully for the
/// currently running application instance.
/// </summary>
public sealed class ApplicationReadinessState
{
    private int _resourcesValidated;
    private int _creatorContentValidated;

    /// <summary>Gets whether every Creator resource registry was validated.</summary>
    public bool ResourcesValidated => Volatile.Read(ref _resourcesValidated) == 1;

    /// <summary>Gets whether every Creator content snapshot was validated.</summary>
    public bool CreatorContentValidated => Volatile.Read(ref _creatorContentValidated) == 1;

    /// <summary>Gets whether the application is ready to serve public traffic.</summary>
    public bool IsReady => ResourcesValidated && CreatorContentValidated;

    /// <summary>Records successful validation of all Creator resource registries.</summary>
    public void MarkResourcesValidated() =>
        Interlocked.Exchange(ref _resourcesValidated, 1);

    /// <summary>Records successful validation of all Creator content snapshots.</summary>
    public void MarkCreatorContentValidated() =>
        Interlocked.Exchange(ref _creatorContentValidated, 1);
}
