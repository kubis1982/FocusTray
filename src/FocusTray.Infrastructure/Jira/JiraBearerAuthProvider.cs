using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Kiota authentication provider that attaches a Bearer access token obtained
/// on demand (with silent refresh handled by the delegate's owner, JiraAuthService).
/// </summary>
public class JiraBearerAuthProvider(Func<Task<string?>> accessTokenProvider) : IAuthenticationProvider
{
    private readonly Func<Task<string?>> _accessTokenProvider =
        accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await _accessTokenProvider();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Cannot authenticate JIRA request: no access token available.");
        }

        request.Headers.TryAdd("Authorization", $"Bearer {accessToken}");
    }
}
