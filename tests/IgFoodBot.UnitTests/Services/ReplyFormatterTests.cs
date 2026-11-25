using IgFoodBot.Api.Gemini;
using IgFoodBot.Api.Instagram;
using IgFoodBot.Api.Places;
using IgFoodBot.Api.Processing;

namespace IgFoodBot.UnitTests.Processing;

public class ReplyFormatterTests
{
    private static readonly TimeSpan SeoulOffset = TimeSpan.FromHours(9);
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, SeoulOffset);
    private const string MapsUrl = "https://maps.google.com/?cid=1";

    [Test]
    public void Format_WithFoundPlace_ReturnsCardThenInsightText()
    {
        var replies = ReplyFormatter.Format(CreateInsight(), CreatePlace(), "PARU 首爾", null, Now);

        Assert.Multiple(() =>
        {
            Assert.That(replies, Has.Count.EqualTo(2));
            Assert.That(replies[0], Is.TypeOf<LinkCardMessage>());
            Assert.That(replies[1], Is.EqualTo(new TextMessage("📝 韓方一隻雞名店\n👍 一隻雞火鍋、綠豆煎餅")));
        });
    }

    [Test]
    public void Format_WithFoundPlace_CardOpensPlaceOnMap()
    {
        var card = FormatCard(CreateInsight(), CreatePlace());

        Assert.Multiple(() =>
        {
            Assert.That(card.ButtonTitle, Is.EqualTo("📍 開啟地圖"));
            Assert.That(card.Url, Is.EqualTo(MapsUrl));
        });
    }

    [Test]
    public void Format_WhenPlaceNameDiffersFromPostName_ShowsBothNames()
    {
        var card = FormatCard(CreateInsight(restaurantName: "PARU"), CreatePlace(name: "파루 닭한마리"));

        Assert.That(card.Title, Is.EqualTo("🍗 PARU（파루 닭한마리）"));
    }

    [Test]
    public void Format_WhenPlaceNameContainsPostName_ShowsPlaceNameOnly()
    {
        var card = FormatCard(CreateInsight(restaurantName: "鼎泰豐"), CreatePlace(name: "鼎泰豐 信義店"));

        Assert.That(card.Title, Is.EqualTo("🍗 鼎泰豐 信義店"));
    }

    [Test]
    public void Format_WhenPostNameHasParentheses_UsesTextBeforeParentheses()
    {
        var card = FormatCard(CreateInsight(restaurantName: "PARU (파루)"), CreatePlace(name: "파루 닭한마리"));

        Assert.That(card.Title, Is.EqualTo("🍗 PARU（파루 닭한마리）"));
    }

    [Test]
    public void Format_WhenOpenNow_ShowsRatingAndClosingTime()
    {
        var closing = new DateTimeOffset(2026, 11, 24, 21, 30, 0, SeoulOffset);
        var place = CreatePlace(openingStatus: new OpeningStatus(true, closing));

        var card = FormatCard(CreateInsight(), place);

        Assert.That(card.Subtitle, Is.EqualTo("⭐ 4.8（1,234） · 🟢 營業中，21:30 打烊"));
    }

    [Test]
    public void Format_WhenClosedUntilTomorrow_ShowsTomorrowOpeningTime()
    {
        var opening = new DateTimeOffset(2026, 11, 25, 11, 0, 0, SeoulOffset);
        var place = CreatePlace(openingStatus: new OpeningStatus(false, opening));

        var card = FormatCard(CreateInsight(), place);

        Assert.That(card.Subtitle, Does.EndWith("🔴 已打烊，明天 11:00 開門"));
    }

    [Test]
    public void Format_WhenClosedForSeveralDays_ShowsWeekdayOfOpening()
    {
        var opening = new DateTimeOffset(2026, 11, 27, 11, 0, 0, SeoulOffset);
        var place = CreatePlace(openingStatus: new OpeningStatus(false, opening));

        var card = FormatCard(CreateInsight(), place);

        Assert.That(card.Subtitle, Does.EndWith("🔴 已打烊，週五 11:00 開門"));
    }

    [Test]
    public void Format_WithForeignPriceAndConversion_ShowsTwdThenOriginalPrice()
    {
        var place = CreatePlace(priceRange: new PriceRange("KRW", 20000, 30000));
        var priceInTwd = new PriceRange("TWD", 490, 730);

        var card = FormatCard(CreateInsight(), place, priceInTwd);

        Assert.That(card.Subtitle, Does.EndWith("\n💰 NT$490–730（₩20,000–30,000）"));
    }

    [Test]
    public void Format_WithForeignPriceWithoutConversion_ShowsOriginalPrice()
    {
        var place = CreatePlace(priceRange: new PriceRange("JPY", 1000, 2000));

        var card = FormatCard(CreateInsight(), place);

        Assert.That(card.Subtitle, Does.EndWith("\n💰 ¥1,000–2,000"));
    }

    [Test]
    public void Format_WithTwdPrice_ShowsPriceOnce()
    {
        var price = new PriceRange("TWD", 300, 500);
        var place = CreatePlace(priceRange: price);

        var card = FormatCard(CreateInsight(), place, price);

        Assert.That(card.Subtitle, Does.EndWith("\n💰 NT$300–500"));
    }

    [Test]
    public void Format_WithStartPriceOnly_ShowsStartingPrice()
    {
        var price = new PriceRange("TWD", 200, null);
        var place = CreatePlace(priceRange: price);

        var card = FormatCard(CreateInsight(), place, price);

        Assert.That(card.Subtitle, Does.EndWith("\n💰 NT$200 起"));
    }

    [Test]
    public void Format_WithoutRatingStatusOrPrice_ReturnsEmptySubtitle()
    {
        var place = CreatePlace(rating: null, userRatingCount: null);

        var card = FormatCard(CreateInsight(), place);

        Assert.That(card.Subtitle, Is.Empty);
    }

    [Test]
    public void Format_WhenPlaceNotFound_ReturnsSearchCard()
    {
        var replies = ReplyFormatter.Format(CreateInsight(restaurantName: "PARU"), null, "PARU 首爾", null, Now);
        var card = (LinkCardMessage)replies[0];

        Assert.Multiple(() =>
        {
            Assert.That(card.Title, Is.EqualTo("🤔 PARU"));
            Assert.That(card.ButtonTitle, Is.EqualTo("🔍 在地圖搜尋"));
            Assert.That(card.Url, Is.EqualTo(PlacesClient.BuildSearchUrl("PARU 首爾")));
        });
    }

    [Test]
    public void Format_WithoutSummaryOrDishes_ReturnsCardOnly()
    {
        var insight = CreateInsight() with { Summary = null, RecommendedDishes = null };

        var replies = ReplyFormatter.Format(insight, CreatePlace(), "PARU", null, Now);

        Assert.That(replies, Has.Count.EqualTo(1));
    }

    [TestCase("chicken_restaurant", "🍗")]
    [TestCase("korean_restaurant", "🍲")]
    [TestCase("ramen_restaurant", "🍜")]
    [TestCase("coffee_shop", "☕")]
    [TestCase("restaurant", "🍽")]
    [TestCase(null, "🍽")]
    public void CuisineEmoji_ForPlaceType_ReturnsMatchingEmoji(string? placeType, string expected)
    {
        Assert.That(CuisineEmoji.For(placeType), Is.EqualTo(expected));
    }

    private static LinkCardMessage FormatCard(RestaurantInsight insight, PlaceResult place, PriceRange? priceInTwd = null) =>
        (LinkCardMessage)ReplyFormatter.Format(insight, place, "query", priceInTwd, Now)[0];

    private static RestaurantInsight CreateInsight(string? restaurantName = "PARU") =>
        new(
            IsRestaurantPost: true,
            RestaurantName: restaurantName,
            Area: "首爾鐘路區",
            CountryCode: "KR",
            SearchQueries: ["PARU 景福宮"],
            Summary: "韓方一隻雞名店",
            RecommendedDishes: ["一隻雞火鍋", "綠豆煎餅"]);

    private static PlaceResult CreatePlace(
        string name = "파루 닭한마리",
        double? rating = 4.8,
        int? userRatingCount = 1234,
        OpeningStatus? openingStatus = null,
        PriceRange? priceRange = null) =>
        new(name, MapsUrl, "chicken_restaurant", rating, userRatingCount, openingStatus, priceRange);
}
