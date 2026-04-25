using FocusTray.Infrastructure.Jira;
using FocusTray.Infrastructure.Teams;
using System.IO;
using System.Text.Json;

namespace FocusTray.Services;

/// <summary>
/// Service for managing application settings.
/// </summary>
public class SettingsService
{
    private readonly string _settingsFilePath;
    private AppSettings? _settings;

    public SettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FocusTray");

        Directory.CreateDirectory(appDataPath);
        _settingsFilePath = Path.Combine(appDataPath, "settings.json");
    }

    /// <summary>
    /// Gets the current JIRA configuration.
    /// </summary>
    public JiraConfiguration JiraConfiguration => LoadSettings().Jira;

    /// <summary>
    /// Saves the JIRA configuration.
    /// </summary>
    public void SaveJiraConfiguration(JiraConfiguration jiraConfig)
    {
        var settings = LoadSettings();
        settings.Jira = jiraConfig;
        SaveSettings(settings);
    }

    private AppSettings LoadSettings()
    {
        if (_settings != null)
            return _settings;

        if (!File.Exists(_settingsFilePath))
        {
            _settings = new AppSettings();
            return _settings;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            _settings = new AppSettings();
        }

        return _settings;
    }

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true 
        };
        
        var json = JsonSerializer.Serialize(settings, options);
        File.WriteAllText(_settingsFilePath, json);
    }
}

/// <summary>
/// Application settings model.
/// </summary>
public class AppSettings
{
    public JiraConfiguration Jira { get; set; } = new();
    public TeamsConfiguration Teams { get; set; } = new();
}
