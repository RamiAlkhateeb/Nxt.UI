namespace Nxt.UI;

/// <summary>Which app of the family is hosting the library. Drives the About card and the "more apps" row.</summary>
public enum NxtAppId { Makdous, NxtTask, Radio }

/// <summary>
/// Per-app configuration passed to <see cref="NxtServiceCollectionExtensions.AddNxtUi"/>.
/// Everything visual is shared; only identity and storage migration differ between apps.
/// </summary>
public sealed class NxtOptions
{
    public NxtAppId App { get; set; }

    /// <summary>Localised app description for the About card, keyed by language code.</summary>
    public Dictionary<string, string> Description { get; set; } = new();

    /// <summary>Path of the app's own logo, relative to the app base.</summary>
    public string LogoUrl { get; set; } = "logo.svg";

    /// <summary>
    /// Old, app-specific localStorage keys that held a value now stored under an <c>nxt.*</c> key.
    /// Read once when the new key is empty, then copied across, so users keep their settings.
    /// Key: new <c>nxt.*</c> key. Value: legacy key.
    /// </summary>
    public Dictionary<string, string> LegacyStorageKeys { get; set; } = new();

    /// <summary>Path the "go to settings" links point at.</summary>
    public string SettingsHref { get; set; } = "settings";
}

/// <summary>One app of the family, used by the About card's "more apps" row.</summary>
public sealed record NxtFamilyApp(NxtAppId Id, string NameAr, string NameEn, string Url, string Logo);

public static class NxtFamily
{
    public static readonly IReadOnlyList<NxtFamilyApp> Apps =
    [
        new(NxtAppId.Makdous, "مكدوس", "Makdous", "https://meal-planner-af799.web.app/", "_content/Nxt.UI/logos/makdous.svg"),
        new(NxtAppId.NxtTask, "نكست تاسك", "NxtTask", "https://task-breaker.pages.dev/", "_content/Nxt.UI/logos/nxttask.svg"),
        new(NxtAppId.Radio, "راديو سوريا", "Syrian Radio", "https://syrian-radio.pages.dev/", "_content/Nxt.UI/logos/radio.svg"),
    ];

    public static NxtFamilyApp Get(NxtAppId id) => Apps.First(a => a.Id == id);
}
