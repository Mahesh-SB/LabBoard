using System.Net.Http.Headers;
using LabBoard.TicketMaster.Api.Configuration;
using LabBoard.TicketMaster.Api.Models;
using Microsoft.Extensions.Options;

namespace LabBoard.TicketMaster.Api.Services;

public class UserDetailsService(
    IHttpClientFactory httpClientFactory,
    IOptions<UserManagementApiOptions> options,
    ILogger<UserDetailsService> logger) : IUserDetailsService
{
    private readonly string _baseUrl = options.Value.BaseUrl;

    public async Task<UserDetails?> GetUserAsync(Guid userId, string serviceToken)
    {
        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);

        try
        {
            var response = await http.GetAsync($"{_baseUrl}/api/users/{userId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("User {UserId} not found in UserManagement API.", userId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("UserManagement API returned {Status} for user {UserId}.", response.StatusCode, userId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UserDetails>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch user details for {UserId} from UserManagement API.", userId);
            return null;
        }
    }
}
