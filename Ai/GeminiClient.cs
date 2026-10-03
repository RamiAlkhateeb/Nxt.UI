using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nxt.UI.Ai;

/// <summary>Thrown when the assistant is used before a key is saved. &lt;AiChat&gt; turns it into a "go to Settings" bubble.</summary>
public sealed class AiKeyMissingException : InvalidOperationException
{
    public AiKeyMissingException() : base("No Gemini API key set. Add one in Settings.") { }
}

/// <summary>
/// Shared Gemini REST client for the whole family. Tries the user's selected model, then falls back through a
/// known-good list (filtered to what the key can actually reach), and forces structured JSON back via
/// <c>response_schema</c> so callers never parse free text. Domain prompts live in each app.
/// </summary>
public sealed class GeminiClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";

    private static readonly string[] FallbackModels =
    [
        "gemini-3.5-flash-lite",
        "gemini-2.0-flash",
        "gemini-2.0-flash-lite",
        "gemini-1.5-flash",
        "gemini-1.5-pro"
    ];

    private readonly HttpClient _http;
    private readonly AiSettingsService _settings;

    public GeminiClient(HttpClient http, AiSettingsService settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<bool> HasKeyAsync() => !string.IsNullOrWhiteSpace(await _settings.GetApiKeyAsync());

    public async Task<List<string>> ListModelsAsync()
    {
        var apiKey = await _settings.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) return [];

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}models?pageSize=200");
            request.Headers.Add("x-goog-api-key", apiKey);
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return [];

            var result = await response.Content.ReadFromJsonAsync<ModelListResponse>();
            return result?.Models?
                .Where(model => model.SupportedGenerationMethods?.Contains("generateContent", StringComparer.OrdinalIgnoreCase) == true)
                .Select(model => model.Name?.Replace("models/", "", StringComparison.OrdinalIgnoreCase))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Sends <paramref name="prompt"/> and deserialises the JSON reply into <typeparamref name="T"/>.
    /// <paramref name="schema"/> is a Gemini <c>response_schema</c> object (anonymous types work well).
    /// <paramref name="validate"/> may throw <see cref="InvalidOperationException"/> to reject a reply and try the next model.
    /// </summary>
    public async Task<T> GenerateJsonAsync<T>(string prompt, object schema, Func<T, T>? validate = null)
    {
        var apiKey = await _settings.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) throw new AiKeyMissingException();

        Exception? lastError = null;
        foreach (var model in await GetModelsToTryAsync())
        {
            try
            {
                var result = await GenerateCoreAsync<T>(prompt, schema, model, apiKey);
                return validate is null ? result : validate(result);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException(
            lastError?.Message ?? "None of the Gemini models could answer. Check your API key, model access, and connection.",
            lastError);
    }

    private async Task<IEnumerable<string>> GetModelsToTryAsync()
    {
        var selected = await _settings.GetModelAsync();
        var available = await ListModelsAsync();
        var fallbacks = available.Count == 0
            ? FallbackModels
            : FallbackModels.Where(model => available.Contains(model, StringComparer.OrdinalIgnoreCase));
        return new[] { selected }.Concat(fallbacks).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<T> GenerateCoreAsync<T>(string prompt, object schema, string model, string apiKey)
    {
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { response_mime_type = "application/json", response_schema = schema }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}models/{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("x-goog-api-key", apiKey);

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("Gemini returned no content.");
        return JsonSerializer.Deserialize<T>(text, JsonOptions)
            ?? throw new InvalidOperationException("Gemini returned an invalid response.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class ModelListResponse { [JsonPropertyName("models")] public List<GeminiModel>? Models { get; set; } }
    private sealed class GeminiModel
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("supportedGenerationMethods")] public List<string>? SupportedGenerationMethods { get; set; }
    }
    private sealed class GeminiResponse { [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; } }
    private sealed class Candidate { [JsonPropertyName("content")] public Content? Content { get; set; } }
    private sealed class Content { [JsonPropertyName("parts")] public List<Part>? Parts { get; set; } }
    private sealed class Part { [JsonPropertyName("text")] public string? Text { get; set; } }
}
