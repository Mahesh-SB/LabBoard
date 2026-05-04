using LabBoard.TicketMaster.Api.Models;

namespace LabBoard.TicketMaster.Api.Services;

public class TicketService(
    IJwtReaderService    jwtReader,
    IServiceTokenService tokenService,
    IUserDetailsService  userService) : ITicketService
{
    private readonly List<Ticket> _tickets = [];

    public IReadOnlyList<Ticket> GetAll() => _tickets;

    public async Task<BookingResult> BookAsync(BookTicketRequest request, string? authorizationHeader)
    {
        var userId = jwtReader.ExtractUserId(authorizationHeader);
        if (userId is null)
            return BookingResult.Fail(401, "A valid user Bearer token is required.");

        var serviceToken = await tokenService.GetTokenAsync();
        if (serviceToken is null)
            return BookingResult.Fail(503, "Service token unavailable. Auth API may be down.");

        var user = await userService.GetUserAsync(userId.Value, serviceToken);
        if (user is null)
            return BookingResult.Fail(404, $"User '{userId}' not found.");

        var ticket = new Ticket(
            Id:         Guid.NewGuid(),
            EventName:  request.EventName,
            Seat:       request.Seat,
            BookedAt:   DateTime.UtcNow,
            UserId:     user.Id,
            UserName:   user.FullName,
            UserEmail:  user.Email,
            UserPhone:  user.Phone,
            UserGender: user.Gender);

        _tickets.Add(ticket);
        return BookingResult.Ok(ticket);
    }
}
