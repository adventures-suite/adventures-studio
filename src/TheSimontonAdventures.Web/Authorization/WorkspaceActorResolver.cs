using System.Security.Claims;
using AdventuresSuite.Identity;
using AdventuresSuite.Identity.ExternalId;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Resolves an authenticated web principal into a provider-neutral platform actor.</summary>
public interface IWorkspaceActorResolver
{
    /// <summary>Returns an ordinary human actor only for a valid application identity.</summary>
    ActorIdentity? Resolve(ClaimsPrincipal principal);
}

/// <summary>Maps the minimal protected application claim to a platform human actor.</summary>
public sealed class WorkspaceActorResolver : IWorkspaceActorResolver
{
    /// <inheritdoc />
    public ActorIdentity? Resolve(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated is not true)
        {
            return null;
        }

        var claims = principal.FindAll(ApplicationUserClaims.UserId).ToArray();
        if (claims.Length != 1)
        {
            return null;
        }

        try
        {
            var userId = new UserId(claims[0].Value);
            return new ActorIdentity(ActorType.Human, userId.Value, userId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
