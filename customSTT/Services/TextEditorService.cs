using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using customSTT.Models;

namespace customSTT.Services;

public class TextEditorService : IDisposable
{
    private readonly HttpClient _httpClient;

    public TextEditorService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public static bool IsConfigured(EditorConfig config) =>
        EditorProviderExtensions.IsConfigured(config.Provider, config.Model, config.ApiKey);

    public async Task<string> EditTextAsync(
        string text,
        EditorConfig config,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (!IsConfigured(config))
            return text;

        var prompt = string.IsNullOrWhiteSpace(config.Prompt)
            ? EditorDefaults.DefaultPrompt
            : config.Prompt;

        prompt = prompt.Replace("{text}", text, StringComparison.Ordinal);

        var url = BuildApiUrl(config);
        var request = new ChatCompletionRequest
        {
            Model = config.Model.Trim(),
            Messages =
            [
                new ChatMessage { Role = "user", Content = prompt }
            ],
            Stream = false
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        if (config.Provider.RequiresApiKey())
        {
            var apiKey = config.ApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Укажите API-ключ для облачного провайдера");

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ошибка API ({(int)response.StatusCode}): {TrimError(body)}");

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body);
        var result = completion?.Choices?[0]?.Message?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("Пустой ответ от нейросети");

        return result;
    }

    private static string BuildApiUrl(EditorConfig config)
    {
        if (config.Provider.IsLocal())
        {
            var port = Math.Clamp(config.Port, 1, 65535);
            return $"http://127.0.0.1:{port}/v1/chat/completions";
        }

        var baseUrl = config.Provider.RequiresBaseUrl()
            ? config.BaseUrl
            : config.Provider.GetDefaultBaseUrl();

        baseUrl = NormalizeBaseUrl(baseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Укажите базовый URL API");

        return $"{baseUrl}/chat/completions";
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";

        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^"/chat/completions".Length];

        return trimmed;
    }

    private static string TrimError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "нет описания";

        const int maxLength = 200;
        var trimmed = body.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public ChatMessage[] Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public ChatChoice[]? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }
}
