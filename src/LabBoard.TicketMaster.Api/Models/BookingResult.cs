namespace LabBoard.TicketMaster.Api.Models;

public class BookingResult
{
    public bool    IsSuccess  { get; private init; }
    public Ticket? Ticket     { get; private init; }
    public string? Error      { get; private init; }
    public int     HttpStatus { get; private init; }

    public static BookingResult Ok(Ticket ticket) =>
        new() { IsSuccess = true, Ticket = ticket };

    public static BookingResult Fail(int httpStatus, string error) =>
        new() { IsSuccess = false, HttpStatus = httpStatus, Error = error };
}
