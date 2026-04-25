using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace FocusTray.Infrastructure.Teams;

/// <summary>
/// Persistent token cache for MSAL that stores tokens encrypted on disk.
/// Tokens are encrypted using Windows DPAPI and stored in %LocalAppData%\FocusTray\msal_cache.
/// </summary>
public class MsalTokenCacheHelper
{
    private static readonly object FileLock = new object();
    private readonly string _cacheFilePath;
    private readonly ILogger? _logger;

    public MsalTokenCacheHelper(ILogger? logger = null)
    {
        _logger = logger;
        
        // Store MSAL cache in %LocalAppData%\FocusTray\msal_cache
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheDirectory = Path.Combine(localAppData, "FocusTray");
        
        Directory.CreateDirectory(cacheDirectory);
        _cacheFilePath = Path.Combine(cacheDirectory, "msal_cache.dat");
    }

    /// <summary>
    /// Registers cache serialization callbacks for the given token cache.
    /// </summary>
    public void EnableSerialization(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        lock (FileLock)
        {
            // Read cache from disk before MSAL accesses it
            if (File.Exists(_cacheFilePath))
            {
                try
                {
                    var encryptedData = File.ReadAllBytes(_cacheFilePath);
                    var decryptedData = ProtectedData.Unprotect(
                        encryptedData,
                        null,
                        DataProtectionScope.CurrentUser);
                    
                    args.TokenCache.DeserializeMsalV3(decryptedData);
                    _logger?.LogDebug("MSAL token cache loaded from disk");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error loading MSAL token cache from disk");
                    // If cache is corrupted, delete it
                    try
                    {
                        File.Delete(_cacheFilePath);
                    }
                    catch
                    {
                        // Ignore errors deleting corrupted cache
                    }
                }
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        // Write cache to disk after MSAL updates it (only if changed)
        if (args.HasStateChanged)
        {
            lock (FileLock)
            {
                try
                {
                    var data = args.TokenCache.SerializeMsalV3();
                    var encryptedData = ProtectedData.Protect(
                        data,
                        null,
                        DataProtectionScope.CurrentUser);
                    
                    File.WriteAllBytes(_cacheFilePath, encryptedData);
                    _logger?.LogDebug("MSAL token cache saved to disk");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error saving MSAL token cache to disk");
                }
            }
        }
    }

    /// <summary>
    /// Clears the token cache from disk.
    /// </summary>
    public void Clear()
    {
        lock (FileLock)
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                    _logger?.LogInformation("MSAL token cache cleared from disk");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing MSAL token cache from disk");
            }
        }
    }
}
