using Dapper;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.Planning.SqlServer;

internal sealed class DapperAdventurePlanRepository(
    CreatorId transactionCreatorId,
    SqlConnection connection,
    SqlTransaction transaction) : IAdventurePlanRepository
{
    public async Task<AdventurePlan?> GetAsync(
        CreatorId creatorId,
        AdventurePlanId planId,
        CancellationToken cancellationToken = default)
    {
        RequireScope(creatorId);
        const string sql = """
            SELECT CreatorId,AdventurePlanId,Title,WorkingDescription,LifecycleStage,PlanningStatus,StartDate,EndDate,Version,CreatedAtUtc,UpdatedAtUtc
              FROM planning.AdventurePlans WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
            SELECT TravelerId,DisplayName FROM planning.Travelers WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY TravelerId;
            SELECT TravelerId,Preference FROM planning.TravelerPreferences WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY TravelerId, Preference;
            SELECT DestinationVisitId,Name,StartDate,EndDate,TimeZone,Sequence,Notes FROM planning.DestinationVisits WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY Sequence;
            SELECT ItineraryDayId,DestinationVisitId,LocalDate,TimeZone,Title FROM planning.ItineraryDays WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY LocalDate;
            SELECT PlannedActivityId,ItineraryDayId,Title,StartsAtLocal,EndsAtLocal,Status FROM planning.PlannedActivities WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY PlannedActivityId;
            SELECT TransportationSegmentId,Mode,Origin,Destination,DepartureDate,DepartureTimeLocal,DepartureTimeZone,ArrivalDate,ArrivalTimeLocal,ArrivalTimeZone,Status FROM planning.TransportationSegments WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY DepartureDate, TransportationSegmentId;
            SELECT AccommodationId,Name,StartDate,EndDate,TimeZone,Status FROM planning.Accommodations WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY StartDate, AccommodationId;
            SELECT ReservationId,Subject,ConfirmationReference,Status FROM planning.Reservations WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY ReservationId;
            SELECT PlanningNoteId,NoteText FROM planning.PlanningNotes WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY PlanningNoteId;
            SELECT PlanningTaskId,Description,DueDate,IsCompleted FROM planning.PlanningTasks WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY DueDate, PlanningTaskId;
            SELECT BudgetItemId,Description,Amount,CurrencyCode FROM planning.BudgetItems WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY BudgetItemId;
            SELECT PackingItemId,Description,IsPacked FROM planning.PackingItems WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId ORDER BY PackingItemId;
            """;
        var command = Command(sql, new { CreatorId = creatorId.Value, PlanId = planId.Value }, cancellationToken);
        using var results = await connection.QueryMultipleAsync(command);
        var root = await results.ReadSingleOrDefaultAsync<PlanRow>();
        var travelers = (await results.ReadAsync<TravelerRow>()).ToArray();
        var preferences = (await results.ReadAsync<PreferenceRow>()).ToArray();
        var visits = (await results.ReadAsync<VisitRow>()).ToArray();
        var days = (await results.ReadAsync<DayRow>()).ToArray();
        var activities = (await results.ReadAsync<ActivityRow>()).ToArray();
        var transportation = (await results.ReadAsync<TransportationRow>()).ToArray();
        var accommodations = (await results.ReadAsync<AccommodationRow>()).ToArray();
        var reservations = (await results.ReadAsync<ReservationRow>()).ToArray();
        var notes = (await results.ReadAsync<NoteRow>()).ToArray();
        var tasks = (await results.ReadAsync<TaskRow>()).ToArray();
        var budgets = (await results.ReadAsync<BudgetRow>()).ToArray();
        var packing = (await results.ReadAsync<PackingRow>()).ToArray();
        return root is null ? null : Map(root, travelers, preferences, visits, days, activities,
            transportation, accommodations, reservations, notes, tasks, budgets, packing);
    }

    public async Task<IReadOnlyList<AdventurePlan>> ListAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default) =>
        await ListByArchiveStateAsync(creatorId, isArchived: false, cancellationToken);

    public async Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default) =>
        await ListByArchiveStateAsync(creatorId, isArchived: true, cancellationToken);

    private async Task<IReadOnlyList<AdventurePlan>> ListByArchiveStateAsync(
        CreatorId creatorId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        RequireScope(creatorId);
        var ids = await connection.QueryAsync<string>(Command("""
            SELECT AdventurePlanId
            FROM planning.AdventurePlans
            WHERE CreatorId=@CreatorId
              AND ((@IsArchived=1 AND PlanningStatus='Archived')
                   OR (@IsArchived=0 AND PlanningStatus<>'Archived'))
            ORDER BY StartDate, AdventurePlanId;
            """, new { CreatorId = creatorId.Value, IsArchived = isArchived }, cancellationToken));
        var plans = new List<AdventurePlan>();
        foreach (var id in ids)
        {
            plans.Add((await GetAsync(creatorId, new AdventurePlanId(id), cancellationToken))!);
        }

        return plans.AsReadOnly();
    }

    public async Task AddAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        CancellationToken cancellationToken = default)
    {
        RequirePlanScope(creatorId, plan);
        await connection.ExecuteAsync(Command(InsertPlanSql, RootParameters(plan), cancellationToken));
        await InsertChildrenAsync(plan, cancellationToken);
    }

    public async Task UpdateAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        RequirePlanScope(creatorId, plan);
        if (expectedVersion < 1 || plan.Audit.Version != expectedVersion + 1)
        {
            throw new ArgumentException("An update must advance the expected version by exactly one.", nameof(expectedVersion));
        }

        var updated = await connection.ExecuteAsync(Command(UpdatePlanSql,
            new
            {
                CreatorId = creatorId.Value,
                PlanId = plan.Id.Value,
                plan.Title,
                plan.WorkingDescription,
                LifecycleStage = plan.LifecycleStage.ToString(),
                PlanningStatus = plan.Status.ToString(),
                StartDate = plan.Dates.Start.ToDateTime(TimeOnly.MinValue),
                EndDate = plan.Dates.End.ToDateTime(TimeOnly.MinValue),
                Version = plan.Audit.Version,
                plan.Audit.UpdatedAtUtc,
                ExpectedVersion = expectedVersion
            }, cancellationToken));
        if (updated == 0)
        {
            throw new PlanningConcurrencyException(plan.Id, expectedVersion);
        }

        await DeleteChildrenAsync(plan, cancellationToken);
        await InsertChildrenAsync(plan, cancellationToken);
    }

    private async Task InsertChildrenAsync(AdventurePlan plan, CancellationToken cancellationToken)
    {
        var owner = new { CreatorId = plan.CreatorId.Value, PlanId = plan.Id.Value };
        foreach (var item in plan.Travelers)
        {
            await ExecuteAsync("INSERT planning.Travelers VALUES (@CreatorId,@PlanId,@Id,@Name);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, Name = item.DisplayName }, cancellationToken);
            foreach (var preference in item.Preferences)
            {
                await ExecuteAsync("INSERT planning.TravelerPreferences VALUES (@CreatorId,@PlanId,@Id,@Preference);",
                    new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, Preference = preference }, cancellationToken);
            }
        }

        foreach (var item in plan.DestinationVisits)
            await ExecuteAsync("INSERT planning.DestinationVisits VALUES (@CreatorId,@PlanId,@Id,@Name,@Start,@End,@Zone,@Sequence,@Notes);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Name, Start = item.Dates.Start.ToDateTime(TimeOnly.MinValue), End = item.Dates.End.ToDateTime(TimeOnly.MinValue), Zone = item.TimeZone.Value, item.Sequence, item.Notes }, cancellationToken);
        foreach (var item in plan.ItineraryDays)
            await ExecuteAsync("INSERT planning.ItineraryDays VALUES (@CreatorId,@PlanId,@Id,@VisitId,@Date,@Zone,@Title);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, VisitId = item.DestinationVisitId?.Value, Date = item.Date.ToDateTime(TimeOnly.MinValue), Zone = item.TimeZone.Value, item.Title }, cancellationToken);
        foreach (var item in plan.Activities)
            await ExecuteAsync("INSERT planning.PlannedActivities VALUES (@CreatorId,@PlanId,@Id,@DayId,@Title,@Start,@End,@Status);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, DayId = item.ItineraryDayId.Value, item.Title, Start = item.StartsAtLocal?.ToTimeSpan(), End = item.EndsAtLocal?.ToTimeSpan(), Status = item.Status.ToString() }, cancellationToken);
        foreach (var item in plan.Transportation)
            await ExecuteAsync("INSERT planning.TransportationSegments VALUES (@CreatorId,@PlanId,@Id,@Mode,@From,@To,@DepartureDate,@DepartureTime,@DepartureZone,@ArrivalDate,@ArrivalTime,@ArrivalZone,@Status);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Mode, item.From, item.To, DepartureDate = item.DepartureDate.ToDateTime(TimeOnly.MinValue), DepartureTime = item.DepartureTimeLocal?.ToTimeSpan(), DepartureZone = item.DepartureTimeZone.Value, ArrivalDate = item.ArrivalDate.ToDateTime(TimeOnly.MinValue), ArrivalTime = item.ArrivalTimeLocal?.ToTimeSpan(), ArrivalZone = item.ArrivalTimeZone.Value, Status = item.Status.ToString() }, cancellationToken);
        foreach (var item in plan.Accommodations)
            await ExecuteAsync("INSERT planning.Accommodations VALUES (@CreatorId,@PlanId,@Id,@Name,@Start,@End,@Zone,@Status);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Name, Start = item.Dates.Start.ToDateTime(TimeOnly.MinValue), End = item.Dates.End.ToDateTime(TimeOnly.MinValue), Zone = item.TimeZone.Value, Status = item.Status.ToString() }, cancellationToken);
        foreach (var item in plan.Reservations)
            await ExecuteAsync("INSERT planning.Reservations VALUES (@CreatorId,@PlanId,@Id,@Subject,@Reference,@Status);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Subject, Reference = item.ConfirmationReference, Status = item.Status.ToString() }, cancellationToken);
        foreach (var item in plan.Notes)
            await ExecuteAsync("INSERT planning.PlanningNotes VALUES (@CreatorId,@PlanId,@Id,@Text);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Text }, cancellationToken);
        foreach (var item in plan.Tasks)
            await ExecuteAsync("INSERT planning.PlanningTasks VALUES (@CreatorId,@PlanId,@Id,@Description,@DueDate,@Completed);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Description, DueDate = item.DueDate?.ToDateTime(TimeOnly.MinValue), Completed = item.IsCompleted }, cancellationToken);
        foreach (var item in plan.BudgetItems)
            await ExecuteAsync("INSERT planning.BudgetItems VALUES (@CreatorId,@PlanId,@Id,@Description,@Amount,@Currency);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Description, item.Amount, Currency = item.CurrencyCode }, cancellationToken);
        foreach (var item in plan.PackingItems)
            await ExecuteAsync("INSERT planning.PackingItems VALUES (@CreatorId,@PlanId,@Id,@Description,@Packed);",
                new { owner.CreatorId, owner.PlanId, Id = item.Id.Value, item.Description, Packed = item.IsPacked }, cancellationToken);
    }

    private Task DeleteChildrenAsync(AdventurePlan plan, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(Command(DeleteChildrenSql,
            new { CreatorId = plan.CreatorId.Value, PlanId = plan.Id.Value }, cancellationToken));

    private Task ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(Command(sql, parameters, cancellationToken));

    private CommandDefinition Command(string sql, object? parameters, CancellationToken cancellationToken) =>
        new(sql, parameters, transaction, cancellationToken: cancellationToken);

    private void RequireScope(CreatorId creatorId)
    {
        if (creatorId == default || creatorId != transactionCreatorId)
            throw new ArgumentException("The repository Creator must match its transaction scope.", nameof(creatorId));
    }

    private void RequirePlanScope(CreatorId creatorId, AdventurePlan plan)
    {
        RequireScope(creatorId);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.CreatorId != creatorId)
            throw new ArgumentException("The Adventure Plan must be owned by the repository Creator.", nameof(plan));
    }

    private static object RootParameters(AdventurePlan plan) => new
    {
        CreatorId = plan.CreatorId.Value,
        PlanId = plan.Id.Value,
        plan.Title,
        plan.WorkingDescription,
        LifecycleStage = plan.LifecycleStage.ToString(),
        PlanningStatus = plan.Status.ToString(),
        StartDate = plan.Dates.Start.ToDateTime(TimeOnly.MinValue),
        EndDate = plan.Dates.End.ToDateTime(TimeOnly.MinValue),
        plan.Audit.Version,
        plan.Audit.CreatedAtUtc,
        plan.Audit.UpdatedAtUtc
    };

    private static AdventurePlan Map(PlanRow root, TravelerRow[] travelerRows, PreferenceRow[] preferenceRows,
        VisitRow[] visitRows, DayRow[] dayRows, ActivityRow[] activityRows, TransportationRow[] transportationRows,
        AccommodationRow[] accommodationRows, ReservationRow[] reservationRows, NoteRow[] noteRows,
        TaskRow[] taskRows, BudgetRow[] budgetRows, PackingRow[] packingRows) => new(
        new(root.AdventurePlanId), new(root.CreatorId), root.Title, root.WorkingDescription,
        Enum.Parse<AdventureLifecycleStage>(root.LifecycleStage), Enum.Parse<PlanningStatus>(root.PlanningStatus),
        new(DateOnly.FromDateTime(root.StartDate), DateOnly.FromDateTime(root.EndDate)),
        new(root.Version, root.CreatedAtUtc.ToUniversalTime(), root.UpdatedAtUtc.ToUniversalTime()),
        travelerRows.Select(row => new Traveler
        {
            Id = new(row.TravelerId),
            DisplayName = row.DisplayName,
            Preferences = preferenceRows.Where(p => p.TravelerId == row.TravelerId).Select(p => p.Preference).ToArray()
        }).ToArray(),
        visitRows.Select(row => new DestinationVisit
        {
            Id = new(row.DestinationVisitId),
            Name = row.Name,
            Dates = new(DateOnly.FromDateTime(row.StartDate), DateOnly.FromDateTime(row.EndDate)),
            TimeZone = new(row.TimeZone),
            Sequence = row.Sequence,
            Notes = row.Notes
        }).ToArray(),
        dayRows.Select(row => new ItineraryDay
        {
            Id = new(row.ItineraryDayId),
            Date = DateOnly.FromDateTime(row.LocalDate),
            TimeZone = new(row.TimeZone),
            DestinationVisitId = row.DestinationVisitId is null ? null : new(row.DestinationVisitId),
            Title = row.Title
        }).ToArray(),
        activityRows.Select(row => new PlannedActivity
        {
            Id = new(row.PlannedActivityId),
            ItineraryDayId = new(row.ItineraryDayId),
            Title = row.Title,
            StartsAtLocal = row.StartsAtLocal is null ? null : TimeOnly.FromTimeSpan(row.StartsAtLocal.Value),
            EndsAtLocal = row.EndsAtLocal is null ? null : TimeOnly.FromTimeSpan(row.EndsAtLocal.Value),
            Status = Enum.Parse<PlanItemStatus>(row.Status)
        }).ToArray(),
        transportationRows.Select(row => new TransportationSegment
        {
            Id = new(row.TransportationSegmentId),
            Mode = row.Mode,
            From = row.Origin,
            To = row.Destination,
            DepartureDate = DateOnly.FromDateTime(row.DepartureDate),
            DepartureTimeLocal = row.DepartureTimeLocal is null ? null : TimeOnly.FromTimeSpan(row.DepartureTimeLocal.Value),
            DepartureTimeZone = new(row.DepartureTimeZone),
            ArrivalDate = DateOnly.FromDateTime(row.ArrivalDate),
            ArrivalTimeLocal = row.ArrivalTimeLocal is null ? null : TimeOnly.FromTimeSpan(row.ArrivalTimeLocal.Value),
            ArrivalTimeZone = new(row.ArrivalTimeZone),
            Status = Enum.Parse<PlanItemStatus>(row.Status)
        }).ToArray(),
        accommodationRows.Select(row => new Accommodation { Id = new(row.AccommodationId), Name = row.Name, Dates = new(DateOnly.FromDateTime(row.StartDate), DateOnly.FromDateTime(row.EndDate)), TimeZone = new(row.TimeZone), Status = Enum.Parse<PlanItemStatus>(row.Status) }).ToArray(),
        reservationRows.Select(row => new Reservation { Id = new(row.ReservationId), Subject = row.Subject, ConfirmationReference = row.ConfirmationReference, Status = Enum.Parse<PlanItemStatus>(row.Status) }).ToArray(),
        noteRows.Select(row => new PlanningNote { Id = new(row.PlanningNoteId), Text = row.NoteText }).ToArray(),
        taskRows.Select(row => new PlanningTask { Id = new(row.PlanningTaskId), Description = row.Description, DueDate = row.DueDate is null ? null : DateOnly.FromDateTime(row.DueDate.Value), IsCompleted = row.IsCompleted }).ToArray(),
        budgetRows.Select(row => new BudgetItem { Id = new(row.BudgetItemId), Description = row.Description, Amount = row.Amount, CurrencyCode = row.CurrencyCode.Trim() }).ToArray(),
        packingRows.Select(row => new PackingItem { Id = new(row.PackingItemId), Description = row.Description, IsPacked = row.IsPacked }).ToArray());

    private const string InsertPlanSql = """
        INSERT planning.AdventurePlans (CreatorId,AdventurePlanId,Title,WorkingDescription,LifecycleStage,PlanningStatus,StartDate,EndDate,Version,CreatedAtUtc,UpdatedAtUtc)
        VALUES (@CreatorId,@PlanId,@Title,@WorkingDescription,@LifecycleStage,@PlanningStatus,@StartDate,@EndDate,@Version,@CreatedAtUtc,@UpdatedAtUtc);
        """;
    private const string UpdatePlanSql = """
        UPDATE planning.AdventurePlans SET Title=@Title,WorkingDescription=@WorkingDescription,LifecycleStage=@LifecycleStage,
          PlanningStatus=@PlanningStatus,StartDate=@StartDate,EndDate=@EndDate,Version=@Version,UpdatedAtUtc=@UpdatedAtUtc
        WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId AND Version=@ExpectedVersion;
        """;
    private const string DeleteChildrenSql = """
        DELETE planning.TravelerPreferences WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.PlannedActivities WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.ItineraryDays WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.DestinationVisits WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.Travelers WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.TransportationSegments WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.Accommodations WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.Reservations WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.PlanningNotes WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.PlanningTasks WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.BudgetItems WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        DELETE planning.PackingItems WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId;
        """;

    private sealed record PlanRow(string CreatorId, string AdventurePlanId, string Title, string? WorkingDescription, string LifecycleStage, string PlanningStatus, DateTime StartDate, DateTime EndDate, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
    private sealed record TravelerRow(string TravelerId, string DisplayName);
    private sealed record PreferenceRow(string TravelerId, string Preference);
    private sealed record VisitRow(string DestinationVisitId, string Name, DateTime StartDate, DateTime EndDate, string TimeZone, int Sequence, string? Notes);
    private sealed record DayRow(string ItineraryDayId, string? DestinationVisitId, DateTime LocalDate, string TimeZone, string Title);
    private sealed record ActivityRow(string PlannedActivityId, string ItineraryDayId, string Title, TimeSpan? StartsAtLocal, TimeSpan? EndsAtLocal, string Status);
    private sealed record TransportationRow(string TransportationSegmentId, string Mode, string Origin, string Destination, DateTime DepartureDate, TimeSpan? DepartureTimeLocal, string DepartureTimeZone, DateTime ArrivalDate, TimeSpan? ArrivalTimeLocal, string ArrivalTimeZone, string Status);
    private sealed record AccommodationRow(string AccommodationId, string Name, DateTime StartDate, DateTime EndDate, string TimeZone, string Status);
    private sealed record ReservationRow(string ReservationId, string Subject, string? ConfirmationReference, string Status);
    private sealed record NoteRow(string PlanningNoteId, string NoteText);
    private sealed record TaskRow(string PlanningTaskId, string Description, DateTime? DueDate, bool IsCompleted);
    private sealed record BudgetRow(string BudgetItemId, string Description, decimal Amount, string CurrencyCode);
    private sealed record PackingRow(string PackingItemId, string Description, bool IsPacked);
}
