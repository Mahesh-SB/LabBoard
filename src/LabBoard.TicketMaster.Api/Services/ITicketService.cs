using LabBoard.TicketMaster.Api.Models;

namespace LabBoard.TicketMaster.Api.Services;

public interface ITicketService
{
    IReadOnlyList<Ticket> GetAll();
    Task<BookingResult> BookAsync(BookTicketRequest request, string? authorizationHeader);
}
