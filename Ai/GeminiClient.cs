using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Nxt.UI.Ai;

/// <summary>Thrown when the assistant is used before a key is saved. &lt;AiChat&gt; turns it into a "go to Settings" bubble.</summary>
public sealed class AiKeyMissingException : InvalidOperationException
{
    public AiKeyMissingException() : base("No Gemini API key set. Add one in Settings.") { }
}

public enum GeminiErrorKind
{
    /// <summary>The key is wrong, revoked or not allowed (400 API_KEY_INVALID, 401, 403). No other model is tried.</summary>
    InvalidKey,
    /// <summary>Rate limit or free-tier quota (429).</summary>
    Quota,
    /// <summary>The model does not exist for this key or cannot do structured JSON (404, other 400s).</summary>
    ModelUnavailable,
    /// <summary>The reply was cut off or withheld (safety, length).</summary>
    Blocked,
    /// <summary>Anything else: 5xx, network, an unreadable reply.</summary>
    Other
}

/// <summary>
/// A Gemini call that failed, with Google's own explanation in <see cref="Detail"/>. Derives from
/// <see cref="InvalidOperationException"/> so existing catch blocks keep working. Show it with <c>NxtText.AiError</c>.
/// </summary>
public sealed class GeminiApiException : InvalidOperationException
{
    public GeminiApiException(GeminiErrorKind kind, string? model, int? statusCode, string detail, Exception? inner = null)
        : base(detail, inner)
    {
        Kind = kind;
        Model = model;
        StatusCode = statusCode;
        Detail = detail;
    }

    public GeminiErrorKind Kind { get; }
    public string? Model { get; }
    public int? StatusCode { get; }
    public string Detail { get; }
}

/// <summary>The user's model did not answer, so another one did. Apps show this so the choice is never ignored silently.</summary>
public sealed record GeminiFallback(string Requested, string Used, GeminiApiException Reason);

