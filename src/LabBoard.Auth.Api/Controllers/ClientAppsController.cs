using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Services.Client;
using Microsoft.AspNetCore.Mvc;

namespace LabBoard.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientAppsController(IClientAppService clientAppService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(ClientAppResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] ClientAppRequest request)
    {
        try
        {
            var app = await clientAppService.RegisterAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = app.Id }, app);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientAppResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var app = await clientAppService.GetByIdAsync(id);
        return app is null ? NotFound() : Ok(app);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClientAppResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var apps = await clientAppService.GetAllAsync();
        return Ok(apps);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await clientAppService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
