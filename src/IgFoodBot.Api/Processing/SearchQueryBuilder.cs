using IgFoodBot.Api.Gemini;

namespace IgFoodBot.Api.Processing;

public static class SearchQueryBuilder
{
    public static IReadOnlyList<string> Build(RestaurantInsight? insight)
    {
        if (insight is null)
        {
            return [];
        }

        var name = insight.RestaurantName?.Trim();
        var signatureDish = insight.RecommendedDishes?.FirstOrDefault();

        IEnumerable<string?> candidates = string.IsNullOrEmpty(name)
            ? insight.SearchQueries ?? []
            :
            [
                .. insight.SearchQueries ?? [],
                $"{name} {insight.Area}",
                $"{name} {insight.Area} {signatureDish}",
                $"{name} {signatureDish}"
            ];

        return candidates
            .Select(NormalizeSpaces)
            .Where(candidate => candidate.Length > 0)
            .Distinct()
            .ToList();
    }

    private static string NormalizeSpaces(string? candidate) =>
        string.Join(' ', (candidate ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
