using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using customSTT.Models;
using customSTT.Services;
using customSTT.ViewModels;

namespace customSTT;

public partial class MainWindow : Window
{
    private readonly OverlayService _overlayService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly TextInjectionService _textInjectionService;
    private MainViewModel _viewModel;
    private readonly WindowIconManager _iconManager = new();
    private bool _isExiting;

    public void PrepareExit() => _isExiting = true;

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    public MainWindow(
        AudioCaptureService audioCaptureService,
        SpeechRecognitionService speechRecognitionService,
        TextInjectionService textInjectionService,
        HotkeyService hotkeyService,
        TrayIconService trayIconService,
        TranscriptionHistoryService transcriptionHistoryService,
        OverlayService overlayService,
        TextEditorService textEditorService,
        UpdateService updateService,
        AutoStartService autoStartService,
        SettingsService settingsService)
    {
        _overlayService = overlayService;
        _hotkeyService = hotkeyService;
        _trayIconService = trayIconService;
        _textInjectionService = textInjectionService;

        _viewModel = new MainViewModel(
            audioCaptureService,
            speechRecognitionService,
            textInjectionService,
            hotkeyService,
            trayIconService,
            transcriptionHistoryService,
            overlayService,
            textEditorService,
            updateService,
            autoStartService,
            settingsService);

        InitializeComponent();
        Title = $"Speech to Text v{AppVersion.Current}";
        _iconManager.Apply(this);
        DataContext = _viewModel;

        _hotkeyService.HotkeyActivated += OnHotkeyActivated;
        _hotkeyService.HotkeyDeactivated += OnHotkeyDeactivated;
        _hotkeyService.OverlayHotkeyActivated += OnOverlayHotkeyActivated;
        Closing += MainWindow_Closing;
        Loaded += OnMainWindowLoaded;
    }

    private void OnMainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnMainWindowLoaded;

        if (StartupOptions.MinimizeToTrayOnStartup || _viewModel.MinimizeToTrayOnStartup)
            _trayIconService.Minimize();

        _viewModel.ApplyOverlayAfterStartup();
        _ = _viewModel.ScheduleDeferredOverlayRefreshAsync();

        _viewModel.ScheduleStartupUpdateCheck();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var hwndSource = HwndSource.FromHwnd(hwnd);
        hwndSource?.AddHook(WndProc);

        _hotkeyService.SetWindowHandle(hwnd);
        _viewModel.SetHotkeysReady();
        if (!_viewModel.ApplyRecordingHotkeyMode())
        {
            System.Windows.MessageBox.Show(
                $"Не удалось зарегистрировать горячую клавишу «{_viewModel.HotkeyDisplay}». " +
                "Возможно, она уже занята другим приложением. Используется Ctrl+Alt+A.",
                "Горячие клавиши",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            _viewModel.HotkeyDisplay = "Ctrl+Alt+A";
            _viewModel.Settings.Hotkey = "Ctrl+Alt+A";
            _viewModel.ApplyRecordingHotkeyMode();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY)
        {
            _hotkeyService.NotifyHotkeyPressed(wParam.ToInt32());
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnHotkeyActivated()
    {
        Dispatcher.Invoke(() =>
        {
            if (_hotkeyService.RecordingMode == RecordingHotkeyMode.Hold)
            {
                if (!_viewModel.IsRecording && !_viewModel.IsProcessing)
                {
                    _textInjectionService.RememberTargetWindow();
                    _viewModel.StartRecordingCommand.Execute(null);
                }
                return;
            }

            if (!_viewModel.IsRecording)
                _textInjectionService.RememberTargetWindow();

            _viewModel.ToggleRecordingCommand.Execute(null);
        });
    }

    private void OnHotkeyDeactivated()
    {
        Dispatcher.Invoke(() =>
        {
            if (_hotkeyService.RecordingMode == RecordingHotkeyMode.Hold && _viewModel.IsRecording)
                _viewModel.StopRecordingCommand.Execute(null);
        });
    }

    private void OnOverlayHotkeyActivated()
    {
        Dispatcher.Invoke(() => _viewModel.ToggleOverlayCommand.Execute(null));
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.MinimizedToTray && !_isExiting)
        {
            e.Cancel = true;
            _trayIconService.Minimize();
            return;
        }

        _overlayService.Hide();
        _hotkeyService.UnregisterHotkeys();
    }

    protected override void OnClosed(EventArgs e)
    {
        _iconManager.Dispose();
        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!_viewModel.IsCapturingHotkey)
            return;

        if (e.Key == Key.Escape)
        {
            _viewModel.StopCaptureHotkey();
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None &&
            e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
        {
            return;
        }

        _viewModel.OnHotkeyKeyDown(e.Key, modifiers);
        e.Handled = true;
    }
}
