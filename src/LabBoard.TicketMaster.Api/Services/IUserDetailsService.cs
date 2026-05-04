using LabBoard.TicketMaster.Api.Models;

namespace LabBoard.TicketMaster.Api.Services;

public interface IUserDetailsService
{
    Task<UserDetails?> GetUserAsync(Guid userId, string serviceToken);
}
