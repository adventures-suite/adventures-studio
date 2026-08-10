namespace AdventuresSuite.Api;

/// <summary>Defines stable symbolic names for the closed Companion API foundation.</summary>
public static class CompanionApiConstants
{
    /// <summary>Gets the API base path.</summary>
    public const string BasePath = "/v1/companion";
    /// <summary>Gets the OpenAPI document name.</summary>
    public const string OpenApiDocumentName = "companion-v1";
    /// <summary>Gets the authorization policy name.</summary>
    public const string AuthorizationPolicy = "CompanionApiAccess";
    /// <summary>Gets the explicit deterministic-mode configuration key.</summary>
    public const string DeterministicModeKey = "Companion:DeterministicMode";
    /// <summary>Gets the explicit product activation-mode configuration key.</summary>
    public const string ActivationModeKey = "Companion:ActivationMode";
    /// <summary>Gets the deployment release-SHA configuration key.</summary>
    public const string ReleaseShaKey = "Deployment:CommitSha";
    /// <summary>Gets the only approved deployed activation mode.</summary>
    public const string DisabledActivationMode = "Disabled";
    /// <summary>Gets the stable API service identity.</summary>
    public const string ServiceName = "AdventuresSuite.Api";
}
