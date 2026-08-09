using System.Security.Claims;
using AdventuresSuite.Identity;
using AdventuresSuite.Identity.Persistence;

namespace AdventuresSuite.Identity.ExternalId;

internal sealed record EstablishedExternalIdSession(
    AuthenticationSessionTicket Ticket,
    DateTimeOffset AuthenticatedAtUtc);

/// <summary>Resolves a validated external principal and creates an application-controlled session.</summary>
internal sealed class ExternalIdSessionIssuer(
    AuthenticationConfiguration configuration,
    IAuthenticationPersistenceTransactionFactory transactionFactory,
    IAuthenticationIdentityGenerator identityGenerator,
    IAuthenticationClock clock)
{
    /// <summary>Establishes a session without deriving authorization from provider claims.</summary>
    public async Task<EstablishedExternalIdSession> EstablishSessionAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = ExternalIdClaims.Map(principal, configuration.ProviderId);
            var utcNow = clock.GetUtcNow();
            var proposedUser = new PlatformUser(
                identityGenerator.CreateUserId(),
                PlatformUserStatus.Active,
                new SecurityVersion(1),
                utcNow,
                utcNow);
            var proposedMapping = new ExternalIdentityMapping(
                identityGenerator.CreateExternalIdentityId(),
                key,
                proposedUser.Id,
                utcNow,
                utcNow);
            await using var transaction = await transactionFactory.BeginAsync(cancellationToken);
            var mapping = await transaction.ResolveOrCreateUserAsync(
                proposedUser,
                proposedMapping,
                cancellationToken);
            var authoritativeMapping = await transaction.ExternalIdentities.GetByKeyAsync(
                key,
                cancellationToken);
            var user = await transaction.Users.GetAsync(mapping.UserId, cancellationToken);
            if (authoritativeMapping is null
                || authoritativeMapping.Id != mapping.Id
                || authoritativeMapping.UserId != mapping.UserId
                || !authoritativeMapping.CanEstablishSession
                || user is not { CanUseSession: true })
            {
                throw new InvalidOperationException();
            }

            var session = new ApplicationSession(
                identityGenerator.CreateSessionId(),
                user.Id,
                user.SecurityVersion,
                utcNow,
                utcNow,
                utcNow + configuration.AbsoluteSessionLifetime);
            await transaction.Sessions.AddAsync(session, mapping.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new EstablishedExternalIdSession(
                new AuthenticationSessionTicket(session.Id, session.UserId, session.SecurityVersion),
                utcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException("Authentication could not be completed.");
        }
    }
}
