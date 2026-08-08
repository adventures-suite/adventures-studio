using System.Reflection;
using System.Text.RegularExpressions;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Discovers and validates immutable embedded database migrations.</summary>
public static partial class MigrationCatalog
{
    /// <summary>Gets validated migration resource names in execution order.</summary>
    public static IReadOnlyList<string> GetOrderedResourceNames(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var names = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Database.Migrations.", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (names.Length == 0)
        {
            throw new InvalidOperationException("No embedded database migrations were found.");
        }

        var numbers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var fileName = name[(name.LastIndexOf(".Database.Migrations.", StringComparison.Ordinal)
                + ".Database.Migrations.".Length)..];
            var match = MigrationNamePattern().Match(fileName);
            if (!match.Success || !numbers.Add(match.Groups[1].Value))
            {
                throw new InvalidOperationException(
                    $"Migration resource '{name}' does not have a unique NNNN_description.sql name.");
            }
        }

        return Array.AsReadOnly(names);
    }

    /// <summary>Determines whether a manifest resource is a validated migration.</summary>
    public static bool IsMigrationResource(Assembly assembly, string resourceName) =>
        GetOrderedResourceNames(assembly).Contains(resourceName, StringComparer.Ordinal);

    [GeneratedRegex(@"^(\d{4})_[a-z0-9_]+\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationNamePattern();
}
