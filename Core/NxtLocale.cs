namespace Nxt.UI;

public sealed record NxtLanguage(string Code, string NativeName, string Flag, bool IsRtl);

/// <summary>
/// The library's view of the host app's current language. Apps with their own localization
/// (Makdous) register an adapter; single-language apps use <see cref="FixedLocale"/>.
/// </summary>
public interface INxtLocale
{
    string Language { get; }
    bool IsRtl { get; }
    string Direction => IsRtl ? "rtl" : "ltr";
    IReadOnlyList<NxtLanguage> Languages { get; }
    Task SetLanguageAsync(string code);
    event Action? Changed;
}

public sealed class FixedLocale : INxtLocale
{
    public FixedLocale(NxtLanguage language) => Languages = [language];

    public string Language => Languages[0].Code;
    public bool IsRtl => Languages[0].IsRtl;
    public IReadOnlyList<NxtLanguage> Languages { get; }
    public Task SetLanguageAsync(string code) => Task.CompletedTask;
    public event Action? Changed { add { } remove { } }

    public static readonly NxtLanguage Arabic = new("ar", "العربية", "🇸🇾", true);
    public static readonly NxtLanguage English = new("en", "English", "🇬🇧", false);
}
