namespace IgFoodBot.Api.Processing;

public static class CuisineEmoji
{
    private const string Default = "🍽";

    private static readonly (string Keyword, string Emoji)[] KeywordEmojis =
    [
        ("ramen", "🍜"),
        ("noodle", "🍜"),
        ("sushi", "🍣"),
        ("japanese", "🍣"),
        ("korean", "🍲"),
        ("chicken", "🍗"),
        ("barbecue", "🍖"),
        ("steak", "🥩"),
        ("hamburger", "🍔"),
        ("pizza", "🍕"),
        ("italian", "🍝"),
        ("seafood", "🦐"),
        ("chinese", "🥟"),
        ("dumpling", "🥟"),
        ("thai", "🍛"),
        ("indian", "🍛"),
        ("vegetarian", "🥗"),
        ("vegan", "🥗"),
        ("breakfast", "🥞"),
        ("brunch", "🥞"),
        ("coffee", "☕"),
        ("cafe", "☕"),
        ("tea", "🍵"),
        ("bakery", "🥐"),
        ("ice_cream", "🍦"),
        ("dessert", "🍰"),
        ("bar", "🍺"),
        ("pub", "🍺")
    ];

    public static string For(string? placeType)
    {
        if (string.IsNullOrWhiteSpace(placeType))
        {
            return Default;
        }

        foreach (var (keyword, emoji) in KeywordEmojis)
        {
            if (placeType.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return emoji;
            }
        }

        return Default;
    }
}
