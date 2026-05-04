using System.Security.Cryptography;
using LabBoard.Auth.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LabBoard.Auth.Api.Controllers;

[ApiController]
public class JwksController(IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpGet(".well-known/jwks.json")]
    public IActionResult GetJwks()
    {
        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportCspBlob(Convert.FromBase64String(jwtOptions.Value.PrivateKey));

        var parameters = rsa.ExportParameters(false); // public portion only

        var jwk = new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = "labboard-auth-key-1",
            n   = Base64UrlEncoder.Encode(parameters.Modulus!),
            e   = Base64UrlEncoder.Encode(parameters.Exponent!)
        };

        return Ok(new { keys = new[] { jwk } });
    }
}
