using AdventuresSuite.Identity.ExternalId;
using Azure.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TheSimontonAdventures.Web.Authorization;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies fail-closed Slice 5F host composition.</summary>
public sealed class AuthenticationHostingTests
{
    /// <summary>Ensures absent authentication configuration remains explicitly public-only.</summary>
    [Fact]
    public void AddAuthentication_AbsentConfiguration_RegistersDisabledMode()
    {
        var builder = WebApplication.CreateBuilder();

        var configuration = builder.AddAdventuresSuiteAuthentication();
        using var services = builder.Services.BuildServiceProvider();

        Assert.Equal(AuthenticationMode.Disabled, configuration.Mode);
        Assert.Same(configuration, services.GetRequiredService<AuthenticationConfiguration>());
    }

    /// <summary>Ensures unknown or partial hosted modes fail rather than falling back.</summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("ExternalProvider")]
    [InlineData("unexpected")]
    public void AddAuthentication_UnsupportedOrPartialConfiguration_Throws(string mode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Authentication:Mode"] = mode;

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddAdventuresSuiteAuthentication());
    }

    /// <summary>Ensures generated platform identities are typed, bounded, and unpredictable.</summary>
    [Fact]
    public void CryptographicIdentityGenerator_CreatesDistinctTypedValues()
    {
        var generator = new CryptographicAuthenticationIdentityGenerator();

        var users = Enumerable.Range(0, 32).Select(_ => generator.CreateUserId()).ToArray();
        var identities = Enumerable.Range(0, 32).Select(_ => generator.CreateExternalIdentityId()).ToArray();
        var sessions = Enumerable.Range(0, 32).Select(_ => generator.CreateSessionId()).ToArray();

        Assert.Equal(users.Length, users.Distinct().Count());
        Assert.Equal(identities.Length, identities.Distinct().Count());
        Assert.Equal(sessions.Length, sessions.Distinct().Count());
        Assert.All(users, value => Assert.StartsWith("user_", value.Value, StringComparison.Ordinal));
        Assert.All(identities, value => Assert.StartsWith("identity_", value.Value, StringComparison.Ordinal));
        Assert.All(sessions, value => Assert.StartsWith("session_", value.Value, StringComparison.Ordinal));
    }

    /// <summary>Ensures unsafe Key Vault references fail before any remote call.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" certificate")]
    [InlineData("certificate/version")]
    [InlineData("certificate_name")]
    public void KeyVaultCertificateSource_InvalidName_Throws(string reference)
    {
        var source = new KeyVaultExternalIdClientCertificateSource(
            new Uri("https://vault.example.com/"),
            new UnusedCredential());

        Assert.Throws<ArgumentException>(() => source.Resolve(reference));
    }

    private sealed class UnusedCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
