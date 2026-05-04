using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using LabBoard.Auth.Api.Configuration;
using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Models.OAuth;
using LabBoard.Auth.Api.Models.User;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LabBoard.Auth.Api.Services.OAuth;

public class TokenService : ITokenService, IDisposable
{
    private readonly JwtOptions _jwt;
    private readonly SigningCredentials _credentials;
    private readonly RSACryptoServiceProvider _rsa;

    public TokenService(IOptions<JwtOptions> options)
    {
        _jwt = options.Value;
        _rsa = new RSACryptoServiceProvider();
        _rsa.ImportCspBlob(Convert.FromBase64String(_jwt.PrivateKey));
        _credentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
    }

    public TokenResponse Generate(UserResponse user, ClientAppResponse client, List<string> scopes)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,  user.FullName),
            new Claim("role",                        user.Role.ToString()),
            new Claim("scope",                       string.Join(" ", scopes))
        };

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddSeconds(client.TokenExpiry),
            signingCredentials: _credentials);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType   = "Bearer",
            ExpiresIn   = client.TokenExpiry,
            Scope       = string.Join(" ", scopes)
        };
    }

    public TokenResponse GenerateClientToken(ClientAppResponse client, List<string> scopes)
    {
        var scopeString = string.Join(" ", scopes);

        // One claim per scope value so policy-based RequireClaim checks work correctly
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.ClientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("client_id", client.ClientId),
            new("app_name",  client.AppName)
        };
        foreach (var scope in scopes)
            claims.Add(new Claim("scope", scope));

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.InternalAudience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddSeconds(client.TokenExpiry),
            signingCredentials: _credentials);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType   = "Bearer",
            ExpiresIn   = client.TokenExpiry,
            Scope       = scopeString
        };
    }

    public void Dispose() => _rsa.Dispose();
}
