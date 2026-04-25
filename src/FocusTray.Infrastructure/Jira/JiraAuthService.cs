using FocusTray.Core.Services;
using Kubis1982.Atlassian.Jira.RestClient.V2;
using Kubis1982.Atlassian.RestClient;
using Microsoft.Extensions.Logging;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Service for managing JIRA authentication state.
/// </summary>
public class JiraAuthService : IJiraAuthService
{
    private const string CredentialTarget = "FocusTray_Jira";
    
    private readonly ICredentialService _credentialService;
    private readonly JiraConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraAuthService> _logger;
    
    private string? _currentUsername;
    private string? _currentUserEmail;
    private string? _currentCompany;
    private bool _isLoggedIn;

    public JiraAuthService(
        ICredentialService credentialService,
        JiraConfiguration configuration,
        HttpClient httpClient,
        ILogger<JiraAuthService> logger)
    {
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsLoggedIn => _isLoggedIn;
    
    public string? CurrentUsername => _currentUsername;
    
    public string? CurrentUserEmail => _currentUserEmail;
    
    public string? CurrentCompany => _currentCompany;

    public event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

    public async Task<bool> LoginAsync(string company, string email, string apiToken)
    {
        if (string.IsNullOrWhiteSpace(company))
        {
            _logger.LogWarning("Cannot login: company is empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Cannot login: email is empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            _logger.LogWarning("Cannot login: API token is empty");
            return false;
        }

        try
        {
            _logger.LogInformation("Attempting to login to JIRA for company {Company}", company);

            // Verify credentials by calling JIRA API
            var basicAuthProvider = new BasicAuthProvider(email, apiToken);
            var jiraClient = JiraRestClient.Create(company, basicAuthProvider, _httpClient);

            var user = await jiraClient.Rest.Api.Two.Myself.GetAsync();

            if (user == null)
            {
                _logger.LogWarning("Login failed: unable to retrieve user information");
                return false;
            }

            // Save credentials to secure storage
            var credentialsSaved = _credentialService.SaveCredentials(
                CredentialTarget,
                email,
                apiToken);

            if (!credentialsSaved)
            {
                _logger.LogError("Failed to save credentials to secure storage");
                return false;
            }

            // Update configuration with company (not sensitive)
            _configuration.Company = company;

            // Update state
            _currentUsername = user.DisplayName ?? user.EmailAddress ?? email;
            _currentUserEmail = email;
            _currentCompany = company;
            _isLoggedIn = true;

            _logger.LogInformation("Successfully logged in as {Username}", _currentUsername);

            // Raise event
            OnAuthStateChanged(new AuthStateChangedEventArgs(true, _currentUsername));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login to JIRA");
            return false;
        }
    }

    public async Task<bool> LogoutAsync()
    {
        try
        {
            _logger.LogInformation("Logging out from JIRA");

            // Delete credentials from secure storage
            var deleted = _credentialService.DeleteCredentials(CredentialTarget);

            if (!deleted)
            {
                _logger.LogWarning("Failed to delete credentials (may not exist)");
            }

            // Clear state
            _currentUsername = null;
            _currentUserEmail = null;
            _currentCompany = null;
            _isLoggedIn = false;

            _logger.LogInformation("Logged out successfully");

            // Raise event
            OnAuthStateChanged(new AuthStateChangedEventArgs(false, null));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout from JIRA");
            return false;
        }
    }

    public async Task<string?> GetCurrentUserAsync()
    {
        if (!_isLoggedIn)
        {
            _logger.LogWarning("Cannot get current user: not logged in");
            return null;
        }

        try
        {
            var credentials = _credentialService.LoadCredentials(CredentialTarget);
            if (credentials == null)
            {
                _logger.LogWarning("Credentials not found");
                return null;
            }

            if (string.IsNullOrWhiteSpace(_currentCompany))
            {
                _logger.LogWarning("Company not configured");
                return null;
            }

            var basicAuthProvider = new BasicAuthProvider(credentials.Value.Username, credentials.Value.Password);
            var jiraClient = JiraRestClient.Create(_currentCompany, basicAuthProvider, _httpClient);

            var user = await jiraClient.Rest.Api.Two.Myself.GetAsync();

            if (user == null)
            {
                _logger.LogWarning("Unable to retrieve user information");
                return null;
            }

            return user.DisplayName ?? user.EmailAddress ?? credentials.Value.Username;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user from JIRA");
            return null;
        }
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            _logger.LogInformation("Attempting auto-login to JIRA");

            // Check if credentials exist
            if (!_credentialService.HasStoredCredentials(CredentialTarget))
            {
                _logger.LogInformation("No stored credentials found for auto-login");
                return false;
            }

            // Load credentials
            var credentials = _credentialService.LoadCredentials(CredentialTarget);
            if (credentials == null)
            {
                _logger.LogWarning("Failed to load stored credentials");
                return false;
            }

            // Load company from configuration
            if (string.IsNullOrWhiteSpace(_configuration.Company))
            {
                _logger.LogWarning("Company not configured, cannot auto-login");
                return false;
            }

            // Try to login
            var success = await LoginAsync(_configuration.Company, credentials.Value.Username, credentials.Value.Password);

            if (success)
            {
                _logger.LogInformation("Auto-login successful");
            }
            else
            {
                _logger.LogWarning("Auto-login failed");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-login to JIRA");
            return false;
        }
    }

    protected virtual void OnAuthStateChanged(AuthStateChangedEventArgs e)
    {
        AuthStateChanged?.Invoke(this, e);
    }
}
