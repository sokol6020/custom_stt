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

    public static readonly string[] SupportedModels = { "tiny", "base", "small", "medium" };

    private static readonly Dictionary<string, long> MinModelSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tiny"] = 70_000_000,
        ["base"] = 130_000_000,
        ["small"] = 400_000_000,
        ["medium"] = 1_000_000_000
    };

    public async Task<bool> LoadModelAsync(string model = "base", string language = "auto", bool useGpu = true)
    {
        return await EnsureProcessorAsync(model, language, useGpu);
    }

    private async Task<bool> EnsureProcessorAsync(string model, string language, bool useGpu)
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

            if (!IsModelFileValid(model))
            {
                if (File.Exists(_modelPath))
                    File.Delete(_modelPath);

                await DownloadModelAsync(model);
            }

            if (!IsModelFileValid(model))
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

    private bool IsModelFileValid(string model)
    {
        if (_modelPath == null || !File.Exists(_modelPath))
            return false;

        if (!MinModelSizes.TryGetValue(model, out var minSize))
            minSize = 50_000_000;

        var info = new FileInfo(_modelPath);
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
            _ => GgmlType.Base
        };
    }

    private async Task DownloadModelAsync(string model)
    {
        try
        {
            Console.WriteLine($"Скачивание модели '{model}'...");
            var ggmlType = MapModelToGgmlType(model);
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
            await using var fileWriter = File.Create(_modelPath!);
            await modelStream.CopyToAsync(fileWriter);
            Console.WriteLine($"Модель '{model}' сохранена в {_modelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось скачать модель '{model}': {ex.Message}");
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

            var textBuilder = new StringBuilder();
            SegmentData? firstSegment = null;
            SegmentData? lastSegment = null;
            var segmentCount = 0;
            var lastReportedProgress = 35.0;

            await foreach (var segment in _processor.ProcessAsync(audioData))
            {
                firstSegment ??= segment;
                lastSegment = segment;
                segmentCount++;

                var piece = segment.Text?.Trim();
                if (!string.IsNullOrEmpty(piece))
                {
                    if (textBuilder.Length > 0)
                        textBuilder.Append(' ');
                    textBuilder.Append(piece);
                }

                var nextProgress = 35 + Math.Min(55, segmentCount * 12);
                if (nextProgress - lastReportedProgress >= 3)
                {
                    progress?.Report(nextProgress);
                    lastReportedProgress = nextProgress;
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
