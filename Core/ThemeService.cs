using Microsoft.JSInterop;

namespace Nxt.UI;

/// <summary>
/// Light / dark / system appearance. nxt.js applies the stored choice before Blazor boots
/// (no flash) by setting <c>data-theme</c> on &lt;html&gt;; this service changes it at runtime.
/// </summary>
public sealed class ThemeService
{
    public const string StorageKey = "nxt.theme";
    public static readonly string[] Modes = ["system", "light", "dark"];

    private readonly IJSRuntime _js;
    private readonly LocalStore _store;

    public ThemeService(IJSRuntime js, LocalStore store)
    {
        _js = js;
        _store = store;
    }

    public async Task<string> GetAsync()
    {
        var mode = await _store.GetAsync(StorageKey);
        return Modes.Contains(mode) ? mode! : "system";
    }

    public async Task SetAsync(string mode)
    {
        if (!Modes.Contains(mode)) mode = "system";
        await _store.SetAsync(StorageKey, mode);
        await _js.InvokeVoidAsync("nxt.applyTheme", mode);
    }
}
