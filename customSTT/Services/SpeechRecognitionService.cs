using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace customSTT.Services;

public class SpeechRecognitionService : IDisposable
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private string? _loadedModel;
    private string? _loadedLanguage;
    private bool _loadedUseGpu = true;
    private string? _modelPath;

    public readonly record struct WhisperModelOption(string Id, string DisplayName);

    public static readonly WhisperModelOption[] ModelOptions =
    {
        new("tiny", "tiny (низкое)"),
        new("base", "base (базовое)"),
        new("small", "small (хорошее)"),
        new("medium", "medium (очень хорошее)"),
        new("large-v3-turbo", "large-v3-turbo (высокое)"),
        new("large-v3", "large-v3 (максимальное)")
    };

    public static readonly string[] SupportedModels =
        Array.ConvertAll(ModelOptions, static o => o.Id);

    private static readonly Dictionary<string, long> MinModelSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tiny"] = 70_000_000,
        ["base"] = 130_000_000,
        ["small"] = 400_000_000,
        ["medium"] = 1_000_000_000,
        ["large-v3"] = 2_900_000_000,
        ["large-v3-turbo"] = 1_400_000_000
    };

    private static readonly Dictionary<string, long> ExpectedModelSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tiny"] = 77_700_000,
        ["base"] = 148_000_000,
        ["small"] = 488_000_000,
        ["medium"] = 1_530_000_000,
        ["large-v3"] = 3_100_000_000,
        ["large-v3-turbo"] = 1_620_000_000
    };

    private const int AudioSampleRate = 16000;

    public static string GetModelSizeText(string model)
    {
        if (!ExpectedModelSizes.TryGetValue(model, out var size))
            return "неизв.";

        var mb = size / 1_000_000.0;
        return mb >= 1000 ? $"{mb / 1000.0:F1} ГБ" : $"{mb:F0} МБ";
    }

    public static string GetModelPath(string model)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var modelDir = Path.Combine(appDir, "whisper-models");
        return Path.Combine(modelDir, $"ggml-{model}.bin");
    }

    public bool IsModelAvailable(string model) => IsModelFileValid(model, GetModelPath(model));

    /// <summary>
    /// Скачивает модель, если её ещё нет. Прогресс сообщается в процентах (0–100).
    /// </summary>
    public async Task<bool> EnsureModelDownloadedAsync(string model, IProgress<double>? progress = null)
    {
        var path = GetModelPath(model);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (IsModelFileValid(model, path))
        {
            progress?.Report(100);
            return true;
        }

        if (File.Exists(path))
            File.Delete(path);

        var ok = await DownloadModelToFileAsync(model, path, progress);
        return ok && IsModelFileValid(model, path);
    }

    public async Task<bool> LoadModelAsync(
        string model = "base",
        string language = "auto",
        bool useGpu = true,
        IProgress<double>? downloadProgress = null)
    {
        return await EnsureProcessorAsync(model, language, useGpu, downloadProgress);
    }

    private async Task<bool> EnsureProcessorAsync(
        string model,
        string language,
        bool useGpu,
        IProgress<double>? downloadProgress = null)
    {
        var normalizedLanguage = NormalizeLanguage(language);

        if (_processor != null
            && _loadedModel == model
            && _loadedLanguage == normalizedLanguage
            && _loadedUseGpu == useGpu)
            return true;

        try
        {
            _processor?.Dispose();
            _processor = null;
            _factory?.Dispose();
            _factory = null;
            _loadedModel = null;
            _loadedLanguage = null;

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var modelDir = Path.Combine(appDir, "whisper-models");
            Directory.CreateDirectory(modelDir);

            _modelPath = Path.Combine(modelDir, $"ggml-{model}.bin");

            if (!IsModelFileValid(model, _modelPath))
            {
                if (File.Exists(_modelPath))
                    File.Delete(_modelPath);

                await DownloadModelToFileAsync(model, _modelPath, downloadProgress);
            }

            if (!IsModelFileValid(model, _modelPath))
            {
                Console.WriteLine($"Модель '{model}' не найдена или повреждена после загрузки.");
                return false;
            }

            RuntimeOptions.Instance.SetUseGpu(useGpu);
            _factory = WhisperFactory.FromPath(_modelPath);

            var builder = _factory
                .CreateBuilder()
                .WithThreads(Math.Max(1, Environment.ProcessorCount));

            if (normalizedLanguage == "auto")
                builder = builder.WithLanguage("auto");
            else
                builder = builder.WithLanguage(normalizedLanguage);

            _processor = builder.Build();
            _loadedModel = model;
            _loadedLanguage = normalizedLanguage;
            _loadedUseGpu = useGpu;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки модели: {ex.Message}");
            return false;
        }
    }

    private static bool IsModelFileValid(string model, string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        if (!MinModelSizes.TryGetValue(model, out var minSize))
            minSize = 50_000_000;

        var info = new FileInfo(path);
        return info.Length >= minSize;
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "auto";

        return language.Trim().ToLowerInvariant();
    }

    private static GgmlType MapModelToGgmlType(string model)
    {
        return model switch
        {
            "tiny" => GgmlType.Tiny,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            "large-v3" => GgmlType.LargeV3,
            "large-v3-turbo" => GgmlType.LargeV3Turbo,
            _ => GgmlType.Base
        };
    }

    private static async Task<bool> DownloadModelToFileAsync(string model, string path, IProgress<double>? progress)
    {
        try
        {
            Console.WriteLine($"Скачивание модели '{model}'...");
            var ggmlType = MapModelToGgmlType(model);
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);

            var expectedSize = ExpectedModelSizes.TryGetValue(model, out var size) ? size : 0;
            long totalRead = 0;
            var buffer = new byte[81920];

            await using (var fileWriter = File.Create(path))
            {
                int read;
                while ((read = await modelStream.ReadAsync(buffer)) > 0)
                {
                    await fileWriter.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (expectedSize > 0)
                        progress?.Report(Math.Min(99.0, totalRead * 100.0 / expectedSize));
                }
            }

            progress?.Report(100);
            Console.WriteLine($"Модель '{model}' сохранена в {path}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось скачать модель '{model}': {ex.Message}");
            return false;
        }
    }

    public async Task<RecognitionResult> RecognizeAsync(
        byte[] pcmData,
        string model = "base",
        string language = "auto",
        IProgress<double>? progress = null,
        bool useGpu = true)
    {
        progress?.Report(5);

        var loaded = await EnsureProcessorAsync(model, language, useGpu);
        if (!loaded || _processor == null)
            throw new InvalidOperationException("Модель Whisper не загружена");

        progress?.Report(25);

        try
        {
            var audioData = ConvertPcmToAudioData(pcmData);
            progress?.Report(35);

            var totalDurationSeconds = audioData.Length / (double)AudioSampleRate;

            var textBuilder = new StringBuilder();
            SegmentData? firstSegment = null;
            SegmentData? lastSegment = null;
            var lastReportedProgress = 35.0;

            await foreach (var segment in _processor.ProcessAsync(audioData))
            {
                firstSegment ??= segment;
                lastSegment = segment;

                var piece = segment.Text?.Trim();
                if (!string.IsNullOrEmpty(piece))
                {
                    if (textBuilder.Length > 0)
                        textBuilder.Append(' ');
                    textBuilder.Append(piece);
                }

                if (totalDurationSeconds > 0)
                {
                    var fraction = Math.Clamp(segment.End.TotalSeconds / totalDurationSeconds, 0, 1);
                    var nextProgress = 35 + fraction * 60;
                    if (nextProgress - lastReportedProgress >= 1)
                    {
                        progress?.Report(nextProgress);
                        lastReportedProgress = nextProgress;
                    }
                }
            }

            progress?.Report(95);

            if (lastSegment == null)
                return new RecognitionResult { Text = string.Empty, Language = string.Empty, Duration = 0 };

            return new RecognitionResult
            {
                Text = textBuilder.ToString(),
                Language = lastSegment.Language ?? firstSegment?.Language ?? language,
                Duration = lastSegment.End.TotalSeconds - (firstSegment?.Start.TotalSeconds ?? 0)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка распознавания: {ex.Message}");
            throw;
        }
        finally
        {
            progress?.Report(100);
        }
    }

    private static float[] ConvertPcmToAudioData(byte[] pcmData)
    {
        var sampleCount = pcmData.Length / 2;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(pcmData, i * 2);
            samples[i] = sample / 32768.0f;
        }

        return samples;
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class RecognitionResult
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double Duration { get; set; }
}
