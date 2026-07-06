using System.Text.Json.Serialization;

namespace customSTT.Models;

/// <summary>
/// Запись транскрипции в истории
/// </summary>
public class TranscriptionEntry
{
    /// <summary>
    /// Уникальный идентификатор записи
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Время создания (ISO 8601)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Распознанный текст
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Использованная модель Whisper
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Язык распознавания
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Длительность аудио в секундах
    /// </summary>
    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Статус записи (завершена, ошибка и т.д.)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";

    /// <summary>
    /// Сообщение об ошибке (если статус != completed)
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
