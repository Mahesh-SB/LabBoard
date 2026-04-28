using Microsoft.AspNetCore.Mvc;

namespace LabBoard.TicketMaster.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private static readonly List<Ticket> _tickets = [];

    [HttpGet]
    public IActionResult GetAll() => Ok(_tickets);

    [HttpPost]
    public IActionResult Book([FromBody] BookTicketRequest request)
    {
        var ticket = new Ticket(
            Id:        Guid.NewGuid(),
            EventName: request.EventName,
            BookedBy:  request.BookedBy,
            Seat:      request.Seat,
            BookedAt:  DateTime.UtcNow
        );

        _tickets.Add(ticket);
        return CreatedAtAction(nameof(GetAll), new { id = ticket.Id }, ticket);
    }
}

public record Ticket(Guid Id, string EventName, string BookedBy, string Seat, DateTime BookedAt);
public record BookTicketRequest(string EventName, string BookedBy, string Seat);
