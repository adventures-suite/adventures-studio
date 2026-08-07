using System.Net;

namespace TheSimontonAdventures.Web.Creators;

internal static class CreatorHost
{
    internal static bool TryNormalize(string? host, out string normalizedHost)
    {
        normalizedHost = string.Empty;

        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var candidate = host.Trim().TrimEnd('.');

        if (candidate.Length == 0
            || candidate.Contains("://", StringComparison.Ordinal)
            || candidate.Contains('/')
            || candidate.Contains('\\'))
        {
            return false;
        }

        if (candidate.Length > 2
            && candidate[0] == '['
            && candidate[^1] == ']')
        {
            candidate = candidate[1..^1];
        }

        if (!IPAddress.TryParse(candidate, out _)
            && Uri.CheckHostName(candidate) == UriHostNameType.Unknown)
        {
            return false;
        }

        normalizedHost = candidate.ToLowerInvariant();
        return true;
    }
}
