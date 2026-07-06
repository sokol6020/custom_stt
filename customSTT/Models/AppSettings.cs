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
    /// Прозрачность оверлея (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("overlayOpacity")]
    public double OverlayOpacity { get; set; } = 0.3;

    /// <summary>
    /// Горячие клавиши для скрытия/показа оверлея
    /// </summary>
    [JsonPropertyName("overlayHotkey")]
    public string OverlayHotkey { get; set; } = "F1";

    [JsonPropertyName("isOverlayVisible")]
    public bool IsOverlayVisible { get; set; }
}
