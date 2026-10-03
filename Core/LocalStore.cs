using System.Text.Json;
using Microsoft.JSInterop;

namespace Nxt.UI;

/// <summary>
/// Thin localStorage wrapper. Every app runs on its own origin, so plain <c>nxt.*</c> keys never collide.
/// Values are stored as raw strings; legacy values written by Blazored.LocalStorage (JSON-encoded) are unwrapped on migration.
/// </summary>
public sealed class LocalStore
{
    private readonly IJSRuntime _js;
    private readonly NxtOptions _options;

    public LocalStore(IJSRuntime js, NxtOptions options)
    {
        _js = js;
        _options = options;
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        if (!string.IsNullOrEmpty(value) || !_options.LegacyStorageKeys.TryGetValue(key, out var legacyKey))
            return value;

        var legacy = Unwrap(await _js.InvokeAsync<string?>("localStorage.getItem", legacyKey));
        if (!string.IsNullOrEmpty(legacy)) await SetAsync(key, legacy);
        return legacy;
    }

    public Task SetAsync(string key, string value) => _js.InvokeVoidAsync("localStorage.setItem", key, value).AsTask();

    public Task RemoveAsync(string key) => _js.InvokeVoidAsync("localStorage.removeItem", key).AsTask();

    private static string? Unwrap(string? raw)
    {
        if (raw is null || raw.Length < 2 || raw[0] != '"') return raw;
        try { return JsonSerializer.Deserialize<string>(raw); }
        catch (JsonException) { return raw; }
    }
}
