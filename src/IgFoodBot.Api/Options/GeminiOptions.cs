using System.ComponentModel.DataAnnotations;

namespace IgFoodBot.Api.Options;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    public string[] FallbackModels { get; set; } = [];

    public string? ThinkingLevel { get; set; } = "minimal";
}
