using System.Reflection;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the provider-neutral and Creator-scoped persistence boundary.</summary>
public sealed class PlanningPersistenceContractTests
{
    /// <summary>Ensures every repository operation begins with explicit Creator identity.</summary>
    [Fact]
    public void AdventurePlanRepository_AllOperationsRequireCreatorIdFirst()
    {
        var operations = typeof(IAdventurePlanRepository).GetMethods();

        Assert.NotEmpty(operations);
        Assert.All(operations, operation =>
        {
            var parameters = operation.GetParameters();
            Assert.NotEmpty(parameters);
            Assert.Equal(typeof(CreatorId), parameters[0].ParameterType);
        });
    }

    /// <summary>Ensures transaction creation establishes one explicit Creator boundary.</summary>
    [Fact]
    public void TransactionFactory_BeginRequiresCreatorIdFirst()
    {
        var operation = typeof(IPlanningTransactionFactory)
            .GetMethod(nameof(IPlanningTransactionFactory.BeginAsync));

        Assert.NotNull(operation);
        Assert.Equal(typeof(CreatorId), operation.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CreatorId), typeof(IPlanningTransaction)
            .GetProperty(nameof(IPlanningTransaction.CreatorId))?.PropertyType);
        Assert.Equal(typeof(IRequiredAuditIntentCollector), typeof(IPlanningTransaction)
            .GetProperty(nameof(IPlanningTransaction.RequiredAuditIntents))?.PropertyType);
    }

    /// <summary>Ensures persistence contracts do not expose infrastructure types.</summary>
    [Fact]
    public void PersistenceContracts_DoNotExposeProviderTypes()
    {
        Type[] contracts =
        [
            typeof(IAdventurePlanRepository),
            typeof(IPlanningTransactionFactory),
            typeof(IPlanningTransaction),
            typeof(IRequiredAuditIntentCollector)
        ];

        var exposedTypes = contracts
            .SelectMany(contract => contract.GetMethods())
            .SelectMany(GetSignatureTypes)
            .Select(type => type.Assembly.GetName().Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(exposedTypes, assembly =>
            assembly.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || assembly.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal)
            || assembly.StartsWith("Dapper", StringComparison.Ordinal));
    }

    /// <summary>Ensures stale-write errors retain provider-independent conflict details.</summary>
    [Fact]
    public void ConcurrencyException_PreservesPlanAndExpectedVersion()
    {
        var planId = new AdventurePlanId("plan_spain_2027");

        var exception = new PlanningConcurrencyException(planId, 4);

        Assert.Equal(planId, exception.PlanId);
        Assert.Equal(4, exception.ExpectedVersion);
        Assert.Contains("expected version 4", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Ensures invalid concurrency context is rejected predictably.</summary>
    [Fact]
    public void ConcurrencyException_InvalidContext_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlanningConcurrencyException(default, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlanningConcurrencyException(
            new AdventurePlanId("plan_spain_2027"),
            0));
    }

    private static IEnumerable<Type> GetSignatureTypes(MethodInfo method)
    {
        yield return Unwrap(method.ReturnType);
        foreach (var parameter in method.GetParameters())
        {
            yield return Unwrap(parameter.ParameterType);
        }
    }

    private static Type Unwrap(Type type)
    {
        if (type.IsGenericType)
        {
            return type.GetGenericArguments().Last();
        }

        return type;
    }
}