/// <summary>
/// Shared Gemini REST client for the whole family. Tries the user's selected model, then up to three other chat
/// models the key can reach, and forces structured JSON back via <c>response_schema</c> so callers never parse free
/// text. Domain prompts live in each app.
/// </summary>
public sealed class GeminiClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";
    private const int MaxFallbacks = 3;

    /// <summary>Only used when the key's model list cannot be read. The "-latest" aliases follow Google's current Flash.</summary>
    private static readonly string[] OfflineFallbacks =
    [
        "gemini-flash-latest",
        "gemini-flash-lite-latest",
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite"
    ];

    // Gemini models that cannot answer a text prompt with JSON.
    private static readonly string[] NonChatMarkers =
        ["tts", "image", "embedding", "audio", "live", "computer-use", "robotics", "aqa", "veo", "imagen"];

    private readonly HttpClient _http;
    private readonly AiSettingsService _settings;
    private string? _modelsForKey;
    private List<string>? _models;

    public GeminiClient(HttpClient http, AiSettingsService settings)
    {
        _http = http;
        _settings = settings;
    }

    /// <summary>The model that produced the last successful reply.</summary>
    public string? LastModel { get; private set; }

    /// <summary>Set when the last successful reply did not come from the selected model; null otherwise.</summary>
    public GeminiFallback? LastFallback { get; private set; }

    public async Task<bool> HasKeyAsync() => !string.IsNullOrWhiteSpace(await _settings.GetApiKeyAsync());

    /// <summary>A Gemini model that can answer a text prompt with JSON (no speech, image, embedding or live models).</summary>
    public static bool IsChatModel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim().ToLowerInvariant();
        return n.StartsWith("gemini-", StringComparison.Ordinal) && !NonChatMarkers.Any(n.Contains);
    }

    /// <summary>
    /// Backup models to try after <paramref name="selected"/>: chat models only, Flash-Lite first (cheapest, most
    /// generous free quota), then Flash, then the rest; newest version first within each group.
    /// </summary>
    public static IReadOnlyList<string> RankFallbacks(IEnumerable<string> available, string selected, int max = MaxFallbacks) =>
        available
            .Where(IsChatModel)
            .Where(m => !string.Equals(m, selected, StringComparison.OrdinalIgnoreCase))
            .Where(m => !m.Contains("preview", StringComparison.OrdinalIgnoreCase) && !m.Contains("exp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Tier)
            .ThenByDescending(m => m.Contains("latest", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(Version)
            .ThenBy(m => m.Length)
            .Take(max)
            .ToList();

    private static int Tier(string model) =>
        model.Contains("flash-lite", StringComparison.OrdinalIgnoreCase) ? 0
        : model.Contains("flash", StringComparison.OrdinalIgnoreCase) ? 1
        : 2;

    private static double Version(string model)
    {
        var match = Regex.Match(model, @"gemini-(\d+(?:\.\d+)?)");
        return match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>
    /// The chat models this key can use, newest first. Cached for the session per key; pass
    /// <paramref name="refresh"/> to read it again (the Settings refresh button).
    /// </summary>
    public async Task<List<string>> ListModelsAsync(bool refresh = false)
    {
        var apiKey = await _settings.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) return [];
        if (!refresh && _models is not null && _modelsForKey == apiKey) return _models;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}models?pageSize=1000");
            request.Headers.Add("x-goog-api-key", apiKey);
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return [];

            var result = await response.Content.ReadFromJsonAsync<ModelListResponse>();
            var models = result?.Models?
                .Where(model => model.SupportedGenerationMethods?.Contains("generateContent", StringComparer.OrdinalIgnoreCase) == true)
                .Select(model => model.Name?.Replace("models/", "", StringComparison.OrdinalIgnoreCase))
                .Where(IsChatModel)
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(Version)
                .ThenBy(Tier)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            _models = models;
            _modelsForKey = apiKey;
            return models;
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
    /// Failures surface as <see cref="GeminiApiException"/> carrying Google's own message.
    /// </summary>
    public async Task<T> GenerateJsonAsync<T>(string prompt, object schema, Func<T, T>? validate = null)
    {
        var apiKey = await _settings.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) throw new AiKeyMissingException();

        var selected = await _settings.GetModelAsync();
        LastFallback = null;
        GeminiApiException? firstError = null;
        GeminiApiException? lastError = null;
        foreach (var model in await GetModelsToTryAsync(selected))
        {
            try
            {
                var result = await GenerateCoreAsync<T>(prompt, schema, model, apiKey);
                if (validate is not null) result = validate(result);
                LastModel = model;
                LastFallback = firstError is not null && !string.Equals(model, selected, StringComparison.OrdinalIgnoreCase)
                    ? new GeminiFallback(selected, model, firstError)
                    : null;
                return result;
            }
            catch (GeminiApiException ex)
            {
                lastError = ex;
                firstError ??= ex;
                // A bad key fails the same way on every model; asking the others only adds delay.
                if (ex.Kind == GeminiErrorKind.InvalidKey) throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                lastError = new GeminiApiException(GeminiErrorKind.Other, model, null, ex.Message, ex);
                firstError ??= lastError;
            }
        }

        // The selected model's error explains most; a later one only says the backups failed too.
        throw firstError ?? lastError
            ?? new GeminiApiException(GeminiErrorKind.Other, selected, null, "None of the Gemini models could answer.");
    }

    private async Task<IEnumerable<string>> GetModelsToTryAsync(string selected)
    {
        var available = await ListModelsAsync();
        var fallbacks = available.Count == 0 ? RankFallbacks(OfflineFallbacks, selected) : RankFallbacks(available, selected);
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
        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, model);

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        if (result?.PromptFeedback?.BlockReason is { Length: > 0 } blocked)
            throw new GeminiApiException(GeminiErrorKind.Blocked, model, null, blocked);

        var candidate = result?.Candidates?.FirstOrDefault()
            ?? throw new GeminiApiException(GeminiErrorKind.Other, model, null, "Gemini returned no answer.");
        // A reply can arrive in several parts; thinking models may add thought parts that are not the answer.
        var text = string.Concat(candidate.Content?.Parts?.Where(p => p.Thought != true).Select(p => p.Text) ?? []);
        if (candidate.FinishReason is "MAX_TOKENS" or "SAFETY" or "RECITATION" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII")
            throw new GeminiApiException(GeminiErrorKind.Blocked, model, null, candidate.FinishReason);
        if (string.IsNullOrWhiteSpace(text))
            throw new GeminiApiException(GeminiErrorKind.Other, model, null, "Gemini returned no content.");

        try
        {
            return JsonSerializer.Deserialize<T>(text, JsonOptions)
                ?? throw new GeminiApiException(GeminiErrorKind.Other, model, null, "Gemini returned an invalid response.");
        }
        catch (JsonException ex)
        {
            throw new GeminiApiException(GeminiErrorKind.Other, model, null, "Gemini returned an invalid response.", ex);
        }
    }

    /// <summary>Keeps Google's own explanation ("API key not valid", "You exceeded your current quota…").</summary>
    private static async Task<GeminiApiException> ToExceptionAsync(HttpResponseMessage response, string model)
    {
        var status = (int)response.StatusCode;
        string? message = null, reason = null, statusText = null;
        try
        {
            var error = (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error;
            message = error?.Message;
            statusText = error?.Status;
            reason = error?.Details?.Select(d => d.Reason).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException) { /* no JSON body */ }

        var detail = string.IsNullOrWhiteSpace(message) ? $"{status} {response.ReasonPhrase}" : message.Trim();
        var kind = ClassifyError(status, reason, statusText, message);
        return new GeminiApiException(kind, model, status, detail);
    }

    /// <summary>Maps an HTTP error from Gemini to what the user should do about it.</summary>
    public static GeminiErrorKind ClassifyError(int status, string? reason, string? statusText, string? message)
    {
        var text = $"{reason} {statusText} {message}";
        if (status is 401 or 403 || text.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase)
            || text.Contains("API key not valid", StringComparison.OrdinalIgnoreCase)
            || text.Contains("API key expired", StringComparison.OrdinalIgnoreCase))
            return GeminiErrorKind.InvalidKey;
        if (status == (int)HttpStatusCode.TooManyRequests || text.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
            return GeminiErrorKind.Quota;
        if (status is 404 or 400) return GeminiErrorKind.ModelUnavailable;
        return GeminiErrorKind.Other;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class ModelListResponse { [JsonPropertyName("models")] public List<GeminiModel>? Models { get; set; } }
    private sealed class GeminiModel
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("supportedGenerationMethods")] public List<string>? SupportedGenerationMethods { get; set; }
    }
    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; }
        [JsonPropertyName("promptFeedback")] public PromptFeedback? PromptFeedback { get; set; }
    }
    private sealed class PromptFeedback { [JsonPropertyName("blockReason")] public string? BlockReason { get; set; } }
    private sealed class Candidate
    {
        [JsonPropertyName("content")] public Content? Content { get; set; }
        [JsonPropertyName("finishReason")] public string? FinishReason { get; set; }
    }
    private sealed class Content { [JsonPropertyName("parts")] public List<Part>? Parts { get; set; } }
    private sealed class Part
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("thought")] public bool? Thought { get; set; }
    }
    private sealed class ErrorResponse { [JsonPropertyName("error")] public ErrorBody? Error { get; set; } }
    private sealed class ErrorBody
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("details")] public List<ErrorDetail>? Details { get; set; }
    }
    private sealed class ErrorDetail { [JsonPropertyName("reason")] public string? Reason { get; set; } }
}
