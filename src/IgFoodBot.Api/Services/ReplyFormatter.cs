using System.Globalization;
using IgFoodBot.Api.Clients;
using IgFoodBot.Api.Models;

namespace IgFoodBot.Api.Services;

public static class ReplyFormatter
{
    public const string NoContent = "我沒有讀到貼文內容 😢\n可以直接打店名給我（例如：鼎泰豐 信義店），我幫你找 Google Maps。";
    public const string NotRecognized = "這篇我認不出是哪一家店 🤔\n可以直接打店名給我（例如：鼎泰豐 信義店），我幫你找 Google Maps。";
    public const string Failed = "處理時出了點問題，請稍後再試一次 🙏";

    private static readonly Dictionary<string, string> CurrencySymbols = new()
    {
        ["TWD"] = "NT$",
        ["KRW"] = "₩",
        ["JPY"] = "¥",
        ["USD"] = "US$",
        ["HKD"] = "HK$",
        ["EUR"] = "€",
        ["GBP"] = "£",
        ["THB"] = "฿"
    };

    private static readonly string[] WeekdayNames = ["週日", "週一", "週二", "週三", "週四", "週五", "週六"];

    public static IReadOnlyList<OutgoingMessage> Format(RestaurantInsight insight, PlaceResult? place, string searchQuery, PriceRange? priceInTwd, DateTimeOffset now)
    {
        var card = place is null
            ? BuildNotFoundCard(insight, searchQuery)
            : BuildPlaceCard(insight, place, priceInTwd, now);

        return BuildInsightText(insight) is { } insightText
            ? [card, new TextMessage(insightText)]
            : [card];
    }

    private static LinkCardMessage BuildPlaceCard(RestaurantInsight insight, PlaceResult place, PriceRange? priceInTwd, DateTimeOffset now)
    {
        var title = $"{CuisineEmoji.For(place.PrimaryType)} {BuildTitle(insight.RestaurantName, place.Name)}";
        var statusLine = string.Join(" · ", new[] { FormatRating(place), FormatOpeningStatus(place.OpeningStatus, now) }.OfType<string>());
        var priceLine = FormatPrice(place.PriceRange, priceInTwd) is { } price ? $"💰 {price}" : null;
        var subtitle = string.Join("\n", new[] { statusLine, priceLine }.Where(line => !string.IsNullOrEmpty(line)));

        return new LinkCardMessage(title, subtitle, "📍 開啟地圖", place.MapsUrl);
    }

    private static LinkCardMessage BuildNotFoundCard(RestaurantInsight insight, string searchQuery) =>
        new(
            $"🤔 {insight.RestaurantName ?? searchQuery}",
            "Google 地圖沒有精準找到這家店，點下方看搜尋結果",
            "🔍 在地圖搜尋",
            PlacesClient.BuildSearchUrl(searchQuery));

    private static string? BuildInsightText(RestaurantInsight insight)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(insight.Summary))
        {
            lines.Add($"📝 {insight.Summary.Trim()}");
        }

        if (insight.RecommendedDishes is { Count: > 0 } dishes)
        {
            lines.Add($"👍 {string.Join("、", dishes)}");
        }

        return lines.Count > 0 ? string.Join('\n', lines) : null;
    }

    private static string BuildTitle(string? postName, string placeName)
    {
        var primaryName = postName?.Split(['(', '（'], 2)[0].Trim();
        if (string.IsNullOrEmpty(primaryName) || placeName.Contains(primaryName, StringComparison.OrdinalIgnoreCase))
        {
            return placeName;
        }

        return $"{primaryName}（{placeName}）";
    }

    private static string? FormatRating(PlaceResult place)
    {
        if (place.Rating is not { } rating)
        {
            return null;
        }

        var count = place.UserRatingCount is { } userRatingCount ? $"（{userRatingCount:N0}）" : string.Empty;
        return $"⭐ {rating:0.0}{count}";
    }

    private static string? FormatOpeningStatus(OpeningStatus? status, DateTimeOffset now)
    {
        if (status is null)
        {
            return null;
        }

        if (status.IsOpenNow)
        {
            return status.NextChangeTime is { } closeTime ? $"🟢 營業中，{FormatTime(closeTime, now)} 打烊" : "🟢 營業中";
        }

        return status.NextChangeTime is { } openTime ? $"🔴 已打烊，{FormatTime(openTime, now)} 開門" : "🔴 已打烊";
    }

    private static string FormatTime(DateTimeOffset time, DateTimeOffset now)
    {
        var localNow = now.ToOffset(time.Offset);
        var dayDifference = (time.Date - localNow.Date).Days;
        var dayLabel = dayDifference switch
        {
            0 => string.Empty,
            1 => "明天 ",
            _ => $"{WeekdayNames[(int)time.DayOfWeek]} "
        };

        return $"{dayLabel}{time:HH:mm}";
    }

    private static string? FormatPrice(PriceRange? originalPrice, PriceRange? priceInTwd)
    {
        var original = FormatPriceRange(originalPrice);
        if (priceInTwd is null || originalPrice is null || originalPrice.CurrencyCode == priceInTwd.CurrencyCode)
        {
            return original;
        }

        return $"{FormatPriceRange(priceInTwd)}（{original}）";
    }

    private static string? FormatPriceRange(PriceRange? priceRange)
    {
        if (priceRange is null)
        {
            return null;
        }

        var symbol = CurrencySymbols.GetValueOrDefault(priceRange.CurrencyCode, $"{priceRange.CurrencyCode} ");
        var start = $"{symbol}{FormatAmount(priceRange.StartPrice)}";
        return priceRange.EndPrice is { } end ? $"{start}–{FormatAmount(end)}" : $"{start} 起";
    }

    private static string FormatAmount(decimal amount) => amount.ToString("N0", CultureInfo.InvariantCulture);
}
