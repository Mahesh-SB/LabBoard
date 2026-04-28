using Microsoft.AspNetCore.Mvc;

namespace LabBoard.Gateway.Api.Controllers;

[ApiController]
[Route("test")]
public class TestController : ControllerBase
{
    [HttpGet("user-detail")]
    public IActionResult GetUserDetail()
    {
        return Ok(new
        {
            Id       = "usr-001",
            FullName = "Mahesh Singh",
            Email    = "mahesh@gmail.com",
            Role     = "Admin",
            Age      = 28,
            Phone    = "+91-9876543210"
        });
    }
}
