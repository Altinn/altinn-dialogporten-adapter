using System.Security.Cryptography.X509Certificates;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.ApiClients.Maskinporten.Models;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Services;

public class FakeMaskinportenService : IMaskinportenService
{
    public Task<TokenResponse> GetToken(
        JsonWebKey jwk,
        string environment,
        string clientId,
        string scope,
        string resource,
        string? consumerOrgNo = null,
        bool disableCaching = false)
    {
        return Task.FromResult(new TokenResponse
        {
            AccessToken = "fake.token",
            ExpiresIn = 3600,
            Scope = "",
            TokenType = "bearer"
        });
    }

    public Task<TokenResponse> GetToken(
        X509Certificate2 cert,
        string environment,
        string clientId,
        string scope,
        string resource,
        string? consumerOrgNo = null,
        bool disableCaching = false)
    {
        return Task.FromResult(new TokenResponse
        {
            AccessToken = "fake.token",
            ExpiresIn = 3600,
            Scope = "",
            TokenType = "bearer"
        });
    }

    public Task<TokenResponse> GetToken(
        string base64EncodedJWK,
        string environment,
        string clientId,
        string scope,
        string resource,
        string? consumerOrgNo = null,
        bool disableCaching = false)
    {
        return Task.FromResult(new TokenResponse
        {
            AccessToken = "fake.token",
            ExpiresIn = 3600,
            Scope = "",
            TokenType = "bearer"
        });
    }

    public Task<TokenResponse> GetToken(IClientDefinition clientDefinition, bool disableCaching = false)
    {
        return Task.FromResult(new TokenResponse
        {
            AccessToken = "fake.token",
            ExpiresIn = 3600,
            Scope = "",
            TokenType = "bearer"
        });
    }

    public Task<TokenResponse> ExchangeToAltinnToken(
        TokenResponse tokenResponse,
        string environment,
        string? userName = null,
        string? password = null,
        bool disableCaching = false,
        bool isTestOrg = false)
    {
        return Task.FromResult(new TokenResponse
        {
            AccessToken = "fake.token",
            ExpiresIn = 3600,
            Scope = "",
            TokenType = "bearer"
        });
    }
}
