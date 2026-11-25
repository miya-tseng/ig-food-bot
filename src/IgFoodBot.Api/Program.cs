using IgFoodBot.Api;

var builder = WebApplication.CreateBuilder(args);

if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddFoodBot(builder.Configuration);

var app = builder.Build();

app.MapControllers();

app.Run();
