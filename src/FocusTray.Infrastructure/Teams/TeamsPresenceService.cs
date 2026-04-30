using FocusTray.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Me.Presence.SetUserPreferredPresence;
using Microsoft.Graph.Me.Presence.SetStatusMessage;
using Microsoft.Kiota.Abstractions.Authentication;

namespace FocusTray.Infrastructure.Teams;

/// <summary>
/// Service for managing Microsoft Teams presence and status messages via Graph API.
/// </summary>
public class TeamsPresenceService : ITeamsPresenceService
{
    private readonly ITeamsAuthService _authService;
    private readonly ILogger<TeamsPresenceService> _logger;
    private GraphServiceClient? _graphClient;

    public TeamsPresenceService(
        ITeamsAuthService authService,
        ILogger<TeamsPresenceService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsEnabled => _authService.IsLoggedIn;

    public async Task<bool> SetFocusStatusAsync(string statusMessage, DateTime expiryDateTime)
    {
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("Cannot set focus status: not logged in to Teams");
            return false;
        }

        if (string.IsNullOrWhiteSpace(statusMessage))
        {
            _logger.LogWarning("Cannot set focus status: status message is empty");
            return false;
        }

        try
        {
            _logger.LogInformation("Setting Teams focus status with message: {Message}", statusMessage);

            // Ensure we have a valid Graph client
            var graphClient = await GetGraphClientAsync();
            if (graphClient == null)
            {
                _logger.LogError("Failed to initialize Graph client");
                return false;
            }

            // Step 1: Set presence to DoNotDisturb
            var presenceRequest = new SetUserPreferredPresencePostRequestBody
            {
                Availability = "DoNotDisturb",
                Activity = "DoNotDisturb",
                ExpirationDuration = TimeSpan.FromHours(24) // Maximum allowed by Graph API
            };

            await graphClient.Me.Presence.SetUserPreferredPresence.PostAsync(presenceRequest);
            _logger.LogInformation("Teams presence set to DoNotDisturb");

            // Step 2: Set status message with expiry
            var statusRequest = new SetStatusMessagePostRequestBody
            {
                StatusMessage = new PresenceStatusMessage
                {
                    Message = new ItemBody
                    {
                        Content = statusMessage,
                        ContentType = BodyType.Text
                    },
                    ExpiryDateTime = new DateTimeTimeZone
                    {
                        DateTime = expiryDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        TimeZone = TimeZoneInfo.Local.Id
                    }
                }
            };

            await graphClient.Me.Presence.SetStatusMessage.PostAsync(statusRequest);
            _logger.LogInformation("Teams status message set with expiry {Expiry}", expiryDateTime);

            return true;
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Graph API error setting Teams status: {ErrorCode} - {Message}", ex.ResponseStatusCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Teams focus status");
            return false;
        }
    }

    public async Task<bool> ClearStatusAsync()
    {
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("Cannot clear status: not logged in to Teams");
            return false;
        }

        try
        {
            _logger.LogInformation("Clearing Teams status");

            // Ensure we have a valid Graph client
            var graphClient = await GetGraphClientAsync();
            if (graphClient == null)
            {
                _logger.LogError("Failed to initialize Graph client");
                return false;
            }

            var statusRequest = new SetStatusMessagePostRequestBody
            {
                StatusMessage = new PresenceStatusMessage
                {
                    Message = new ItemBody
                    {
                        Content = "",
                        ContentType = BodyType.Text
                    },
                    ExpiryDateTime = null
                }
            };

            // Clear user preferred presence (returns to automatic status)
            await graphClient.Me.Presence.ClearUserPreferredPresence.PostAsync();
            await graphClient.Me.Presence.SetStatusMessage.PostAsync(statusRequest);
            _logger.LogInformation("Teams status cleared successfully");

            return true;
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Graph API error clearing Teams status: {ErrorCode} - {Message}", ex.ResponseStatusCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Teams status");
            return false;
        }
    }

    public async Task<string?> GetCurrentPresenceAsync()
    {
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("Cannot get presence: not logged in to Teams");
            return null;
        }

        try
        {
            // Ensure we have a valid Graph client
            var graphClient = await GetGraphClientAsync();
            if (graphClient == null)
            {
                _logger.LogError("Failed to initialize Graph client");
                return null;
            }

            var presence = await graphClient.Me.Presence.GetAsync();

            if (presence == null)
            {
                _logger.LogWarning("Failed to retrieve presence information");
                return null;
            }

            return presence.Availability;
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Graph API error getting presence: {ErrorCode} - {Message}", ex.ResponseStatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current presence");
            return null;
        }
    }

    /// <summary>
    /// Gets or creates a Graph API client with current access token.
    /// </summary>
    private async Task<GraphServiceClient?> GetGraphClientAsync()
    {
        try
        {
            // Get valid access token (auto-refreshes if needed)
            var accessToken = await _authService.GetAccessTokenAsync();
            
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("Could not obtain valid access token");
                return null;
            }

            // Create new Graph client with current token
            _graphClient = new GraphServiceClient(
                new BaseBearerTokenAuthenticationProvider(
                    new TokenProvider(accessToken)));

            return _graphClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Graph client");
            return null;
        }
    }

    /// <summary>
    /// Token provider for Microsoft.Graph authentication.
    /// </summary>
    private class TokenProvider(string accessToken) : IAccessTokenProvider
    {
        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(accessToken);
        }

        public AllowedHostsValidator AllowedHostsValidator => new();
    }
}
