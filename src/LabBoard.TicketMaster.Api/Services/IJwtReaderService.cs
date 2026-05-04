namespace LabBoard.TicketMaster.Api.Services;

public interface IJwtReaderService
{
    Guid? ExtractUserId(string? authorizationHeader);
}
