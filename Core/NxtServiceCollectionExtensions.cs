using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nxt.UI.Ai;

namespace Nxt.UI;

public static class NxtServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared family services. Register an <see cref="INxtLocale"/> before or after this call;
    /// if none is registered the library falls back to Arabic.
    /// Requires an <see cref="HttpClient"/> registration (the Blazor WASM template default).
    /// </summary>
    public static IServiceCollection AddNxtUi(this IServiceCollection services, Action<NxtOptions> configure)
    {
        var options = new NxtOptions();
        configure(options);

        services.AddSingleton(options);
        services.TryAddSingleton<INxtLocale>(new FixedLocale(FixedLocale.Arabic));
        services.AddScoped<LocalStore>();
        services.AddScoped<NxtText>();
        services.AddScoped<ThemeService>();
        services.AddScoped<ToastService>();
        services.AddScoped<AiSettingsService>();
        services.AddScoped<GeminiClient>();
        return services;
    }
}
