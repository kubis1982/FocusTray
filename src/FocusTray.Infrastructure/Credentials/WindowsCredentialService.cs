using FocusTray.Core.Services;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FocusTray.Infrastructure.Credentials;

/// <summary>
/// Credential storage implementation using Windows Data Protection API (DPAPI).
/// Credentials are encrypted per-user and stored in local app data folder.
/// </summary>
public class WindowsCredentialService : ICredentialService
{
    private readonly ILogger<WindowsCredentialService> _logger;
    private readonly string _credentialsDirectory;

    public WindowsCredentialService(ILogger<WindowsCredentialService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Store encrypted credentials in %LocalAppData%\FocusTray\credentials\
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _credentialsDirectory = Path.Combine(localAppData, "FocusTray", "credentials");
        
        // Ensure directory exists
        Directory.CreateDirectory(_credentialsDirectory);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public bool SaveCredentials(string target, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            _logger.LogWarning("Cannot save credentials: target is empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Cannot save credentials: username is empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Cannot save credentials: password is empty");
            return false;
        }

        try
        {
            var credentialData = new StoredCredential
            {
                Username = username,
                Password = password
            };

            // Serialize to JSON
            var json = JsonSerializer.Serialize(credentialData);
            var plainBytes = Encoding.UTF8.GetBytes(json);

            // Encrypt using DPAPI (per-user)
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null, // entropy
                DataProtectionScope.CurrentUser);

            // Save to file
            var filePath = GetCredentialFilePath(target);
            File.WriteAllBytes(filePath, encryptedBytes);

            _logger.LogInformation("Credentials saved successfully for target {Target}", target);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving credentials for target {Target}", target);
            return false;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public (string Username, string Password)? LoadCredentials(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            _logger.LogWarning("Cannot load credentials: target is empty");
            return null;
        }

        try
        {
            var filePath = GetCredentialFilePath(target);

            if (!File.Exists(filePath))
            {
                _logger.LogInformation("No credentials found for target {Target}", target);
                return null;
            }

            // Read encrypted file
            var encryptedBytes = File.ReadAllBytes(filePath);

            // Decrypt using DPAPI
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null, // entropy
                DataProtectionScope.CurrentUser);

            // Deserialize from JSON
            var json = Encoding.UTF8.GetString(plainBytes);
            var credentialData = JsonSerializer.Deserialize<StoredCredential>(json);

            if (credentialData == null || 
                string.IsNullOrWhiteSpace(credentialData.Username) || 
                string.IsNullOrWhiteSpace(credentialData.Password))
            {
                _logger.LogWarning("Credentials for target {Target} are incomplete", target);
                return null;
            }

            _logger.LogInformation("Credentials loaded successfully for target {Target}", target);
            return (credentialData.Username, credentialData.Password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading credentials for target {Target}", target);
            return null;
        }
    }

    public bool DeleteCredentials(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            _logger.LogWarning("Cannot delete credentials: target is empty");
            return false;
        }

        try
        {
            var filePath = GetCredentialFilePath(target);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("No credentials to delete for target {Target} (file does not exist)", target);
                return true; // Not an error - credentials don't exist
            }

            File.Delete(filePath);
            _logger.LogInformation("Credentials deleted successfully for target {Target}", target);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting credentials for target {Target}", target);
            return false;
        }
    }

    public bool HasStoredCredentials(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        try
        {
            var filePath = GetCredentialFilePath(target);
            return File.Exists(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if credentials exist for target {Target}", target);
            return false;
        }
    }

    private string GetCredentialFilePath(string target)
    {
        // Create a safe filename from target name
        var safeFileName = string.Join("_", target.Split(Path.GetInvalidFileNameChars())) + ".dat";
        return Path.Combine(_credentialsDirectory, safeFileName);
    }

    private class StoredCredential
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
