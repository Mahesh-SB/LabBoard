using LabBoard.TicketMaster.Api.Models;
using LabBoard.TicketMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LabBoard.TicketMaster.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Ticket>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(ticketService.GetAll());

    [HttpPost]
    [ProducesResponseType(typeof(Ticket), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Book([FromBody] BookTicketRequest request)
    {
        var result = await ticketService.BookAsync(request, Request.Headers.Authorization);

        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetAll), new { id = result.Ticket!.Id }, result.Ticket);

        return result.HttpStatus switch
        {
            401 => Unauthorized(new { error = result.Error }),
            404 => NotFound(new { error = result.Error }),
            503 => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = result.Error }),
            _   => BadRequest(new { error = result.Error })
        };
    }
}
