using LabBoard.UserManagement.Api.Models.User;
using LabBoard.UserManagement.Api.Services.User;
using Microsoft.AspNetCore.Mvc;

namespace LabBoard.UserManagement.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IUserManagementService userService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
        => Ok(await userService.GetAllAsync());

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await userService.GetByIdAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var user = await userService.UpdateRoleAsync(id, request.Role);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var user = await userService.UpdateStatusAsync(id, request.IsActive);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await userService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
