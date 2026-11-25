using IgFoodBot.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IgFoodBot.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(InstagramHealthCheck instagramHealthCheck) : ControllerBase
{
    [HttpGet("instagram")]
    public async Task<IActionResult> Instagram()
    {
        if (!instagramHealthCheck.IsConfigured)
        {
            return Problem("Instagram:AccountId is not configured", statusCode: StatusCodes.Status500InternalServerError);
        }

        return await instagramHealthCheck.IsHealthyAsync()
            ? Content("ok")
            : StatusCode(StatusCodes.Status503ServiceUnavailable, "instagram api unavailable");
    }
}
