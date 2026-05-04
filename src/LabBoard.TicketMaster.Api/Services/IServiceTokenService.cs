namespace LabBoard.TicketMaster.Api.Services;

public interface IServiceTokenService
{
    Task<string?> GetTokenAsync();
}
