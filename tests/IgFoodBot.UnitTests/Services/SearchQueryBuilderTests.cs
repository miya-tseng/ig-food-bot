using IgFoodBot.Api.Gemini;
using IgFoodBot.Api.Processing;

namespace IgFoodBot.UnitTests.Processing;

public class SearchQueryBuilderTests
{
    [Test]
    public void Build_WithNullInsight_ReturnsEmpty()
    {
        Assert.That(SearchQueryBuilder.Build(null), Is.Empty);
    }

    [Test]
    public void Build_WithGeminiQueries_PutsThemBeforeFallbacks()
    {
        var insight = CreateInsight(searchQueries: ["PARU 景福宮 一隻雞", "파루 닭한마리"], dishes: ["一隻雞火鍋"]);

        var queries = SearchQueryBuilder.Build(insight);

        Assert.That(queries, Is.EqualTo(new[]
        {
            "PARU 景福宮 一隻雞",
            "파루 닭한마리",
            "PARU 首爾鐘路區",
            "PARU 首爾鐘路區 一隻雞火鍋",
            "PARU 一隻雞火鍋"
        }));
    }

    [Test]
    public void Build_WithoutRestaurantName_ReturnsGeminiQueriesOnly()
    {
        var insight = CreateInsight(restaurantName: "  ", searchQueries: ["景福宮 一隻雞"]);

        var queries = SearchQueryBuilder.Build(insight);

        Assert.That(queries, Is.EqualTo(new[] { "景福宮 一隻雞" }));
    }

    [Test]
    public void Build_WithMissingAreaAndDishes_RemovesExtraSpacesAndDuplicates()
    {
        var insight = CreateInsight(area: null, searchQueries: ["PARU"]);

        var queries = SearchQueryBuilder.Build(insight);

        Assert.That(queries, Is.EqualTo(new[] { "PARU" }));
    }

    [Test]
    public void Build_WithBlankGeminiQueries_SkipsThem()
    {
        var insight = CreateInsight(searchQueries: ["", "   "]);

        var queries = SearchQueryBuilder.Build(insight);

        Assert.That(queries, Is.EqualTo(new[] { "PARU 首爾鐘路區", "PARU" }));
    }

    private static RestaurantInsight CreateInsight(
        string? restaurantName = "PARU",
        string? area = "首爾鐘路區",
        List<string>? searchQueries = null,
        List<string>? dishes = null) =>
        new(
            IsRestaurantPost: true,
            RestaurantName: restaurantName,
            Area: area,
            CountryCode: "KR",
            SearchQueries: searchQueries,
            Summary: null,
            RecommendedDishes: dishes);
}
