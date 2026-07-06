using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using customSTT.Models;

namespace customSTT.Services;

/// <summary>
/// Сервис для управления историей транскрипций
/// </summary>
public class TranscriptionHistoryService : IDisposable
{
    private readonly string _historyFilePath;
    private readonly object _lock = new();
    private int _maxHistoryItems = 20;

    /// <summary>
    /// Конструктор
    /// </summary>
    public TranscriptionHistoryService()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(appDir, "Data");

        _historyFilePath = Path.Combine(dataDir, "transcription_history.json");
    }

    /// <summary>
    /// Добавление записи в историю
    /// </summary>
    public void AddToHistory(TranscriptionEntry entry)
    {
        lock (_lock)
        {
            var history = LoadHistory();
            
            // Добавляем новую запись в начало списка
            history.Insert(0, entry);

            // Ограничиваем размер истории
            if (history.Count > _maxHistoryItems)
            {
                history.RemoveRange(_maxHistoryItems, history.Count - _maxHistoryItems);
            }

            SaveHistory(history);
        }
    }

    /// <summary>
    /// Загрузка истории из файла
    /// </summary>
    private List<TranscriptionEntry> LoadHistory()
    {
        if (!File.Exists(_historyFilePath))
            return new List<TranscriptionEntry>();

        try
        {
            var json = File.ReadAllText(_historyFilePath);
            return JsonSerializer.Deserialize<List<TranscriptionEntry>>(json) ?? new List<TranscriptionEntry>();
        }
        catch
        {
            return new List<TranscriptionEntry>();
        }
    }

    /// <summary>
    /// Сохранение истории в файл
    /// </summary>
    private void SaveHistory(List<TranscriptionEntry> history)
    {
        try
        {
            var dir = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения истории: {ex.Message}");
        }
    }

    /// <summary>
    /// Получение всех записей из истории
    /// </summary>
    public List<TranscriptionEntry> GetAllHistory()
    {
        lock (_lock)
        {
            return LoadHistory();
        }
    }

    /// <summary>
    /// Удаление записи из истории по ID
    /// </summary>
    public void RemoveFromHistory(string id)
    {
        lock (_lock)
        {
            var history = LoadHistory();
            var entry = history.FirstOrDefault(h => h.Id == id);
            if (entry != null)
                history.Remove(entry);
            SaveHistory(history);
        }
    }

    /// <summary>
    /// Очистка всей истории
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            File.Delete(_historyFilePath);
        }
    }

    /// <summary>
    /// Получение количества записей в истории
    /// </summary>
    public int GetHistoryCount()
    {
        lock (_lock)
        {
            return LoadHistory().Count;
        }
    }

    /// <summary>
    /// Установить лимит истории
    /// </summary>
    public void SetMaxHistoryItems(int count)
    {
        _maxHistoryItems = Math.Max(0, count);
    }

    /// <summary>
    /// Освобождение ресурсов
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
