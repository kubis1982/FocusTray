using FocusTray.Core.Services;
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

        // Add JIRA integration
        services.AddHttpClient<IJiraService, JiraService>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });

        services.AddSingleton<JiraConfiguration>(provider =>
        {
            var settingsService = provider.GetRequiredService<SettingsService>();
            return settingsService.JiraConfiguration;
        });

        // Add dialogs
        services.AddTransient<SessionConfigDialogViewModel>();
        services.AddTransient<SessionConfigDialog>();
        services.AddTransient<Views.JiraSettingsDialog>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // Start MainWindow
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}