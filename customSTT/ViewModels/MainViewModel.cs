using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using customSTT.Models;
using customSTT.Services;

namespace customSTT.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AudioCaptureService _audioCaptureService;
    private readonly SpeechRecognitionService _speechRecognitionService;
    private readonly TextInjectionService _textInjectionService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly TranscriptionHistoryService _transcriptionHistoryService;
    private readonly OverlayService _overlayService;
    private readonly SettingsService _settingsService;

    private AppSettings? _settings;
    public AppSettings Settings
    {
        get => _settings ??= new AppSettings();
        set => SetProperty(ref _settings, value);
    }

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingStatus = "Готов";

    private readonly ObservableCollection<string> _audioDevices = new();
    public ObservableCollection<string> AudioDevices => _audioDevices;

    [ObservableProperty]
    private string? _selectedAudioDevice;

    [ObservableProperty]
    private string _selectedModel = "base";

    [ObservableProperty]
    private int _selectedModelIndex = 1;

    [ObservableProperty]
    private string _selectedLanguage = "auto";

    [ObservableProperty]
    private int _selectedLanguageIndex = 0;

    public static readonly string[] SupportedModels = { "tiny", "base", "small", "medium" };
    public static readonly string[] SupportedLanguages = { "auto", "ru", "en", "de", "fr", "es", "it", "ja", "zh" };

    public string[] AvailableModels => SupportedModels;
    public string[] AvailableLanguages => SupportedLanguages;

    [ObservableProperty]
    private string _hotkeyDisplay = "Ctrl+Alt+A";

    private bool _isLoadingSettings;
    private bool _hotkeysReady;

    [ObservableProperty]
    private int _hotkeyModeIndex;

    public void SetHotkeysReady() => _hotkeysReady = true;

    public static readonly string[] HotkeyModeOptions =
    {
        "Нажатие (вкл/выкл)",
        "Удержание (зажать для записи)"
    };

    public string[] AvailableHotkeyModes => HotkeyModeOptions;

    public bool IsHotkeyHoldMode => HotkeyModeIndex == 1;

    public string HotkeyHintText => IsHotkeyHoldMode
        ? $"Удерживайте {HotkeyDisplay} для записи"
        : $"Горячая клавиша: {HotkeyDisplay}";

    [ObservableProperty]
    private string _hotkeyInputText = "";

    [ObservableProperty]
    private bool _isCapturingHotkey = false;

    [ObservableProperty]
    private string _historyCountText = "0 записей";

    private readonly ObservableCollection<TranscriptionEntry> _history = new();
    public ObservableCollection<TranscriptionEntry> History => _history;

    [ObservableProperty]
    private TranscriptionEntry? _selectedHistoryEntry;

    [ObservableProperty]
    private bool _isOverlayVisible;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _minimizedToTray = true;

    [ObservableProperty]
    private bool _minimizeToTrayOnStartup;

    [ObservableProperty]
    private bool _useGpu = true;

    [ObservableProperty]
    private int _historyLimit = 20;

    [ObservableProperty]
    private string _outputFormat = "plainText";

    [ObservableProperty]
    private double _overlayOpacity = 0.3;

    [ObservableProperty]
    private string _overlayHotkey = "F1";

    [ObservableProperty]
    private string _overlayStatusText = "Ожидание";

    [ObservableProperty]
    private string _overlayStatusColor = "#888888";

    [ObservableProperty]
    private string _overlayToggleText = "Вкл";

    [ObservableProperty]
    private string _overlayRecordingIndicator = "";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _processingProgress;

    [ObservableProperty]
    private string _processingStageText = "Готов к работе";

    [ObservableProperty]
    private string _progressCaptionText = "Готов к работе";

    partial void OnHotkeyDisplayChanged(string value)
    {
        OnPropertyChanged(nameof(HotkeyHintText));
    }

    partial void OnHotkeyModeIndexChanged(int value)
    {
        Settings.HotkeyMode = value == 1 ? "hold" : "toggle";
        OnPropertyChanged(nameof(IsHotkeyHoldMode));
        OnPropertyChanged(nameof(HotkeyHintText));
        if (!_isLoadingSettings && _hotkeysReady)
            ApplyRecordingHotkeyMode();
        SaveSettings();
    }

    partial void OnSelectedModelIndexChanged(int value)
    {
        if (value >= 0 && value < SupportedModels.Length)
            SelectedModel = SupportedModels[value];
        SaveSettings();
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (value >= 0 && value < SupportedLanguages.Length)
            SelectedLanguage = SupportedLanguages[value];
        SaveSettings();
    }

    partial void OnSelectedAudioDeviceChanged(string? value) => SaveSettings();

    partial void OnIsOverlayVisibleChanged(bool value)
    {
        if (value)
            _overlayService.Show();
        else
            _overlayService.Hide();

        Settings.IsOverlayVisible = value;
        SaveSettings();
        UpdateOverlayPanelStatus();
        UpdateOverlayState();
    }

    partial void OnIsRecordingChanged(bool value)
    {
        UpdateOverlayState();
        SyncTrayStatus();
        (StartRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (StopRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnProcessingProgressChanged(double value)
    {
        SyncOverlayProgress();
    }

    partial void OnProcessingStageTextChanged(string value)
    {
        ProgressCaptionText = value;
        SyncOverlayProgress();
    }

    partial void OnIsProcessingChanged(bool value)
    {
        if (!value)
        {
            ProcessingProgress = 0;
            ProcessingStageText = "Готов к работе";
            ProgressCaptionText = "Готов к работе";
        }

        UpdateOverlayState();
        SyncTrayStatus();
        (StartRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (StopRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnAutoStartChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnMinimizedToTrayChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnMinimizeToTrayOnStartupChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnUseGpuChanged(bool value)
    {
        SaveSettings();
        _ = _speechRecognitionService.LoadModelAsync(SelectedModel, SelectedLanguage, value);
    }

    partial void OnHistoryLimitChanged(int value)
    {
        SaveSettings();
    }

    partial void OnOutputFormatChanged(string value)
    {
        SaveSettings();
    }

    partial void OnOverlayOpacityChanged(double value)
    {
        SaveSettings();
    }

    partial void OnOverlayHotkeyChanged(string value)
    {
        SaveSettings();
        if (!string.IsNullOrEmpty(value) && IsValidHotkey(value))
            ApplyRecordingHotkeyMode();
    }

    public bool ApplyRecordingHotkeyMode()
    {
        _hotkeyService.SetRecordingHotkeyMode(
            IsHotkeyHoldMode ? RecordingHotkeyMode.Hold : RecordingHotkeyMode.Toggle);

        if (!_hotkeysReady)
            return true;

        return _hotkeyService.RegisterHotkeys(HotkeyDisplay, OverlayHotkey);
    }

    private void UpdateOverlayState()
    {
        OverlayStatus status;
        if (IsRecording)
        {
            status = OverlayStatus.Recording;
            OverlayRecordingIndicator = "● Запись";
        }
        else if (IsProcessing)
        {
            status = OverlayStatus.Processing;
            OverlayRecordingIndicator = "● Обработка";
        }
        else
        {
            status = OverlayStatus.Idle;
            OverlayRecordingIndicator = "○ Ожидание";
        }

        _overlayService.SetStatus(status);
        SyncOverlayProgress();
    }

    private void SyncOverlayProgress()
    {
        if (IsProcessing)
        {
            _overlayService.SetProgress(ProcessingProgress, ProcessingStageText);
            return;
        }

        if (IsRecording)
        {
            _overlayService.SetProgress(0, "Идёт запись...");
            return;
        }

        _overlayService.SetProgress(0, "Готов к работе");
    }

    private void UpdateOverlayPanelStatus()
    {
        if (IsOverlayVisible)
        {
            OverlayStatusText = "Отображается";
            OverlayStatusColor = "#1F9D55";
            OverlayToggleText = "Выкл";
        }
        else
        {
            OverlayStatusText = "Скрыт";
            OverlayStatusColor = "#E84855";
            OverlayToggleText = "Вкл";
        }
    }

    private void SyncTrayStatus()
    {
        var trayStatus = IsRecording
            ? TrayIconStatus.Recording
            : IsProcessing
                ? TrayIconStatus.Processing
                : TrayIconStatus.Ready;

        _trayIconService.SetStatus(trayStatus);
    }

    public ICommand ToggleRecordingCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand ToggleOverlayCommand { get; }
    public ICommand ChangeHotkeyCommand { get; }
    public ICommand StartCaptureHotkeyCommand { get; }
    public ICommand StopCaptureHotkeyCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand OpenHistoryCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AboutCommand { get; }

    public MainViewModel(
        AudioCaptureService audioCaptureService,
        SpeechRecognitionService speechRecognitionService,
        TextInjectionService textInjectionService,
        HotkeyService hotkeyService,
        TrayIconService trayIconService,
        TranscriptionHistoryService transcriptionHistoryService,
        OverlayService overlayService,
        SettingsService settingsService)
    {
        _audioCaptureService = audioCaptureService;
        _speechRecognitionService = speechRecognitionService;
        _textInjectionService = textInjectionService;
        _hotkeyService = hotkeyService;
        _trayIconService = trayIconService;
        _transcriptionHistoryService = transcriptionHistoryService;
        _overlayService = overlayService;
        _settingsService = settingsService;

        ToggleRecordingCommand = new RelayCommand(ToggleRecording);
        StartRecordingCommand = new RelayCommand(StartRecordingCore, () => !IsRecording && !IsProcessing);
        StopRecordingCommand = new RelayCommand(StopRecordingCore, () => IsRecording);
        ToggleOverlayCommand = new RelayCommand(ToggleOverlay);
        ChangeHotkeyCommand = new RelayCommand(StartCaptureHotkey);
        StartCaptureHotkeyCommand = new RelayCommand(CaptureHotkeyInput);
        StopCaptureHotkeyCommand = new RelayCommand(StopCaptureHotkey);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        OpenHistoryCommand = new RelayCommand(OpenHistoryWindow);
        ExitCommand = new RelayCommand(ExitApp);
        AboutCommand = new RelayCommand(ShowAbout);

        LoadSettings();
        InitializeAudioDevices();
        InitializeHotkeyDisplay();
        LoadHistory();
        _history.CollectionChanged += OnHistoryCollectionChanged;
        UpdateHistoryCountText();

        if (IsOverlayVisible)
            _overlayService.Show();

        UpdateOverlayPanelStatus();
        UpdateOverlayState();

        _ = _speechRecognitionService.LoadModelAsync(SelectedModel, SelectedLanguage, UseGpu);
    }

    private void OnHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateHistoryCountText();
    }

    private void UpdateHistoryCountText()
    {
        HistoryCountText = FormatHistoryCount(_history.Count);
    }

    private static string FormatHistoryCount(int count)
    {
        var n = Math.Abs(count) % 100;
        var n1 = n % 10;
        var word = (n > 10 && n < 20) || n1 == 0 || n1 >= 5
            ? "записей"
            : n1 == 1
                ? "запись"
                : "записи";
        return $"{count} {word}";
    }

    private void ToggleRecording()
    {
        if (IsRecording)
        {
            StopRecordingCore();
        }
        else
        {
            StartRecordingCore();
        }
    }

    private async void StartRecordingCore()
    {
        try
        {
            _textInjectionService.RememberTargetWindow();

            IsRecording = true;
            IsProcessing = false;
            ProcessingProgress = 0;
            ProcessingStageText = "Идёт запись...";
            ProgressCaptionText = "Идёт запись...";
            RecordingStatus = "Запись...";
            UpdateOverlayState();
            SyncTrayStatus();

            var deviceIndex = _audioCaptureService.FindDeviceIndexByName(SelectedAudioDevice);
            await _audioCaptureService.StartCaptureAsync(SelectedAudioDevice, deviceIndex);
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Ошибка: {ex.Message}";
            IsRecording = false;
            UpdateOverlayState();
            SyncTrayStatus();
            _textInjectionService.ClearTargetWindow();
        }
    }

    private async void StopRecordingCore()
    {
        try
        {
            IsRecording = false;
            IsProcessing = true;
            ProcessingProgress = 5;
            ProcessingStageText = "Остановка записи...";
            RecordingStatus = ProcessingStageText;
            UpdateOverlayState();
            SyncTrayStatus();

            await _audioCaptureService.StopCaptureAsync();
            ProcessingProgress = 15;
            var pcmData = _audioCaptureService.GetAllCapturedData();

            if (pcmData.Length == 0)
            {
                RecordingStatus = "Нет данных";
                return;
            }

            ProcessingStageText = $"Загрузка модели {SelectedModel}...";
            RecordingStatus = ProcessingStageText;
            ProcessingProgress = 25;

            var modelLoaded = await _speechRecognitionService.LoadModelAsync(SelectedModel, SelectedLanguage, UseGpu);
            if (!modelLoaded)
            {
                RecordingStatus = "Ошибка: модель не загружена";
                return;
            }

            ProcessingStageText = "Распознавание речи...";
            RecordingStatus = ProcessingStageText;
            ProcessingProgress = 35;

            var progress = new Progress<double>(value =>
            {
                if (Math.Abs(ProcessingProgress - value) >= 2 || value >= 98)
                    ProcessingProgress = value;
            });

            var result = await _speechRecognitionService.RecognizeAsync(
                pcmData, SelectedModel, SelectedLanguage, progress, UseGpu);

            ProcessingStageText = "Вставка текста...";
            RecordingStatus = ProcessingStageText;
            ProcessingProgress = 96;

            if (!string.IsNullOrEmpty(result.Text))
            {
                await _textInjectionService.InjectTextAsync(result.Text);

                var entry = new TranscriptionEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Text = result.Text,
                    Language = result.Language,
                    Model = SelectedModel,
                    DurationSeconds = result.Duration,
                    Timestamp = DateTime.Now
                };
                _transcriptionHistoryService.AddToHistory(entry);
                _history.Add(entry);

                ProcessingProgress = 100;
                RecordingStatus = "Готов";
            }
            else
            {
                RecordingStatus = "Ничего не распознано";
            }
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Ошибка: {ex.Message}";
            IsRecording = false;
        }
        finally
        {
            IsProcessing = false;
            ProcessingProgress = 0;
            ProcessingStageText = "Готов к работе";
            ProgressCaptionText = "Готов к работе";
            UpdateOverlayState();
            SyncTrayStatus();
            _textInjectionService.ClearTargetWindow();
        }

        SaveSettings();
    }

    private void ToggleOverlay()
    {
        IsOverlayVisible = !IsOverlayVisible;
    }

    private void LoadSettings()
    {
        try
        {
            _isLoadingSettings = true;
            Settings = _settingsService.Load();

            string hotkey = Settings.Hotkey;
            if (string.IsNullOrEmpty(hotkey) || !IsValidHotkey(hotkey))
            {
                hotkey = "Ctrl+Alt+A";
                Settings.Hotkey = hotkey;
                SaveSettings();
            }

            SelectedModel = Settings.Model ?? "base";
            SelectedLanguage = Settings.Language ?? "auto";
            HotkeyDisplay = hotkey;
            HotkeyModeIndex = string.Equals(Settings.HotkeyMode, "hold", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            AutoStart = Settings.AutoStart;
            MinimizedToTray = Settings.MinimizedToTray;
            MinimizeToTrayOnStartup = Settings.MinimizeToTrayOnStartup;
            UseGpu = Settings.UseGpu;
            HistoryLimit = Settings.HistoryLimit;
            OutputFormat = Settings.OutputFormat ?? "plainText";
            OverlayOpacity = Settings.OverlayOpacity;
            OverlayHotkey = NormalizeOverlayHotkey(Settings.OverlayHotkey);
            if (Settings.OverlayHotkey != OverlayHotkey)
            {
                Settings.OverlayHotkey = OverlayHotkey;
                SaveSettings();
            }

            IsOverlayVisible = Settings.IsOverlayVisible;

            var modelIdx = Array.IndexOf(SupportedModels, SelectedModel);
            if (modelIdx < 0) modelIdx = 1;
            SelectedModel = SupportedModels[modelIdx];
            SelectedModelIndex = modelIdx;

            var langIdx = Array.IndexOf(SupportedLanguages, SelectedLanguage);
            if (langIdx < 0) langIdx = 0;
            SelectedLanguage = SupportedLanguages[langIdx];
            SelectedLanguageIndex = langIdx;

            _transcriptionHistoryService.SetMaxHistoryItems(HistoryLimit);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void InitializeAudioDevices()
    {
        _audioDevices.Clear();
        var devices = _audioCaptureService.GetAvailableAudioDevices();
        foreach (var device in devices)
            _audioDevices.Add(device);

        if (_audioDevices.Count == 0)
            _audioDevices.Add("Default Microphone");

        RestoreSelectedAudioDevice();
    }

    private void RestoreSelectedAudioDevice()
    {
        string? savedName = Settings.AudioDevice;
        int? savedIndex = Settings.AudioDeviceIndex;

        if (savedIndex is >= 0)
        {
            var nameByIndex = _audioCaptureService.GetDeviceNameByIndex(savedIndex.Value);
            if (!string.IsNullOrEmpty(nameByIndex) && _audioDevices.Contains(nameByIndex))
            {
                SelectedAudioDevice = nameByIndex;
                return;
            }
        }

        if (!string.IsNullOrEmpty(savedName))
        {
            var exact = _audioDevices.FirstOrDefault(d => d == savedName);
            if (exact != null)
            {
                SelectedAudioDevice = exact;
                return;
            }

            var partial = _audioDevices.FirstOrDefault(d =>
                d.Contains(savedName, StringComparison.OrdinalIgnoreCase) ||
                savedName.Contains(d, StringComparison.OrdinalIgnoreCase));
            if (partial != null)
            {
                SelectedAudioDevice = partial;
                return;
            }
        }

        SelectedAudioDevice = _audioDevices.FirstOrDefault();
    }

    private void InitializeHotkeyDisplay()
    {
        string? hotkey = Settings.Hotkey;
        if (string.IsNullOrEmpty(hotkey) || !IsValidHotkey(hotkey))
        {
            hotkey = "Ctrl+Alt+A";
            Settings.Hotkey = hotkey;
            SaveSettings();
        }
        HotkeyDisplay = hotkey;
    }

    private void LoadHistory()
    {
        _history.Clear();
        foreach (var entry in _transcriptionHistoryService.GetAllHistory())
            _history.Add(entry);
    }

    private void SaveSettings()
    {
        try
        {
            Settings.Model = SelectedModel;
            Settings.Language = SelectedLanguage;
            Settings.Hotkey = HotkeyDisplay;
            Settings.HotkeyMode = HotkeyModeIndex == 1 ? "hold" : "toggle";
            Settings.AudioDevice = SelectedAudioDevice;
            Settings.AudioDeviceIndex = _audioCaptureService.FindDeviceIndexByName(SelectedAudioDevice);
            Settings.AutoStart = AutoStart;
            Settings.MinimizedToTray = MinimizedToTray;
            Settings.MinimizeToTrayOnStartup = MinimizeToTrayOnStartup;
            Settings.UseGpu = UseGpu;
            Settings.HistoryLimit = HistoryLimit;
            Settings.OutputFormat = OutputFormat;
            Settings.OverlayOpacity = OverlayOpacity;
            Settings.OverlayHotkey = OverlayHotkey;
            Settings.IsOverlayVisible = IsOverlayVisible;

            _settingsService.Save(Settings);
            _transcriptionHistoryService.SetMaxHistoryItems(HistoryLimit);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearHistory()
    {
        _history.Clear();
        _transcriptionHistoryService.ClearHistory();
    }

    private void ExitApp()
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.PrepareExit();

        _hotkeyService.UnregisterHotkeys();
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "Speech to Text v1.0\n\nПриложение для распознавания речи.\nНажмите кнопку записи или горячие клавиши для начала.",
            "О приложении",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void OpenHistoryWindow()
    {
        var historyWindow = new HistoryWindow(History, _transcriptionHistoryService);
        historyWindow.Owner = System.Windows.Application.Current.MainWindow;
        historyWindow.ShowDialog();
        LoadHistory();
    }

    private void StartCaptureHotkey()
    {
        IsCapturingHotkey = true;
        HotkeyInputText = "Нажмите клавишу или комбинацию...";
    }

    private void CaptureHotkeyInput()
    {
        // This command is triggered by key events from the window
        // The actual key capture happens in MainWindow.KeyDown
    }

    public void StopCaptureHotkey()
    {
        IsCapturingHotkey = false;
        HotkeyInputText = "";
    }

    public void OnHotkeyKeyDown(Key key, ModifierKeys modifiers)
    {
        if (!IsCapturingHotkey)
            return;

        var newHotkey = HotkeyService.FormatHotkey(modifiers, key);
        if (string.IsNullOrEmpty(newHotkey))
        {
            HotkeyInputText = "Клавиша не поддерживается";
            return;
        }

        _hotkeyService.UnregisterHotkeys();
        HotkeyDisplay = newHotkey;
        HotkeyInputText = newHotkey;
        IsCapturingHotkey = false;

        if (!ApplyRecordingHotkeyMode())
        {
            HotkeyDisplay = Settings.Hotkey ?? "Ctrl+Alt+A";
            HotkeyInputText = "Комбинация занята, попробуйте другую";
            ApplyRecordingHotkeyMode();
            return;
        }

        Settings.Hotkey = HotkeyDisplay;
        SaveSettings();
    }

    private static bool IsValidHotkey(string hotkey)
    {
        return HotkeyService.TryParseHotkey(hotkey, out _, out _);
    }

    private static string NormalizeOverlayHotkey(string? hotkey)
    {
        if (string.Equals(hotkey, "Esc", StringComparison.OrdinalIgnoreCase))
            return "F1";

        if (!string.IsNullOrEmpty(hotkey) && IsValidHotkey(hotkey))
            return hotkey;

        return "F1";
    }
}
