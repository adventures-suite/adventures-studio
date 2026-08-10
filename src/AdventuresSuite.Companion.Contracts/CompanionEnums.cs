using System.Text.Json.Serialization;

namespace AdventuresSuite.Companion.Contracts;

/// <summary>Describes an Adventure's traveler-facing lifecycle.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionAdventureStatus>))]
public enum CompanionAdventureStatus
{
    /// <summary>The Adventure is planned.</summary>
    [JsonStringEnumMemberName("planned")] Planned,
    /// <summary>The Adventure is committed.</summary>
    [JsonStringEnumMemberName("committed")] Committed,
    /// <summary>The Adventure is currently underway.</summary>
    [JsonStringEnumMemberName("inProgress")] InProgress,
    /// <summary>The Adventure is complete.</summary>
    [JsonStringEnumMemberName("completed")] Completed
}

/// <summary>Describes the countdown position.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionCountdownState>))]
public enum CompanionCountdownState
{
    /// <summary>The Adventure begins in the future.</summary>
    [JsonStringEnumMemberName("future")] Future,
    /// <summary>The Adventure begins today.</summary>
    [JsonStringEnumMemberName("today")] Today,
    /// <summary>The Adventure is underway.</summary>
    [JsonStringEnumMemberName("inProgress")] InProgress,
    /// <summary>The Adventure is complete.</summary>
    [JsonStringEnumMemberName("complete")] Complete
}

/// <summary>Describes a projection's offline state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionOfflineState>))]
public enum CompanionOfflineState
{
    /// <summary>No offline projection is available.</summary>
    [JsonStringEnumMemberName("notAvailable")] NotAvailable,
    /// <summary>An offline projection is available.</summary>
    [JsonStringEnumMemberName("available")] Available,
    /// <summary>The offline projection is stale.</summary>
    [JsonStringEnumMemberName("stale")] Stale,
    /// <summary>The offline projection expired.</summary>
    [JsonStringEnumMemberName("expired")] Expired,
    /// <summary>The offline projection was revoked.</summary>
    [JsonStringEnumMemberName("revoked")] Revoked
}

/// <summary>Describes Today projection state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionTodayState>))]
public enum CompanionTodayState
{
    /// <summary>The Adventure has not begun.</summary>
    [JsonStringEnumMemberName("beforeAdventure")] BeforeAdventure,
    /// <summary>The Adventure is active.</summary>
    [JsonStringEnumMemberName("active")] Active,
    /// <summary>The Adventure ended.</summary>
    [JsonStringEnumMemberName("afterAdventure")] AfterAdventure,
    /// <summary>No items are scheduled.</summary>
    [JsonStringEnumMemberName("noScheduledItems")] NoScheduledItems
}

/// <summary>Describes schedule timing.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionTimeStatus>))]
public enum CompanionTimeStatus
{
    /// <summary>The item has a scheduled time.</summary>
    [JsonStringEnumMemberName("scheduled")] Scheduled,
    /// <summary>The item lasts all day.</summary>
    [JsonStringEnumMemberName("allDay")] AllDay,
    /// <summary>The time is not confirmed.</summary>
    [JsonStringEnumMemberName("toBeConfirmed")] ToBeConfirmed,
    /// <summary>The item was cancelled.</summary>
    [JsonStringEnumMemberName("cancelled")] Cancelled
}

/// <summary>Describes an item's operational status.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionOperationalStatus>))]
public enum CompanionOperationalStatus
{
    /// <summary>The item is proposed.</summary>
    [JsonStringEnumMemberName("proposed")] Proposed,
    /// <summary>The item is reserved.</summary>
    [JsonStringEnumMemberName("reserved")] Reserved,
    /// <summary>The item is confirmed.</summary>
    [JsonStringEnumMemberName("confirmed")] Confirmed,
    /// <summary>The item materially changed.</summary>
    [JsonStringEnumMemberName("changed")] Changed,
    /// <summary>The item was cancelled.</summary>
    [JsonStringEnumMemberName("cancelled")] Cancelled,
    /// <summary>The item is complete.</summary>
    [JsonStringEnumMemberName("completed")] Completed
}

/// <summary>Describes readiness state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionReadinessState>))]
public enum CompanionReadinessState
{
    /// <summary>The state is unknown.</summary>
    [JsonStringEnumMemberName("unknown")] Unknown,
    /// <summary>Attention is required.</summary>
    [JsonStringEnumMemberName("attentionRequired")] AttentionRequired,
    /// <summary>Work is in progress.</summary>
    [JsonStringEnumMemberName("inProgress")] InProgress,
    /// <summary>The projection is ready.</summary>
    [JsonStringEnumMemberName("ready")] Ready
}

/// <summary>Describes a readiness category.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionReadinessCategory>))]
public enum CompanionReadinessCategory
{
    /// <summary>Travel readiness.</summary>
    [JsonStringEnumMemberName("travel")] Travel,
    /// <summary>Lodging readiness.</summary>
    [JsonStringEnumMemberName("lodging")] Lodging,
    /// <summary>Activity readiness.</summary>
    [JsonStringEnumMemberName("activities")] Activities,
    /// <summary>Document readiness.</summary>
    [JsonStringEnumMemberName("documents")] Documents,
    /// <summary>Task readiness.</summary>
    [JsonStringEnumMemberName("tasks")] Tasks,
    /// <summary>Packing readiness.</summary>
    [JsonStringEnumMemberName("packing")] Packing
}

/// <summary>Describes Playbook freshness.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionPlaybookStaleState>))]
public enum CompanionPlaybookStaleState
{
    /// <summary>The Playbook is current.</summary>
    [JsonStringEnumMemberName("current")] Current,
    /// <summary>The Playbook is stale.</summary>
    [JsonStringEnumMemberName("stale")] Stale,
    /// <summary>The Playbook expired.</summary>
    [JsonStringEnumMemberName("expired")] Expired,
    /// <summary>The Playbook was revoked.</summary>
    [JsonStringEnumMemberName("revoked")] Revoked
}

/// <summary>Describes Resource availability without exposing provider state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionResourceAvailability>))]
public enum CompanionResourceAvailability
{
    /// <summary>The Resource is available.</summary>
    [JsonStringEnumMemberName("available")] Available,
    /// <summary>The Resource is processing.</summary>
    [JsonStringEnumMemberName("processing")] Processing,
    /// <summary>The Resource is blocked.</summary>
    [JsonStringEnumMemberName("blocked")] Blocked,
    /// <summary>The Resource expired.</summary>
    [JsonStringEnumMemberName("expired")] Expired,
    /// <summary>The Resource was revoked.</summary>
    [JsonStringEnumMemberName("revoked")] Revoked
}
