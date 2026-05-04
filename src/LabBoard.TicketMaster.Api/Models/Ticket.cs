namespace LabBoard.TicketMaster.Api.Models;

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
