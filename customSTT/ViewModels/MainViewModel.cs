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
    private readonly TextEditorService _textEditorService;
    private readonly UpdateService _updateService;
    private readonly SettingsService _settingsService;

    private bool _isUpdateInProgress;

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

    public static readonly string[] SupportedModels = SpeechRecognitionService.SupportedModels;
    public static readonly string[] SupportedLanguages = { "auto", "ru", "en", "de", "fr", "es", "it", "ja", "zh" };

    public string[] AvailableModels { get; } =
        Array.ConvertAll(SpeechRecognitionService.ModelOptions, static o => o.DisplayName);
    public string[] AvailableLanguages => SupportedLanguages;

    [ObservableProperty]
    private string _hotkeyDisplay = "Ctrl+Alt+A";

    private bool _isLoadingSettings;
    private bool _hotkeysReady;
    private bool _suppressModelChange;
    private int _lastConfirmedModelIndex;

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
    private bool _checkUpdatesOnStartup = true;

    [ObservableProperty]
    private string _updateStatusText = "";

    [ObservableProperty]
    private bool _useGpu = true;

    [ObservableProperty]
    private int _historyLimit = 20;

    [ObservableProperty]
    private string _outputFormat = "plainText";

    [ObservableProperty]
    private double _overlayOpacity = 30;

    [ObservableProperty]
    private int _overlayCornerIndex = (int)OverlayCorner.TopRight;

    [ObservableProperty]
    private int _overlayScreenIndex;

    public string[] AvailableOverlayCorners => OverlayCornerExtensions.DisplayNames;

    private readonly ObservableCollection<string> _availableScreens = new();
    public ObservableCollection<string> AvailableScreens => _availableScreens;

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
    private bool _isDownloading;

    [ObservableProperty]
    private double _processingProgress;

    [ObservableProperty]
    private string _processingStageText = "Готов к работе";

    [ObservableProperty]
    private string _progressCaptionText = "Готов к работе";

    [ObservableProperty]
    private int _editorProviderIndex;

    [ObservableProperty]
    private int _editorPort = 11434;

    [ObservableProperty]
    private string _editorApiKey = "";

    [ObservableProperty]
    private string _editorBaseUrl = EditorDefaults.DefaultCustomBaseUrl;

    [ObservableProperty]
    private string _editorModel = "";

    [ObservableProperty]
    private string _editorPrompt = EditorDefaults.DefaultPrompt;

    public string[] AvailableEditorProviders => EditorProviderExtensions.DisplayNames;

    public bool IsLocalEditorProvider => CurrentEditorProvider.IsLocal();

    public bool IsCloudEditorProvider => CurrentEditorProvider.RequiresApiKey();

    public bool IsEditorBaseUrlVisible => CurrentEditorProvider.RequiresBaseUrl();

    private EditorProviderKind CurrentEditorProvider =>
        EditorProviderExtensions.FromIndex(EditorProviderIndex);

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
        if (value < 0 || value >= SupportedModels.Length)
            return;

        SelectedModel = SupportedModels[value];

        if (_isLoadingSettings || _suppressModelChange)
        {
            _lastConfirmedModelIndex = value;
            return;
        }

        _ = HandleModelChangeAsync(value);
    }

    private async Task HandleModelChangeAsync(int newIndex)
    {
        var model = SupportedModels[newIndex];

        if (_speechRecognitionService.IsModelAvailable(model))
        {
            _lastConfirmedModelIndex = newIndex;
            SaveSettings();
            _ = _speechRecognitionService.LoadModelAsync(model, SelectedLanguage, UseGpu);
            return;
        }

        var displayName = newIndex < AvailableModels.Length ? AvailableModels[newIndex] : model;
        var sizeText = SpeechRecognitionService.GetModelSizeText(model);
        var answer = MessageBox.Show(
            $"Модель «{displayName}» ещё не скачана (~{sizeText}). Скачать сейчас?",
            "Скачивание модели",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            RevertModelSelection();
            return;
        }

        await DownloadAndLoadModelAsync(model, newIndex);
    }

    private void RevertModelSelection()
    {
        _suppressModelChange = true;
        SelectedModelIndex = _lastConfirmedModelIndex;
        SelectedModel = SupportedModels[_lastConfirmedModelIndex];
        _suppressModelChange = false;
    }

    private async Task DownloadAndLoadModelAsync(string model, int index)
    {
        var displayName = index < AvailableModels.Length ? AvailableModels[index] : model;

        try
        {
            IsDownloading = true;
            IsProcessing = true;
            ProcessingProgress = 0;
            ProcessingStageText = $"Скачивание модели {model}...";
            ProgressCaptionText = ProcessingStageText;
            RecordingStatus = ProcessingStageText;

            var progress = new Progress<double>(p =>
            {
                if (Math.Abs(ProcessingProgress - p) >= 1 || p >= 100)
                    ProcessingProgress = p;
            });

            var downloaded = await _speechRecognitionService.EnsureModelDownloadedAsync(model, progress);
            if (!downloaded)
            {
                RecordingStatus = $"Не удалось скачать модель «{displayName}»";
                RevertModelSelection();
                return;
            }

            _lastConfirmedModelIndex = index;
            SaveSettings();

            IsDownloading = false;
            ProcessingStageText = $"Загрузка модели {model}...";
            ProgressCaptionText = ProcessingStageText;
            RecordingStatus = ProcessingStageText;
            await _speechRecognitionService.LoadModelAsync(model, SelectedLanguage, UseGpu);

            RecordingStatus = "Готов";
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Ошибка: {ex.Message}";
            RevertModelSelection();
        }
        finally
        {
            IsDownloading = false;
            IsProcessing = false;
        }
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

    partial void OnCheckUpdatesOnStartupChanged(bool value)
    {
        if (!_isLoadingSettings)
            SaveSettings();
    }

    public string AppVersionText => $"Версия {AppVersion.Current}";

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
        if (!_isLoadingSettings)
            ApplyOverlayLayout();
        SaveSettings();
    }

    partial void OnOverlayCornerIndexChanged(int value)
    {
        if (!_isLoadingSettings)
            ApplyOverlayLayout();
        SaveSettings();
    }

    partial void OnOverlayScreenIndexChanged(int value)
    {
        if (!_isLoadingSettings)
            ApplyOverlayLayout();
        SaveSettings();
    }

    partial void OnOverlayHotkeyChanged(string value)
    {
        SaveSettings();
        if (!string.IsNullOrEmpty(value) && IsValidHotkey(value))
            ApplyRecordingHotkeyMode();
    }

    partial void OnEditorProviderIndexChanged(int value)
    {
        NotifyEditorProviderUi();

        if (!_isLoadingSettings)
        {
            var provider = EditorProviderExtensions.FromIndex(value);
            if (provider.IsLocal())
                EditorPort = provider.GetDefaultPort();
            else if (provider.RequiresBaseUrl())
                EditorBaseUrl = provider.GetDefaultBaseUrl();

            SaveSettings();
        }
    }

    partial void OnEditorPortChanged(int value)
    {
        if (!_isLoadingSettings)
            SaveSettings();
    }

    partial void OnEditorApiKeyChanged(string value)
    {
        if (!_isLoadingSettings)
            SaveSettings();
    }

    partial void OnEditorBaseUrlChanged(string value)
    {
        if (!_isLoadingSettings)
            SaveSettings();
    }

    partial void OnEditorModelChanged(string value)
    {
        if (!_isLoadingSettings)
            SaveSettings();
    }

    partial void OnEditorPromptChanged(string value)
    {
        if (!_isLoadingSettings)
            SaveSettings();
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
        if (IsDownloading)
        {
            status = OverlayStatus.Downloading;
            OverlayRecordingIndicator = "● Скачивание";
        }
        else if (IsRecording)
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
        if (IsDownloading || IsProcessing)
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

    partial void OnIsDownloadingChanged(bool value)
    {
        UpdateOverlayState();
        SyncTrayStatus();
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
        var trayStatus = IsDownloading
            ? TrayIconStatus.Downloading
            : IsRecording
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
    public ICommand ResetEditorPromptCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    public MainViewModel(
        AudioCaptureService audioCaptureService,
        SpeechRecognitionService speechRecognitionService,
        TextInjectionService textInjectionService,
        HotkeyService hotkeyService,
        TrayIconService trayIconService,
        TranscriptionHistoryService transcriptionHistoryService,
        OverlayService overlayService,
        TextEditorService textEditorService,
        UpdateService updateService,
        SettingsService settingsService)
    {
        _audioCaptureService = audioCaptureService;
        _speechRecognitionService = speechRecognitionService;
        _textInjectionService = textInjectionService;
        _hotkeyService = hotkeyService;
        _trayIconService = trayIconService;
        _transcriptionHistoryService = transcriptionHistoryService;
        _overlayService = overlayService;
        _textEditorService = textEditorService;
        _updateService = updateService;
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
        ResetEditorPromptCommand = new RelayCommand(ResetEditorPrompt);
        CheckForUpdatesCommand = new RelayCommand(() => _ = CheckForUpdatesAsync(true));

        LoadSettings();
        InitializeAudioDevices();
        InitializeOverlayScreens();
        InitializeHotkeyDisplay();
        LoadHistory();
        _history.CollectionChanged += OnHistoryCollectionChanged;
        UpdateHistoryCountText();

        if (IsOverlayVisible)
            _overlayService.Show();

        UpdateOverlayPanelStatus();
        UpdateOverlayState();

        _ = _speechRecognitionService.LoadModelAsync(SelectedModel, SelectedLanguage, UseGpu);

        if (CheckUpdatesOnStartup)
            _ = CheckForUpdatesAsync(false);
    }

    private async Task CheckForUpdatesAsync(bool interactive)
    {
        if (_isUpdateInProgress)
            return;

        _isUpdateInProgress = true;
        try
        {
            if (interactive)
                UpdateStatusText = "Проверка обновлений...";

            var update = await _updateService.CheckForUpdateAsync();

            if (update == null)
            {
                UpdateStatusText = interactive
                    ? $"Установлена последняя версия ({AppVersion.Current})"
                    : "";
                return;
            }

            UpdateStatusText = $"Доступна версия {update.Version}";

            var notes = string.IsNullOrWhiteSpace(update.Notes)
                ? ""
                : $"\n\nЧто нового:\n{Truncate(update.Notes, 600)}";

            var answer = MessageBox.Show(
                $"Доступна новая версия {update.Version} (текущая {AppVersion.Current}).{notes}\n\nСкачать и установить сейчас? Приложение перезапустится.",
                "Обновление",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer != MessageBoxResult.Yes)
                return;

            await DownloadAndInstallUpdateAsync(update);
        }
        catch (Exception ex)
        {
            UpdateStatusText = "Не удалось проверить обновления";
            if (interactive)
                MessageBox.Show($"Ошибка проверки обновлений: {ex.Message}", "Обновление",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isUpdateInProgress = false;
        }
    }

    private async Task DownloadAndInstallUpdateAsync(UpdateInfo update)
    {
        try
        {
            IsDownloading = true;
            IsProcessing = true;
            ProcessingProgress = 0;
            ProcessingStageText = $"Скачивание обновления {update.Version}...";
            ProgressCaptionText = ProcessingStageText;
            UpdateStatusText = ProcessingStageText;

            var progress = new Progress<double>(p =>
            {
                if (Math.Abs(ProcessingProgress - p) >= 1 || p >= 100)
                    ProcessingProgress = p;
            });

            var zipPath = await _updateService.DownloadUpdateAsync(update, progress);

            IsDownloading = false;
            ProcessingStageText = "Установка обновления...";
            ProgressCaptionText = ProcessingStageText;
            UpdateStatusText = ProcessingStageText;

            _updateService.ApplyUpdateAndRestart(zipPath);

            if (Application.Current.MainWindow is MainWindow mainWindow)
                mainWindow.PrepareExit();

            _hotkeyService.UnregisterHotkeys();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatusText = "Не удалось установить обновление";
            MessageBox.Show($"Ошибка установки обновления: {ex.Message}", "Обновление",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsDownloading = false;
            IsProcessing = false;
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
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

            var downloadProgress = new Progress<double>(p =>
            {
                if (!IsDownloading)
                    IsDownloading = true;
                ProcessingStageText = $"Скачивание модели {SelectedModel}...";
                ProgressCaptionText = ProcessingStageText;
                var mapped = 15 + p * 0.15;
                if (Math.Abs(ProcessingProgress - mapped) >= 1)
                    ProcessingProgress = mapped;
            });

            var modelLoaded = await _speechRecognitionService.LoadModelAsync(
                SelectedModel, SelectedLanguage, UseGpu, downloadProgress);
            IsDownloading = false;
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

            var finalText = result.Text;
            var editorConfig = BuildEditorConfig();
            if (!string.IsNullOrEmpty(result.Text) && TextEditorService.IsConfigured(editorConfig))
            {
                ProcessingStageText = "Редактирование текста...";
                RecordingStatus = ProcessingStageText;
                ProcessingProgress = 88;

                try
                {
                    finalText = await _textEditorService.EditTextAsync(result.Text, editorConfig);
                }
                catch (Exception editEx)
                {
                    RecordingStatus = $"Редактор: {editEx.Message}";
                }
            }

            if (!string.IsNullOrEmpty(finalText))
            {
                await _textInjectionService.InjectTextAsync(finalText);

                var entry = new TranscriptionEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Text = finalText,
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
            IsDownloading = false;
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
            CheckUpdatesOnStartup = Settings.CheckUpdatesOnStartup;
            UseGpu = Settings.UseGpu;
            HistoryLimit = Settings.HistoryLimit;
            OutputFormat = Settings.OutputFormat ?? "plainText";
            OverlayOpacity = NormalizeOverlayOpacity(Settings.OverlayOpacity);
            OverlayCornerIndex = OverlayCornerExtensions.FromStorageId(Settings.OverlayCorner).ToIndex();
            OverlayScreenIndex = Settings.OverlayScreenIndex;
            OverlayHotkey = NormalizeOverlayHotkey(Settings.OverlayHotkey);
            if (Settings.OverlayHotkey != OverlayHotkey)
            {
                Settings.OverlayHotkey = OverlayHotkey;
                SaveSettings();
            }

            IsOverlayVisible = Settings.IsOverlayVisible;

            EditorProviderIndex = EditorProviderExtensions.FromStorageId(Settings.EditorProvider).ToIndex();
            EditorPort = NormalizeEditorPort(Settings.EditorPort);
            EditorApiKey = Settings.EditorApiKey ?? "";
            EditorBaseUrl = string.IsNullOrWhiteSpace(Settings.EditorBaseUrl)
                ? EditorDefaults.DefaultCustomBaseUrl
                : Settings.EditorBaseUrl;
            EditorModel = Settings.EditorModel ?? "";
            EditorPrompt = string.IsNullOrWhiteSpace(Settings.EditorPrompt)
                ? EditorDefaults.DefaultPrompt
                : Settings.EditorPrompt;
            NotifyEditorProviderUi();

            var modelIdx = Array.IndexOf(SupportedModels, SelectedModel);
            if (modelIdx < 0) modelIdx = 1;
            SelectedModel = SupportedModels[modelIdx];
            SelectedModelIndex = modelIdx;

            var langIdx = Array.IndexOf(SupportedLanguages, SelectedLanguage);
            if (langIdx < 0) langIdx = 0;
            SelectedLanguage = SupportedLanguages[langIdx];
            SelectedLanguageIndex = langIdx;

            _transcriptionHistoryService.SetMaxHistoryItems(HistoryLimit);
            RefreshOverlayScreens();
            ApplyOverlayLayout();
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

    private void InitializeOverlayScreens()
    {
        RefreshOverlayScreens();
    }

    private void RefreshOverlayScreens()
    {
        var screens = OverlayLayoutHelper.GetScreens();
        _availableScreens.Clear();
        foreach (var screen in screens)
            _availableScreens.Add(screen.DisplayName);

        if (_availableScreens.Count == 0)
            _availableScreens.Add("Дисплей 1");

        if (OverlayScreenIndex >= _availableScreens.Count)
            OverlayScreenIndex = Math.Max(0, _availableScreens.Count - 1);
    }

    private void ApplyOverlayLayout()
    {
        var corner = OverlayCornerExtensions.FromIndex(OverlayCornerIndex);
        _overlayService.SetLayout(OverlayOpacity / 100.0, corner, OverlayScreenIndex);
    }

    private static double NormalizeOverlayOpacity(double saved)
    {
        if (saved > 0 && saved <= 1.0)
            return Math.Clamp(saved * 100, 0, 100);

        return Math.Clamp(saved, 0, 100);
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
            Settings.CheckUpdatesOnStartup = CheckUpdatesOnStartup;
            Settings.UseGpu = UseGpu;
            Settings.HistoryLimit = HistoryLimit;
            Settings.OutputFormat = OutputFormat;
            Settings.OverlayOpacity = OverlayOpacity;
            Settings.OverlayCorner = OverlayCornerExtensions.FromIndex(OverlayCornerIndex).ToStorageId();
            Settings.OverlayScreenIndex = OverlayScreenIndex;
            Settings.OverlayHotkey = OverlayHotkey;
            Settings.IsOverlayVisible = IsOverlayVisible;
            Settings.EditorProvider = EditorProviderExtensions.FromIndex(EditorProviderIndex).ToStorageId();
            Settings.EditorPort = NormalizeEditorPort(EditorPort);
            Settings.EditorApiKey = EditorApiKey.Trim();
            Settings.EditorBaseUrl = EditorBaseUrl.Trim();
            Settings.EditorModel = EditorModel.Trim();
            Settings.EditorPrompt = EditorPrompt;

            _settingsService.Save(Settings);
            _transcriptionHistoryService.SetMaxHistoryItems(HistoryLimit);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetEditorPrompt()
    {
        EditorPrompt = EditorDefaults.DefaultPrompt;
    }

    private void NotifyEditorProviderUi()
    {
        OnPropertyChanged(nameof(IsLocalEditorProvider));
        OnPropertyChanged(nameof(IsCloudEditorProvider));
        OnPropertyChanged(nameof(IsEditorBaseUrlVisible));
    }

    private EditorConfig BuildEditorConfig() => new()
    {
        Provider = CurrentEditorProvider,
        Port = NormalizeEditorPort(EditorPort),
        Model = EditorModel,
        Prompt = EditorPrompt,
        ApiKey = EditorApiKey,
        BaseUrl = EditorBaseUrl
    };

    private static int NormalizeEditorPort(int port) => Math.Clamp(port, 1, 65535);

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
