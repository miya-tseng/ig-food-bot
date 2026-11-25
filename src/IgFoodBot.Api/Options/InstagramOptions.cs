using System.ComponentModel.DataAnnotations;

namespace IgFoodBot.Api.Options;

public sealed class InstagramOptions
{
    public const string SectionName = "Instagram";

    [Required]
    public string VerifyToken { get; set; } = string.Empty;

    [Required]
    public string AppSecret { get; set; } = string.Empty;

    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";

    [Required]
    public string GraphApiVersion { get; set; } = "v27.0";

    public string[] AllowedSenderIds { get; set; } = [];

    public string? AccountId { get; set; }
}
