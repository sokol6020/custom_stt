using System.Text.Json.Serialization;

namespace customSTT.Models;

/// <summary>
/// Настройки приложения
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Имя устройства захвата аудио
    /// </summary>
    [JsonPropertyName("audioDevice")]
    public string? AudioDevice { get; set; }

    /// <summary>
    /// Индекс устройства захвата аудио
    /// </summary>
    [JsonPropertyName("audioDeviceIndex")]
    public int? AudioDeviceIndex { get; set; }

    /// <summary>
    /// Язык распознавания речи
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Модель Whisper для использования
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "base";

    /// <summary>
    /// Использовать GPU для ускорения распознавания (Whisper.NET)
    /// </summary>
    [JsonPropertyName("useGpu")]
    public bool UseGpu { get; set; } = true;

    /// <summary>
    /// Горячие клавиши для запуска/остановки записи
    /// </summary>
    [JsonPropertyName("hotkey")]
    public string Hotkey { get; set; } = "Ctrl+Alt+A";

    /// <summary>
    /// Режим горячей клавиши записи: toggle (нажатие) или hold (удержание)
    /// </summary>
    [JsonPropertyName("hotkeyMode")]
    public string HotkeyMode { get; set; } = "toggle";

    /// <summary>
    /// Распознавание и вставка текста по паузам во время записи
    /// </summary>
    [JsonPropertyName("dictationOnPause")]
    public bool DictationOnPause { get; set; }

    /// <summary>
    /// Автозапуск при старте Windows
    /// </summary>
    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    /// <summary>
    /// Минимизация в трей вместо закрытия
    /// </summary>
    [JsonPropertyName("minimizedToTray")]
    public bool MinimizedToTray { get; set; } = true;

    /// <summary>
    /// Скрывать окно в трей при запуске приложения
    /// </summary>
    [JsonPropertyName("minimizeToTrayOnStartup")]
    public bool MinimizeToTrayOnStartup { get; set; }

    /// <summary>
    /// История транскрипций (последние N записей)
    /// </summary>
    [JsonPropertyName("historyLimit")]
    public int HistoryLimit { get; set; } = 20;

    /// <summary>
    /// Формат вывода текста
    /// </summary>
    [JsonPropertyName("outputFormat")]
    public string OutputFormat { get; set; } = "plainText";

    /// <summary>
    /// Прозрачность оверлея (0–100%)
    /// </summary>
    [JsonPropertyName("overlayOpacity")]
    public double OverlayOpacity { get; set; } = 30;

    /// <summary>
    /// Угол экрана для оверлея: topLeft, topRight, bottomLeft, bottomRight
    /// </summary>
    [JsonPropertyName("overlayCorner")]
    public string OverlayCorner { get; set; } = "topRight";

    /// <summary>
    /// Индекс экрана (0 — первый в списке Windows)
    /// </summary>
    [JsonPropertyName("overlayScreenIndex")]
    public int OverlayScreenIndex { get; set; }

    /// <summary>
    /// Горячие клавиши для скрытия/показа оверлея
    /// </summary>
    [JsonPropertyName("overlayHotkey")]
    public string OverlayHotkey { get; set; } = "F1";

    [JsonPropertyName("isOverlayVisible")]
    public bool IsOverlayVisible { get; set; }

    /// <summary>
    /// Проверять обновления при запуске приложения
    /// </summary>
    [JsonPropertyName("checkUpdatesOnStartup")]
    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>
    /// Провайдер редактора текста
    /// </summary>
    [JsonPropertyName("editorProvider")]
    public string EditorProvider { get; set; } = "ollama";

    /// <summary>
    /// Порт локального API редактора
    /// </summary>
    [JsonPropertyName("editorPort")]
    public int EditorPort { get; set; } = 11434;

    /// <summary>
    /// API-ключ облачного провайдера
    /// </summary>
    [JsonPropertyName("editorApiKey")]
    public string EditorApiKey { get; set; } = "";

    /// <summary>
    /// Базовый URL для пользовательского OpenAI API
    /// </summary>
    [JsonPropertyName("editorBaseUrl")]
    public string EditorBaseUrl { get; set; } = EditorDefaults.DefaultCustomBaseUrl;

    /// <summary>
    /// Имя модели для редактирования текста
    /// </summary>
    [JsonPropertyName("editorModel")]
    public string EditorModel { get; set; } = "";

    /// <summary>
    /// Промпт для редактирования. {text} — распознанный текст
    /// </summary>
    [JsonPropertyName("editorPrompt")]
    public string EditorPrompt { get; set; } = EditorDefaults.DefaultPrompt;
}
