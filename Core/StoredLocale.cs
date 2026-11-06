using Microsoft.JSInterop;

namespace Nxt.UI;

/// <summary>
/// A switchable <see cref="INxtLocale"/> for apps without their own localization service.
/// The choice is kept in <c>nxt.language</c>; on first run the browser language is used when it is offered,
/// otherwise the first language in the list. Call <see cref="InitializeAsync"/> once at start-up (main layout).
/// </summary>
public sealed class StoredLocale : INxtLocale
{
    public const string StorageKey = "nxt.language";

    private readonly LocalStore _store;
    private readonly IJSRuntime _js;
    private NxtLanguage _current;

    public StoredLocale(LocalStore store, IJSRuntime js, IReadOnlyList<NxtLanguage> languages)
    {
        if (languages.Count == 0) throw new ArgumentException("At least one language is required.", nameof(languages));
        _store = store;
        _js = js;
        Languages = languages;
        _current = languages[0];
    }

    public string Language => _current.Code;
    public bool IsRtl => _current.IsRtl;
    public string Direction => IsRtl ? "rtl" : "ltr";
    public IReadOnlyList<NxtLanguage> Languages { get; }
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var code = await _store.GetAsync(StorageKey);
        if (string.IsNullOrEmpty(code))
        {
            try { code = (await _js.InvokeAsync<string?>("nxt.browserLanguage"))?.Split('-')[0]; }
            catch (JSException) { code = null; }
        }
        var match = Find(code);
        if (match is not null && match != _current)
        {
            _current = match;
            Changed?.Invoke();
        }
    }

    public async Task SetLanguageAsync(string code)
    {
        var match = Find(code);
        if (match is null) return;
        await _store.SetAsync(StorageKey, match.Code);
        if (match == _current) return;
        _current = match;
        Changed?.Invoke();
    }

    private NxtLanguage? Find(string? code) =>
        code is null ? null : Languages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
}
