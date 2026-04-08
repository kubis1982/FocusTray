using FocusTray.Core.Models;
using FocusTray.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace FocusTray;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ITimerService _timerService = default!;
    private DispatcherTimer _uiUpdateTimer = default!;
    private SoundPlayer _notificationSound = default!;

    public MainWindow(ITimerService timerService)
    {
        InitializeComponent();

        _timerService = timerService;

        InitializeWindow();
    }

    private void InitializeWindow()
    {
        // Initialize UI update timer
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiUpdateTimer.Tick += UiUpdateTimer_Tick;

        // Initialize notification sound
        _notificationSound = new SoundPlayer();

        // Subscribe to timer service events
        _timerService.Tick += TimerService_Tick;
        _timerService.SessionCompleted += TimerService_SessionCompleted;
        _timerService.StateChanged += TimerService_StateChanged;

        // Hide window on startup
        WindowState = WindowState.Minimized;
        Hide();
    }

    private void StartSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sessionDialog = App.Services.GetRequiredService<SessionConfigDialog>();
            var result = sessionDialog.ShowDialog();

            if (result == true)
            {
                UpdateTrayMenuState();
                UpdateTrayIcon(true);
                _uiUpdateTimer?.Start();
            }
        }
        catch (Exception ex)
        {
            ShowErrorNotification("FocusTray Error", $"Error starting session: {ex.Message}");
        }
    }

    private void ViewSession_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.CurrentSession == null)
            return;

        try
        {
            var statusDialog = new SessionStatusDialog(_timerService);
            statusDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowErrorNotification("FocusTray Error", $"Error viewing session: {ex.Message}");
        }
    }

    private void EndSession_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.IsRunning == true)
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to end the current focus session?", 
                "FocusTray", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _timerService.StopSession();
                UpdateTrayMenuState();
                UpdateTrayIcon(false);
                _uiUpdateTimer?.Stop();
                UpdateTrayTooltip("FocusTray - Click to start focus session");
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void TimerService_Tick(object? sender, TimeSpan timeRemaining)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateTrayTooltip($"FocusTray - {FormatTime(timeRemaining)} remaining");
        });
    }

    private void TimerService_SessionCompleted(object? sender, FocusSession session)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _uiUpdateTimer.Stop();

            // Play notification sound
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch
            {
                // Log warning but continue execution
            }

            // Show completion notification
            ShowSessionCompletedNotification(session);

            UpdateTrayMenuState();
            UpdateTrayIcon(false);
            UpdateTrayTooltip("FocusTray - Session completed! Click to start new session");
        });
    }

    private void TimerService_StateChanged(object? sender, TimerState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateTrayMenuState();
            UpdateTrayIcon(state == TimerState.Running);
        });
    }

    private void UiUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_timerService.CurrentSession != null)
        {
            var timeRemaining = _timerService.CurrentSession.TimeRemaining;
            UpdateTrayTooltip($"FocusTray - {FormatTime(timeRemaining)} remaining");
        }
    }

    private void UpdateTrayMenuState()
    {
        var hasActiveSession = _timerService.IsRunning;

        StartSessionMenuItem.IsEnabled = !hasActiveSession;
        ViewSessionMenuItem.IsEnabled = hasActiveSession;
        EndSessionMenuItem.IsEnabled = hasActiveSession;
    }

    private void UpdateTrayTooltip(string tooltip)
    {
        TrayIcon.ToolTipText = tooltip;
    }

    private void UpdateTrayIcon(bool isActive)
    {
        var iconPath = isActive
            ? "pack://application:,,,/Resources/favicon-active.ico"
            : "pack://application:,,,/Resources/favicon.ico";

        TrayIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(
            new System.Uri(iconPath, System.UriKind.Absolute));
    }

    private void ShowSessionCompletedNotification(FocusSession session)
    {
        // Show completion notification with toast only
        ShowSuccessNotification(
            "Session Completed!",
            $"Task: {session.TaskDescription}\nDuration: {FormatTime(session.Duration)}"
        );
    }

    private void ShowSuccessNotification(string title, string message)
    {
        CreateNotificationWindow(title, message, ControlAppearance.Success, SymbolRegular.CheckmarkCircle24);
    }

    private void ShowInfoNotification(string title, string message)
    {
        CreateNotificationWindow(title, message, ControlAppearance.Info, SymbolRegular.Info24);
    }

    private void ShowErrorNotification(string title, string message)
    {
        CreateNotificationWindow(title, message, ControlAppearance.Danger, SymbolRegular.ErrorCircle24);
    }

    private void CreateNotificationWindow(string title, string message, ControlAppearance appearance, SymbolRegular iconSymbol)
    {
        // Create dedicated window for notification
        var notificationWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            Width = 350,
            Height = 120,
            Opacity = 0
        };

        // Position in bottom right corner of screen, accounting for taskbar
        var workingArea = SystemParameters.WorkArea;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var screenWidth = SystemParameters.PrimaryScreenWidth;

        // Calculate taskbar height
        var taskbarHeight = screenHeight - workingArea.Height;

        // Positioning with margins and taskbar consideration
        var marginRight = 20;
        var marginBottom = 70;  // Increased from 20 to 70 (50px additional space above taskbar)

        notificationWindow.Left = workingArea.Right - notificationWindow.Width - marginRight;

        // If taskbar is at bottom (standard configuration)
        if (workingArea.Bottom < screenHeight)
        {
            notificationWindow.Top = workingArea.Bottom - notificationWindow.Height - marginBottom;
        }
        // If taskbar is at top
        else if (workingArea.Top > 0)
        {
            notificationWindow.Top = screenHeight - notificationWindow.Height - taskbarHeight - marginBottom;
        }
        // Fallback - standard positioning
        else
        {
            notificationWindow.Top = workingArea.Bottom - notificationWindow.Height - marginBottom;
        }

        // Ensure notification doesn't go off screen
        if (notificationWindow.Top < 0)
        {
            notificationWindow.Top = marginBottom;
        }
        if (notificationWindow.Left < 0)
        {
            notificationWindow.Left = marginRight;
        }

        // Create content using WPF-UI InfoBar
        var infoBar = new InfoBar
        {
            Title = title,
            Message = message,
            Severity = appearance switch
            {
                ControlAppearance.Success => InfoBarSeverity.Success,
                ControlAppearance.Danger => InfoBarSeverity.Error,
                ControlAppearance.Caution => InfoBarSeverity.Warning,
                _ => InfoBarSeverity.Informational
            },
            IsOpen = true,
            Margin = new Thickness(10)
        };

        notificationWindow.Content = infoBar;
        notificationWindow.Show();

        // Fade-in animation
        var fadeInAnimation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromSeconds(0.3)
        };
        notificationWindow.BeginAnimation(Window.OpacityProperty, fadeInAnimation);

        // Auto-close after 5 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();

            // Fade-out animation before closing
            var fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            fadeOutAnimation.Completed += (sender, args) => notificationWindow.Close();
            notificationWindow.BeginAnimation(Window.OpacityProperty, fadeOutAnimation);
        };
        timer.Start();

        // Allow closing by clicking
        notificationWindow.MouseLeftButtonDown += (s, e) =>
        {
            timer.Stop();
            notificationWindow.Close();
        };
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiUpdateTimer?.Stop();

        _timerService.Tick -= TimerService_Tick;
        _timerService.SessionCompleted -= TimerService_SessionCompleted;
        _timerService.StateChanged -= TimerService_StateChanged;

        TrayIcon?.Dispose();
        _notificationSound?.Dispose();

        base.OnClosed(e);
    }
}