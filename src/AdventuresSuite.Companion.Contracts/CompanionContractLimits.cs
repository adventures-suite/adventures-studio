namespace AdventuresSuite.Companion.Contracts;

/// <summary>Defines the reviewed alpha bounds for the Companion v1 wire contract.</summary>
public static class CompanionContractLimits
{
    /// <summary>Gets the default collection page size.</summary>
    public const int DefaultPageSize = 20;
    /// <summary>Gets the maximum collection page size.</summary>
    public const int MaximumPageSize = 100;
    /// <summary>Gets the maximum opaque identity length.</summary>
    public const int MaximumIdentityLength = 128;
    /// <summary>Gets the maximum itinerary days in one projection.</summary>
    public const int MaximumItineraryDays = 180;
    /// <summary>Gets the maximum schedule items in one itinerary day.</summary>
    public const int MaximumScheduleItemsPerDay = 250;
    /// <summary>Gets the approved opaque identity pattern.</summary>
    public const string OpaqueIdentityPattern = "^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$";
    /// <summary>Gets the maximum ordinary JSON response size.</summary>
    public const int MaximumJsonResponseBytes = 2 * 1024 * 1024;
}
