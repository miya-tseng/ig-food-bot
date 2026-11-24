using IgFoodBot.Api.Currency;
using IgFoodBot.Api.Gemini;
using IgFoodBot.Api.Instagram;
using IgFoodBot.Api.Options;
using IgFoodBot.Api.Places;
using IgFoodBot.Api.Processing;

namespace IgFoodBot.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFoodBot(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<InstagramOptions>(configuration, InstagramOptions.SectionName);
        services.AddValidatedOptions<GeminiOptions>(configuration, GeminiOptions.SectionName);
        services.AddValidatedOptions<GooglePlacesOptions>(configuration, GooglePlacesOptions.SectionName);

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<ProcessedMessageTracker>();
        services.AddSingleton<IncomingMessageFilter>();
        services.AddSingleton<WebhookSignatureValidator>();
        services.AddScoped<MessageProcessor>();

        services.AddHttpClient<InstagramMessenger>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<GeminiClient>(client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<PlacesClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<ExchangeRateClient>(client => client.Timeout = TimeSpan.FromSeconds(5));

        return services;
    }

    private static void AddValidatedOptions<TOptions>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
