using System.Text.Json.Serialization;

namespace AdventuresSuite.Companion.Contracts;

/// <summary>Provides source-generated System.Text.Json metadata for every Companion v1 contract.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    NumberHandling = JsonNumberHandling.Strict,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CompanionAdventureCollectionDto))]
[JsonSerializable(typeof(CompanionAdventureDto))]
[JsonSerializable(typeof(CompanionTodayDto))]
[JsonSerializable(typeof(CompanionItineraryDto))]
[JsonSerializable(typeof(CompanionReadinessDto))]
[JsonSerializable(typeof(CompanionPlaybookDto))]
[JsonSerializable(typeof(CompanionProblemDto))]
[JsonSerializable(typeof(IReadOnlyList<CompanionAdventureSummaryDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionDestinationSummaryDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionScheduleItemDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionItineraryDayDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionReadinessCategoryDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionReadinessActionDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionPlaybookSectionDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionPlaybookEntryDto>))]
[JsonSerializable(typeof(IReadOnlyList<CompanionResourceSummaryDto>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
public partial class CompanionJsonSerializerContext : JsonSerializerContext;
