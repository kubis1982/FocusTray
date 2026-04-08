using System.Windows;
using System.Windows.Threading;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Collections.Generic;

namespace FocusTray;

public partial class SessionStatusDialog : Window
{
    private readonly ITimerService _timerService;
    private readonly DispatcherTimer _updateTimer;

    // Keep references to hand shapes so we can rotate/update them
    private Line? _hourHand;
    private Line? _minuteHand;
    private Line? _secondHand;

    // Canvas acquired from XAML at runtime (use FindName to avoid generated-field issues)
    private Canvas? _timeRemainingCanvas;

    // Rim ticks and center text
    private readonly List<Shape> _rimTicks = new List<Shape>();
    private TextBlock? _centerText;

    public SessionStatusDialog(ITimerService timerService)
    {
        InitializeComponent();
        // Acquire canvas element from the loaded XAML
        _timeRemainingCanvas = (Canvas?)FindName("TimeRemainingCanvas");
        _timerService = timerService;
        
        // Initialize update timer to refresh display every second
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        
        // Subscribe to timer service events
        _timerService.SessionCompleted += TimerService_SessionCompleted;
        _timerService.StateChanged += TimerService_StateChanged;
        
        LoadSessionData();
        _updateTimer.Start();
    }

    private void LoadSessionData()
    {
        var session = _timerService.CurrentSession;
        if (session == null)
        {
            Close();
            return;
        }

        TaskDescriptionText.Text = session.TaskDescription;
        SessionDurationText.Text = FormatTime(session.Duration);
        InitializeAnalogClock();
        UpdateTimeRemaining();
    }

    private void InitializeAnalogClock()
    {
        // Clear any existing elements
        if (_timeRemainingCanvas == null)
            return;

        _timeRemainingCanvas.Children.Clear();

        double size = Math.Min(_timeRemainingCanvas.Width, _timeRemainingCanvas.Height);
        double radius = size / 2;
        Point center = new Point(radius, radius);

        // Draw clock face (circle)
        var face = new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = Brushes.DarkBlue,
            StrokeThickness = 3,
            Fill = Brushes.White
        };
        Canvas.SetLeft(face, 0);
        Canvas.SetTop(face, 0);
        _timeRemainingCanvas.Children.Add(face);

        // Draw rim ticks (60 ticks, color updated to show progress)
        const int totalTicks = 60;
        for (int i = 0; i < totalTicks; i++)
        {
            double angle = i * 6 * Math.PI / 180.0; // 6 degrees per tick
            double inner = radius - 10;
            double outer = radius - 2;
            // make every 5th tick slightly longer/thicker
            if (i % 5 == 0)
            {
                inner = radius - 14;
                outer = radius - 2;
            }

            var x1 = center.X + inner * Math.Sin(angle);
            var y1 = center.Y - inner * Math.Cos(angle);
            var x2 = center.X + outer * Math.Sin(angle);
            var y2 = center.Y - outer * Math.Cos(angle);

            var mark = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = Brushes.LightGray,
                StrokeThickness = (i % 5 == 0) ? 3 : 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _rimTicks.Add(mark);
            _timeRemainingCanvas.Children.Add(mark);
        }

        // Center text showing remaining minutes/seconds
        _centerText = new TextBlock
        {
            Width = size,
            TextAlignment = TextAlignment.Center,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black
        };
        // Place roughly centered vertically
        Canvas.SetLeft(_centerText, 0);
        Canvas.SetTop(_centerText, center.Y - 20);
        _timeRemainingCanvas.Children.Add(_centerText);
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateTimeRemaining();
    }

    private void UpdateTimeRemaining()
    {
        var session = _timerService.CurrentSession;
        if (session == null)
        {
            _updateTimer.Stop();
            Close();
            return;
        }

        // Update textual time as tooltip for accessibility
        var remaining = session.TimeRemaining;
        if (_timeRemainingCanvas != null)
            _timeRemainingCanvas.ToolTip = FormatTime(remaining);

        DrawAnalogCountdown(remaining, session.Duration);
        
        // Update button states based on session state
        ExtendButton.IsEnabled = _timerService.IsRunning;
        EndSessionButton.IsEnabled = _timerService.IsRunning;
    }

    private void DrawAnalogCountdown(TimeSpan remaining, TimeSpan total)
    {
        if (_timeRemainingCanvas == null)
            return;

        double size = Math.Min(_timeRemainingCanvas.Width, _timeRemainingCanvas.Height);
        double radius = size / 2;
        Point center = new Point(radius, radius);

        // Fraction of total remaining (0..1)
        double fraction = 0;
        if (total.TotalSeconds > 0)
            fraction = Math.Max(0, Math.Min(1, remaining.TotalSeconds / total.TotalSeconds));

        // Update rim ticks: color first N ticks as progressed (progress = 1 - fraction)
        double progress = 1.0 - fraction; // 0 at start, 1 when complete
        int totalTicks = _rimTicks.Count > 0 ? _rimTicks.Count : 60;
        int lit = (int)Math.Round(progress * totalTicks);
        for (int i = 0; i < totalTicks; i++)
        {
            var tick = _rimTicks[i] as Line;
            if (tick == null) continue;
            if (i < lit)
            {
                tick.Stroke = Brushes.DodgerBlue;
            }
            else
            {
                tick.Stroke = Brushes.LightGray;
            }
        }

        // Update center text: show minutes remaining; when less than 1 minute show seconds
        if (_centerText != null)
        {
            if (remaining.TotalSeconds >= 60)
            {
                int mins = (int)Math.Ceiling(remaining.TotalMinutes);
                _centerText.Text = $"{mins} min.";
                _centerText.FontSize = 28;
            }
            else
            {
                int secs = Math.Max(0, (int)remaining.TotalSeconds);
                _centerText.Text = $"{secs} s";
                _centerText.FontSize = 32;
            }
        }
    }

    private void TimerService_SessionCompleted(object? sender, FocusSession session)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _updateTimer.Stop();
            Close();
        });
    }

    private void TimerService_StateChanged(object? sender, TimerState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (state == TimerState.Idle || state == TimerState.Completed)
            {
                _updateTimer.Stop();
                Close();
            }
        });
    }

    private void ExtendButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Extend current session by 5 minutes?", 
            "Extend Session", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _timerService.ExtendSession(TimeSpan.FromMinutes(5));
            MessageBox.Show("Session extended by 5 minutes.", 
                "FocusTray", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void EndSessionButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to end the current focus session?", 
            "End Session", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _timerService.StopSession();
            MessageBox.Show("Focus session ended.", 
                "FocusTray", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
        _updateTimer.Stop();
        _timerService.SessionCompleted -= TimerService_SessionCompleted;
        _timerService.StateChanged -= TimerService_StateChanged;
        
        base.OnClosed(e);
    }
}