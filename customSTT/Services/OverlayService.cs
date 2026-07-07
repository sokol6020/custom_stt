using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using customSTT.Models;

namespace customSTT.Services;

public class OverlayService : IDisposable
{
    private const double WindowWidth = 230;
    private const double WindowHeight = 82;
    private const double ProgressTrackWidth = 168;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    public static readonly Color IdleColor = Color.FromRgb(156, 163, 175);
    public static readonly Color RecordingColor = Color.FromRgb(31, 157, 85);
    public static readonly Color ProcessingColor = Color.FromRgb(234, 179, 8);
    public static readonly Color DownloadingColor = Color.FromRgb(91, 141, 239);

    private static readonly SolidColorBrush IdleBrush = new(IdleColor);
    private static readonly SolidColorBrush RecordingBrush = new(RecordingColor);
    private static readonly SolidColorBrush ProcessingBrush = new(ProcessingColor);
    private static readonly SolidColorBrush DownloadingBrush = new(DownloadingColor);
    private static readonly SolidColorBrush ProcessingTextBrush = new(Color.FromRgb(255, 240, 170));
    private static readonly SolidColorBrush ProgressLabelBrush = new(Color.FromRgb(200, 200, 210));
    private static readonly SolidColorBrush TrackBrush = new(Color.FromRgb(45, 45, 55));

    private Window? _overlayWindow;
    private Ellipse? _indicator;
    private TextBlock? _statusText;
    private TextBlock? _progressText;
    private Border? _progressFill;
    private ScaleTransform? _progressScale;
    private bool _isVisible;
    private bool _isInitialized;
    private double _opacity = 0.3;
    private OverlayCorner _corner = OverlayCorner.TopRight;
    private int _screenIndex;
    private OverlayStatus _status = OverlayStatus.Idle;
    private double _progress;
    private string _progressMessage = "Готов к работе";
    private readonly DispatcherTimer _blinkTimer = new();

    static OverlayService()
    {
        IdleBrush.Freeze();
        RecordingBrush.Freeze();
        ProcessingBrush.Freeze();
        DownloadingBrush.Freeze();
        ProcessingTextBrush.Freeze();
        ProgressLabelBrush.Freeze();
        TrackBrush.Freeze();
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_isInitialized)
                return;

