using System.Security.Cryptography;
using System.Text;
using IgFoodBot.Api.Instagram;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Options;

namespace IgFoodBot.UnitTests.Instagram;

public class WebhookSignatureValidatorTests
{
    private const string AppSecret = "test-app-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"object":"instagram","entry":[]}""");

    private WebhookSignatureValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new WebhookSignatureValidator(Microsoft.Extensions.Options.Options.Create(new InstagramOptions { AppSecret = AppSecret }));
    }

    [Test]
    public void IsValid_WithCorrectSignature_ReturnsTrue()
    {
        Assert.That(_validator.IsValid(Body, Sign(Body, AppSecret)), Is.True);
    }

    [Test]
    public void IsValid_WithUppercaseHexSignature_ReturnsTrue()
    {
        var signature = "sha256=" + Sign(Body, AppSecret)["sha256=".Length..].ToUpperInvariant();

        Assert.That(_validator.IsValid(Body, signature), Is.True);
    }

    [Test]
    public void IsValid_WithSignatureFromAnotherSecret_ReturnsFalse()
    {
        Assert.That(_validator.IsValid(Body, Sign(Body, "another-secret")), Is.False);
    }

    [Test]
    public void IsValid_WhenBodyWasModified_ReturnsFalse()
    {
        var signature = Sign(Body, AppSecret);
        var modifiedBody = Encoding.UTF8.GetBytes("""{"object":"instagram","entry":[{}]}""");

        Assert.That(_validator.IsValid(modifiedBody, signature), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("sha1=abcdef")]
    [TestCase("sha256=")]
    [TestCase("sha256=not-hex")]
    [TestCase("sha256=abcd")]
    public void IsValid_WithMalformedHeader_ReturnsFalse(string? signatureHeader)
    {
        Assert.That(_validator.IsValid(Body, signatureHeader), Is.False);
    }

    private static string Sign(byte[] body, string secret) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
}
