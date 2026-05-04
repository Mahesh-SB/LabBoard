using System.Text.Json;
using LabBoard.TicketMaster.Api.Models;
using LabBoard.TicketMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LabBoard.TicketMaster.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController(
    IServiceTokenService tokenService,
    IUserDetailsService  userService) : ControllerBase
{
    private static readonly List<Ticket> _tickets = [];

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Ticket>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(_tickets);

    [HttpPost]
    [ProducesResponseType(typeof(Ticket), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Book([FromBody] BookTicketRequest request)
    {
        // ── Step 1: extract userId from the user's Bearer token ───────────────
        var userId = ExtractUserIdFromToken(Request.Headers.Authorization);
        if (userId is null)
            return Unauthorized(new { error = "A valid user Bearer token is required." });

        // ── Step 2: get service token from Auth API (cached) ──────────────────
        var serviceToken = await tokenService.GetTokenAsync();
        if (serviceToken is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Service token unavailable. Auth API may be down." });

        // ── Step 3: fetch user details from UserManagement API ────────────────
        var user = await userService.GetUserAsync(userId.Value, serviceToken);
        if (user is null)
            return NotFound(new { error = $"User '{userId}' not found." });

        // ── Step 4: create ticket enriched with user details ──────────────────
        var ticket = new Ticket(
            Id:         Guid.NewGuid(),
            EventName:  request.EventName,
            Seat:        request.Seat,
            BookedAt:   DateTime.UtcNow,
            UserId:     user.Id,
            UserName:   user.FullName,
            UserEmail:  user.Email,
            UserPhone:  user.Phone,
            UserGender: user.Gender
        );

        _tickets.Add(ticket);
        return CreatedAtAction(nameof(GetAll), new { id = ticket.Id }, ticket);
    }

    // Decodes the JWT payload (middle segment) without signature verification.
    // The gateway has already validated the token; we only need the subject claim.
    private static Guid? ExtractUserIdFromToken(string? authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = authHeader["Bearer ".Length..].Split('.');
        if (parts.Length != 3) return null;

        try
        {
            // Base64url → Base64
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

public record Ticket(
    Guid     Id,
    string   EventName,
    string   Seat,
    DateTime BookedAt,
    Guid     UserId,
    string   UserName,
    string   UserEmail,
    string   UserPhone,
    string   UserGender);

public record BookTicketRequest(string EventName, string Seat);
