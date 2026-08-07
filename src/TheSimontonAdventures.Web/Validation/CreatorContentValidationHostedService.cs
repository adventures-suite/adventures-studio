using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Validation;

/// <summary>
/// Warms the immutable Creator registry and validates every deployed Creator
/// before the application begins serving requests.
/// </summary>
public sealed class CreatorContentValidationHostedService : IHostedService
{
    private readonly ICreatorService _creatorService;
    private readonly ICreatorContentValidator _validator;
    private readonly ILogger<CreatorContentValidationHostedService> _logger;

    /// <summary>Initializes startup validation.</summary>
    /// <param name="creatorService">The immutable Creator registry.</param>
    /// <param name="validator">The Creator-scoped content validator.</param>
    /// <param name="logger">The startup diagnostic logger.</param>
    public CreatorContentValidationHostedService(
        ICreatorService creatorService,
        ICreatorContentValidator validator,
        ILogger<CreatorContentValidationHostedService> logger)
    {
        _creatorService = creatorService;
        _validator = validator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var creators = await _creatorService.GetAllAsync(cancellationToken);
        var errors = new List<ContentValidationIssue>();

        foreach (var creator in creators)
        {
            var result = await _validator.ValidateAsync(
                creator.Id,
                cancellationToken);

            foreach (var issue in result.Issues)
            {
                if (issue.Severity == ContentValidationSeverity.Error)
                {
                    errors.Add(issue);
                    _logger.LogError(
                        "Creator content validation {Code} for {CreatorId}: {Message}",
                        issue.Code,
                        issue.CreatorId,
                        issue.Message);
                }
                else
                {
                    _logger.LogWarning(
                        "Creator content validation {Code} for {CreatorId}: {Message}",
                        issue.Code,
                        issue.CreatorId,
                        issue.Message);
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"Creator content validation failed with {errors.Count} error(s). " +
                "Review the preceding Creator-scoped diagnostics.");
        }

        _logger.LogInformation(
            "Validated {CreatorCount} Creator content snapshot(s).",
            creators.Count);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
