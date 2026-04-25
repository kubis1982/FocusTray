using FocusTray.Core.Services;
using FocusTray.Infrastructure.Credentials;
using FocusTray.Infrastructure.Jira;
using FocusTray.Services;
using FocusTray.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Net.Http;
using System.Windows;

namespace FocusTray;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services => ((App)Current)._serviceProvider!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Setup dependency injection
        var services = new ServiceCollection();

        // Configure logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/focustray.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog());

        // Add settings
        services.AddSingleton<SettingsService>();

        // Add core services
        services.AddSingleton<ITimerService, TimerService>();
        
        // Add credential service
        services.AddSingleton<ICredentialService, WindowsCredentialService>();

        // Add JIRA configuration (shared singleton)
        services.AddSingleton<JiraConfiguration>(provider =>
        {
            var settingsService = provider.GetRequiredService<SettingsService>();
            return settingsService.JiraConfiguration;
        });
        
        // Add JIRA authentication service
        services.AddSingleton<IJiraAuthService, JiraAuthService>();

        // Add JIRA integration with HttpClient
        services.AddHttpClient<IJiraService, JiraService>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });

        // Add dialogs
        services.AddTransient<JiraLoginDialogViewModel>();
        services.AddTransient<Views.JiraLoginDialog>();
        services.AddTransient<Views.JiraAdvancedSettingsDialog>();
        services.AddTransient<SessionConfigDialogViewModel>();
        services.AddTransient<SessionConfigDialog>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // Start MainWindow
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        // Try auto-login to JIRA in background (non-blocking)
        _ = TryAutoLoginAsync();

        base.OnStartup(e);
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            var authService = _serviceProvider!.GetRequiredService<IJiraAuthService>();
            await authService.TryAutoLoginAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't show to user
            Log.Warning(ex, "Auto-login to JIRA failed");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}