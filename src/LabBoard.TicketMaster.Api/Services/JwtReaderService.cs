using System.Text.Json;

namespace LabBoard.TicketMaster.Api.Services;

// Decodes the JWT payload without signature verification.
// The gateway is responsible for validating the token; this service only extracts the subject.
public class JwtReaderService : IJwtReaderService
{
    public Guid? ExtractUserId(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = authorizationHeader["Bearer ".Length..].Split('.');
        if (parts.Length != 3) return null;

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            var sub = doc.RootElement.TryGetProperty("sub", out var el) ? el.GetString() : null;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
