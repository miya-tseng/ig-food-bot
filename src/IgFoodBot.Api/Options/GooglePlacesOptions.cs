using System.ComponentModel.DataAnnotations;

namespace IgFoodBot.Api.Options;

public sealed class GooglePlacesOptions
{
    public const string SectionName = "GooglePlaces";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = "zh-TW";

    public string RegionCode { get; set; } = "TW";
}
