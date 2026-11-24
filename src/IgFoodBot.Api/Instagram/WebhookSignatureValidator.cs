using System.Security.Cryptography;
using System.Text;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Instagram;

public sealed class WebhookSignatureValidator(IOptions<InstagramOptions> options)
{
    private const string SignaturePrefix = "sha256=";

    public bool IsValid(ReadOnlySpan<byte> requestBody, string? signatureHeader)
    {
        if (signatureHeader is null || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] providedHash;
        try
        {
            providedHash = Convert.FromHexString(signatureHeader.AsSpan(SignaturePrefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var secret = Encoding.UTF8.GetBytes(options.Value.AppSecret);
        var expectedHash = HMACSHA256.HashData(secret, requestBody);
        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }
}