            _blinkTimer.Interval = TimeSpan.FromSeconds(0.45);
            _blinkTimer.Tick += OnBlinkTick;
            CreateOverlayWindow();
            _isInitialized = true;
        });
    }

    public void SetLayout(double opacity, OverlayCorner corner, int screenIndex)
    {
        _opacity = Math.Clamp(opacity, 0, 1.0);
        _corner = corner;
        _screenIndex = screenIndex;

        if (!_isInitialized)
            return;

        RunOnUi(() =>
        {
            if (_overlayWindow == null)
                return;

            _overlayWindow.Opacity = _opacity;
            PositionOverlayWindow();
        });
    }

    private void CreateOverlayWindow()
    {
        _overlayWindow = new Window
        {
            Width = WindowWidth,
            Height = WindowHeight,
            Opacity = _opacity,
            Topmost = true,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            SizeToContent = SizeToContent.Manual
        };

        _overlayWindow.SourceInitialized += OnOverlaySourceInitialized;

        var root = new Border
        {
            Width = WindowWidth,
            Height = WindowHeight,
            Background = new SolidColorBrush(Color.FromArgb(235, 20, 20, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 70, 70, 82)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10)
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _indicator = new Ellipse
        {
            Width = 22,
            Height = 22,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Fill = IdleBrush,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        _statusText = new TextBlock
        {
            Text = "ОЖИДАНИЕ",
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        row.Children.Add(_indicator);
        row.Children.Add(_statusText);
        Grid.SetRow(row, 0);
        layout.Children.Add(row);

        var progressArea = new Grid
        {
            Margin = new Thickness(34, 4, 0, 0)
        };

        _progressText = new TextBlock
        {
            Text = "Готов к работе",
            Foreground = ProgressLabelBrush,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 4),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Top
        };

        var trackHost = new Grid
        {
            Height = 8,
            Width = ProgressTrackWidth,
            VerticalAlignment = VerticalAlignment.Bottom,
            ClipToBounds = true
        };

        var progressTrack = new Border
        {
            Height = 8,
            Width = ProgressTrackWidth,
            Background = TrackBrush,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var progressFill = new Border
        {
            Height = 8,
            Width = ProgressTrackWidth,
            Background = IdleBrush,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransformOrigin = new Point(0, 0.5)
        };
        _progressFill = progressFill;
        _progressScale = new ScaleTransform(0, 1);
        progressFill.RenderTransform = _progressScale;

        trackHost.Children.Add(progressTrack);
        trackHost.Children.Add(progressFill);
        progressArea.Children.Add(_progressText);
        progressArea.Children.Add(trackHost);
        Grid.SetRow(progressArea, 1);
        layout.Children.Add(progressArea);

        root.Child = layout;
        _overlayWindow.Content = root;
        PositionOverlayWindow();
        _overlayWindow.Visibility = Visibility.Collapsed;
        _isVisible = false;
    }

    private void OnOverlaySourceInitialized(object? sender, EventArgs e)
    {
        MakeWindowClickThrough();
    }

    private void MakeWindowClickThrough()
    {
        if (_overlayWindow == null)
            return;

        var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private void PositionOverlayWindow()
    {
        if (_overlayWindow == null)
            return;

        var workArea = OverlayLayoutHelper.GetWorkArea(_screenIndex);
        var position = OverlayLayoutHelper.GetOverlayPosition(workArea, WindowWidth, WindowHeight, _corner);
        _overlayWindow.Left = position.X;
        _overlayWindow.Top = position.Y;
    }

    public void Show()
    {
        EnsureInitialized();
        RunOnUi(() =>
        {
            if (_overlayWindow == null || _isVisible)
                return;

            _overlayWindow.Show();
            _overlayWindow.Visibility = Visibility.Visible;
            _isVisible = true;
            ApplyVisualState();
        });
    }

    public void Hide()
    {
        if (!_isInitialized)
            return;

        RunOnUi(() =>
        {
            if (_overlayWindow == null || !_isVisible)
                return;

            _blinkTimer.Stop();
            _overlayWindow.Hide();
            _overlayWindow.Visibility = Visibility.Collapsed;
            _isVisible = false;
        });
    }

    public void SetStatus(OverlayStatus status)
    {
        _status = status;
        if (!_isVisible || _overlayWindow == null)
            return;

        RunOnUi(ApplyVisualState);
    }

    public void SetProgress(double percent, string? message = null)
    {
        _progress = Math.Clamp(percent, 0, 100);
        if (!string.IsNullOrWhiteSpace(message))
            _progressMessage = message;

        if (!_isVisible || _overlayWindow == null)
            return;

        RunOnUi(ApplyProgressOnly);
    }

    private void ApplyVisualState()
    {
        if (_indicator == null || _statusText == null)
            return;

        _indicator.Opacity = 1.0;
        _blinkTimer.Stop();

        switch (_status)
        {
            case OverlayStatus.Recording:
                _indicator.Fill = RecordingBrush;
                _statusText.Text = "ЗАПИСЬ";
                _statusText.Foreground = Brushes.White;
                if (string.IsNullOrWhiteSpace(_progressMessage) || _progressMessage == "Готов к работе")
                    _progressMessage = "Идёт запись...";
                _blinkTimer.Start();
                break;

            case OverlayStatus.Processing:
                _indicator.Fill = ProcessingBrush;
                _statusText.Text = "ОБРАБОТКА";
                _statusText.Foreground = ProcessingTextBrush;
                _blinkTimer.Start();
                break;

            case OverlayStatus.Downloading:
                _indicator.Fill = DownloadingBrush;
                _statusText.Text = "СКАЧИВАНИЕ";
                _statusText.Foreground = Brushes.White;
                _blinkTimer.Start();
                break;

            default:
                _indicator.Fill = IdleBrush;
                _statusText.Text = "ОЖИДАНИЕ";
                _statusText.Foreground = Brushes.White;
                if (!IsProcessingMessage(_progressMessage))
                    _progressMessage = "Готов к работе";
                _progress = 0;
                break;
        }

        ApplyProgressOnly();
    }

    private static bool IsProcessingMessage(string message)
    {
        return message.Contains("Загрузка", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Распознавание", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Редактирование", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Вставка", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Остановка", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyProgressOnly()
    {
        if (_progressText == null || _progressScale == null || _progressFill == null)
            return;

        _progressScale.ScaleX = _progress / 100.0;
        _progressText.Text = $"{_progressMessage} — {_progress:F0}%";

        _progressFill.Background = _status switch
        {
            OverlayStatus.Recording => RecordingBrush,
            OverlayStatus.Processing => ProcessingBrush,
            OverlayStatus.Downloading => DownloadingBrush,
            _ => IdleBrush
        };
    }

    private void OnBlinkTick(object? sender, EventArgs e)
    {
        if (_indicator == null || _status == OverlayStatus.Idle)
            return;

        _indicator.Opacity = _indicator.Opacity > 0.5 ? 0.4 : 1.0;
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            Initialize();
    }

    private void RunOnUi(Action action)
    {
        var dispatcher = _overlayWindow?.Dispatcher ?? Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        _blinkTimer.Stop();
        if (_isInitialized)
        {
            var dispatcher = _overlayWindow?.Dispatcher ?? Application.Current.Dispatcher;
            dispatcher.Invoke(() =>
            {
                _overlayWindow?.Close();
                _overlayWindow = null;
            });
        }

        _isVisible = false;
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }
}
