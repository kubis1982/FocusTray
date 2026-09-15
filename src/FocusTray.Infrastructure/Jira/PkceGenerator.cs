using System.Security.Cryptography;
using System.Text;
using IdentityModel;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Generates PKCE (RFC 7636) code verifier/challenge pairs and OAuth state values
/// for the JIRA Authorization Code + PKCE flow.
/// </summary>
public static class PkceGenerator
{
    public static string GenerateCodeVerifier() =>
        CryptoRandom.CreateUniqueId(32, CryptoRandom.OutputFormat.Base64Url);

    public static string GenerateState() =>
        CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Base64Url);

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        var verifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = SHA256.HashData(verifierBytes);
        return Base64Url.Encode(hash);
    }
}
