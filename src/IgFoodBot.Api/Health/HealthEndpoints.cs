namespace IgFoodBot.Api.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/instagram", CheckInstagramAsync);
        return endpoints;
    }

    private static async Task<IResult> CheckInstagramAsync(InstagramHealthCheck healthCheck)
    {
        if (!healthCheck.IsConfigured)
        {
            return Results.Problem("Instagram:AccountId is not configured", statusCode: StatusCodes.Status500InternalServerError);
        }

        return await healthCheck.IsHealthyAsync()
            ? Results.Text("ok")
            : Results.Text("instagram api unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
