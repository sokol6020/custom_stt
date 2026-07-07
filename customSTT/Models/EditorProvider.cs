namespace customSTT.Models;

public enum EditorProviderKind
{
    Ollama,
    LmStudio,
    OpenAi,
    OpenRouter,
    Groq,
    DeepSeek,
    CustomOpenAi
}

public static class EditorDefaults
{
    public const string DefaultPrompt =
        "Исправь грамматику, пунктуацию и форматирование в тексте диктовки. Сохрани смысл и стиль речи. Верни только исправленный текст без пояснений.\n\nТекст:\n{text}";

    public const string DefaultCustomBaseUrl = "https://api.openai.com/v1";
}

public static class EditorProviderExtensions
{
    public static readonly string[] DisplayNames =
    {
        "Ollama",
        "LM Studio",
        "OpenAI",
        "OpenRouter",
        "Groq",
        "DeepSeek",
        "Свой (OpenAI API)"
    };

    public static bool IsLocal(this EditorProviderKind provider) =>
        provider is EditorProviderKind.Ollama or EditorProviderKind.LmStudio;

    public static bool RequiresApiKey(this EditorProviderKind provider) => !provider.IsLocal();

    public static bool RequiresBaseUrl(this EditorProviderKind provider) =>
        provider is EditorProviderKind.CustomOpenAi;

    public static int GetDefaultPort(this EditorProviderKind provider) => provider switch
    {
        EditorProviderKind.Ollama => 11434,
        EditorProviderKind.LmStudio => 1234,
        _ => 11434
    };

    public static string GetDefaultBaseUrl(this EditorProviderKind provider) => provider switch
    {
        EditorProviderKind.OpenAi => "https://api.openai.com/v1",
        EditorProviderKind.OpenRouter => "https://openrouter.ai/api/v1",
        EditorProviderKind.Groq => "https://api.groq.com/openai/v1",
        EditorProviderKind.DeepSeek => "https://api.deepseek.com/v1",
        EditorProviderKind.CustomOpenAi => EditorDefaults.DefaultCustomBaseUrl,
        _ => ""
    };

    public static string ToStorageId(this EditorProviderKind provider) => provider switch
    {
        EditorProviderKind.Ollama => "ollama",
        EditorProviderKind.LmStudio => "lmstudio",
        EditorProviderKind.OpenAi => "openai",
        EditorProviderKind.OpenRouter => "openrouter",
        EditorProviderKind.Groq => "groq",
        EditorProviderKind.DeepSeek => "deepseek",
        EditorProviderKind.CustomOpenAi => "custom",
        _ => "ollama"
    };

    public static EditorProviderKind FromStorageId(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "lmstudio" or "lm-studio" or "lm_studio" => EditorProviderKind.LmStudio,
        "openai" => EditorProviderKind.OpenAi,
        "openrouter" => EditorProviderKind.OpenRouter,
        "groq" => EditorProviderKind.Groq,
        "deepseek" => EditorProviderKind.DeepSeek,
        "custom" or "customopenai" or "custom-openai" => EditorProviderKind.CustomOpenAi,
        _ => EditorProviderKind.Ollama
    };

    public static int ToIndex(this EditorProviderKind provider) => (int)provider;

    public static EditorProviderKind FromIndex(int index) =>
        index is >= 0 and <= (int)EditorProviderKind.CustomOpenAi
            ? (EditorProviderKind)index
            : EditorProviderKind.Ollama;

    public static bool IsConfigured(EditorProviderKind provider, string? model, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        return provider.IsLocal() || !string.IsNullOrWhiteSpace(apiKey);
    }
}

public sealed class EditorConfig
{
    public EditorProviderKind Provider { get; init; } = EditorProviderKind.Ollama;
    public int Port { get; init; } = 11434;
    public string Model { get; init; } = "";
    public string Prompt { get; init; } = EditorDefaults.DefaultPrompt;
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; }
}
