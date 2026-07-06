using System.Windows;
using customSTT.Services;

namespace customSTT;

public partial class App : Application
{
    private readonly AudioCaptureService _audioCaptureService;
    private readonly SpeechRecognitionService _speechRecognitionService;
    private readonly TextInjectionService _textInjectionService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly TranscriptionHistoryService _transcriptionHistoryService;
    private readonly OverlayService _overlayService;
    private readonly SettingsService _settingsService;

    public App()
    {
        _audioCaptureService = new AudioCaptureService();
        _speechRecognitionService = new SpeechRecognitionService();
        _textInjectionService = new TextInjectionService();
        _hotkeyService = new HotkeyService();
        _trayIconService = new TrayIconService();
        _transcriptionHistoryService = new TranscriptionHistoryService();
        _overlayService = new OverlayService();
        _settingsService = new SettingsService();
    }

    protected void OnStartup(object sender, StartupEventArgs e)
    {
        _overlayService.Initialize();
        _trayIconService.Initialize();

        var mainWindow = new MainWindow(
            _audioCaptureService,
            _speechRecognitionService,
            _textInjectionService,
            _hotkeyService,
            _trayIconService,
            _transcriptionHistoryService,
            _overlayService,
            _settingsService);

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService.UnregisterHotkeys();
        _audioCaptureService.Dispose();
        _speechRecognitionService.Dispose();
        _overlayService.Dispose();
        _trayIconService.Dispose();

        base.OnExit(e);
    }
}
