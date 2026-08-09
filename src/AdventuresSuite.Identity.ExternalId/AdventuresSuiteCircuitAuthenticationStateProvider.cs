using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Logging;
using AdventuresSuite.Identity;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>
/// Revalidates the authoritative application session for a live Blazor server circuit.
/// </summary>
internal class AdventuresSuiteCircuitAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    AuthenticationConfiguration configuration,
    IServerSessionAuthenticator sessionAuthenticator,
    IAuthenticationClock clock)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    private readonly AuthenticationConfiguration configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IServerSessionAuthenticator sessionAuthenticator =
        sessionAuthenticator ?? throw new ArgumentNullException(nameof(sessionAuthenticator));
    private readonly IAuthenticationClock clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    /// <inheritdoc />
    protected override TimeSpan RevalidationInterval =>
        configuration.CircuitRevalidationInterval;

    internal TimeSpan IntervalForTest => RevalidationInterval;

    internal Task<bool> ValidateForTestAsync(AuthenticationState authenticationState) =>
        ValidateAuthenticationStateAsync(authenticationState, CancellationToken.None);

    /// <inheritdoc />
    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        if (configuration.Mode == AuthenticationMode.Disabled
            || string.IsNullOrEmpty(configuration.WorkspaceOrigin))
        {
            return false;
        }

        var cookie = ApplicationCookiePrincipal.Parse(
            authenticationState.User,
            clock.GetUtcNow());
        if (cookie is null)
        {
            return false;
        }

        try
        {
            var result = await sessionAuthenticator.AuthenticateAsync(
                configuration.WorkspaceOrigin,
                cookie.Ticket,
                cancellationToken);
            return result.Outcome == SessionAuthenticationOutcome.Authenticated;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Circuit revalidation cannot fall back to the captured principal.
            return false;
        }
    }
}
