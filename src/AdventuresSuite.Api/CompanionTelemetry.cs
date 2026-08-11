using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AdventuresSuite.Api;

/// <summary>Emits low-cardinality operational signals for Companion API reads.</summary>
public static class CompanionTelemetry
{
    /// <summary>Gets the stable activity source name.</summary>
    public const string ActivitySourceName = "AdventuresSuite.Companion";

    /// <summary>Gets the stable meter name.</summary>
    public const string MeterName = "AdventuresSuite.Companion";

    /// <summary>Gets the list-operation signal value.</summary>
    public const string ListAdventuresOperation = "ListCompanionAdventures";

    /// <summary>Gets the detail-operation signal value.</summary>
    public const string GetAdventureOperation = "GetCompanionAdventure";

    /// <summary>Gets the Today-operation signal value.</summary>
    public const string GetTodayOperation = "GetCompanionToday";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "adventures_suite.companion.requests",
        description: "Count of completed Companion API operations.");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "adventures_suite.companion.duration",
        unit: "ms",
        description: "Duration of completed Companion API operations.");

    /// <summary>Starts a trace span for the Adventures collection query.</summary>
    public static Activity? StartListAdventures() =>
        ActivitySource.StartActivity("companion.adventures.list", ActivityKind.Server);

    /// <summary>Starts a trace span for one Adventure detail query.</summary>
    public static Activity? StartGetAdventure() =>
        ActivitySource.StartActivity("companion.adventures.get", ActivityKind.Server);

    /// <summary>Starts a trace span for one Adventure Today query.</summary>
    public static Activity? StartGetToday() =>
        ActivitySource.StartActivity("companion.adventures.today", ActivityKind.Server);

    /// <summary>Records a completed operation without identity or resource dimensions.</summary>
    public static void Record(string outcome, TimeSpan elapsed, Activity? activity) =>
        Record(ListAdventuresOperation, outcome, elapsed, activity);

    /// <summary>Records a completed operation without identity or resource dimensions.</summary>
    public static void Record(string operation, string outcome, TimeSpan elapsed, Activity? activity)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "outcome", outcome }
        };
        Requests.Add(1, tags);
        Duration.Record(elapsed.TotalMilliseconds, tags);
        activity?.SetTag("operation", operation);
        activity?.SetTag("outcome", outcome);
    }
}
