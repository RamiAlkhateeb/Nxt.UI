namespace Nxt.UI.Ai;

/// <summary>
/// The user's own Gemini key and preferred model. Client-side only: there is no backend in any of the apps,
/// so each person brings a free key from AI Studio.
/// </summary>
public sealed class AiSettingsService
{
    public const string ApiKeyKey = "nxt.ai.key";
    public const string ModelKey = "nxt.ai.model";
    public const string DefaultModel = "gemini-3.5-flash-lite";

    private readonly LocalStore _store;

    public AiSettingsService(LocalStore store) => _store = store;

    public Task<string?> GetApiKeyAsync() => _store.GetAsync(ApiKeyKey);

    public Task SaveApiKeyAsync(string apiKey) => _store.SetAsync(ApiKeyKey, apiKey.Trim());

    public async Task<string> GetModelAsync()
    {
        var model = await _store.GetAsync(ModelKey);
        return string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
    }

    public Task SaveModelAsync(string model) =>
        _store.SetAsync(ModelKey, string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim());
}
