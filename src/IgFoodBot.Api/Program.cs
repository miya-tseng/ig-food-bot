using IgFoodBot.Api;
using IgFoodBot.Api.Health;
using IgFoodBot.Api.Webhook;

var builder = WebApplication.CreateBuilder(args);

if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddFoodBot(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "IG Food Bot is running");
app.MapWebhookEndpoints();
app.MapHealthEndpoints();

app.Run();
