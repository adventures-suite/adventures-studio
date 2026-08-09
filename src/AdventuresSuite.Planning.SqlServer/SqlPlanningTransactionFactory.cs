using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.Planning.SqlServer;

/// <summary>Creates Creator-scoped SQL Server transactions for Planning persistence.</summary>
public sealed class SqlPlanningTransactionFactory : IPlanningTransactionFactory
{
    private readonly string connectionString;

    /// <summary>Initializes the factory with a SQL Server connection string.</summary>
    public SqlPlanningTransactionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

        this.connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task<IPlanningTransaction> BeginAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        if (creatorId == default)
        {
            throw new ArgumentException("A valid Creator identity is required.", nameof(creatorId));
        }

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            return new SqlPlanningTransaction(creatorId, connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

internal sealed class SqlPlanningTransaction(
    CreatorId creatorId,
    SqlConnection connection,
    SqlTransaction transaction) : IPlanningTransaction
{
    private bool completed;

    public CreatorId CreatorId { get; } = creatorId;

    public IAdventurePlanRepository AdventurePlans { get; } =
        new DapperAdventurePlanRepository(creatorId, connection, transaction);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        await transaction.CommitAsync(cancellationToken);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!completed)
            {
                await transaction.RollbackAsync();
            }
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
                completed = true;
            }
        }
    }
}
