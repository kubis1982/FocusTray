using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Cached JIRA OAuth2 token state, persisted encrypted on disk.
/// </summary>
public class JiraTokenCacheData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string CloudId { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Persistent token cache for JIRA OAuth2 tokens, encrypted on disk using Windows DPAPI.
/// Analogous in spirit to MsalTokenCacheHelper, but for JIRA's own token model
/// (Atlassian has no MSAL equivalent).
/// </summary>
public class JiraTokenCacheHelper
{
    private static readonly object FileLock = new();
    private readonly string _cacheFilePath;
    private readonly ILogger? _logger;

    public JiraTokenCacheHelper(ILogger? logger = null, string? cacheDirectory = null)
    {
        _logger = logger;

        var directory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusTray");

        Directory.CreateDirectory(directory);
        _cacheFilePath = Path.Combine(directory, "jira_token_cache.dat");
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public void Save(JiraTokenCacheData data)
    {
        lock (FileLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_cacheFilePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving JIRA token cache to disk");
            }
        }
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public JiraTokenCacheData? Load()
    {
        lock (FileLock)
        {
            if (!File.Exists(_cacheFilePath))
            {
                return null;
            }

            try
            {
                var encryptedBytes = File.ReadAllBytes(_cacheFilePath);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<JiraTokenCacheData>(json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading JIRA token cache from disk; clearing corrupted cache");
                try
                {
                    File.Delete(_cacheFilePath);
                }
                catch
                {
                    // Ignore errors deleting corrupted cache
                }

                return null;
            }
        }
    }

    public void Clear()
    {
        lock (FileLock)
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing JIRA token cache from disk");
            }
        }
    }
}
