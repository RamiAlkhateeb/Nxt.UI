# Nxt.UI

Shared Blazor component library for the family of apps: **Makdous** (NxtWeek), **NxtTask** (task-breaker) and **Syrian Radio**.

Each app consumes it as a git submodule at `lib/Nxt.UI` with a `ProjectReference`.

## What's inside
- Design tokens + light/dark theme (`wwwroot/nxt.css`), runtime helpers (`wwwroot/nxt.js`)
- Components: `NxtButton`, `NxtIconButton`, `NxtCard`, `NxtPageHeader`, `NxtBottomSheet`, `NxtEmptyState`, `NxtTextInput`, `NxtChip`, `NxtSegmented`, `NxtSpinner`, `NxtSkeleton`, `NxtToastHost` (+ `ToastService`), `NxtVersionBanner`, `NxtInstallCarousel`, `NxtAppLogo`, `NxtIcon` (line icons by name; `NxtSettingsSection` and `NxtPageHeader` take an icon name, or an emoji as before)
- `NxtSettingsPage`: the same settings cards in every app (app sections → language → appearance → AI → install → about)
- AI: `GeminiClient` (structured JSON; tries the chosen model, then up to three chat models the key can reach, and reports it in `LastFallback`; failures are `GeminiApiException` with Google's own reason, shown in the user's language by `NxtText.AiError`), `AiSettingsService` (the model saves as soon as it is picked), and the `AiChat` component
- `wwwroot/logos/`: the family logos (PNG, cut from `design/family-icons-source.webp`): NxtTask, Makdous, Syrian Radio, Courses, Carousel Engine. `tools/export-icons.sh <name> <dir>` writes an app's favicon, PWA and apple-touch icons

## Using it in an app
```html
<!-- index.html <head>, before the app's own css -->
<link rel="stylesheet" href="_content/Nxt.UI/nxt.css" />
<link rel="stylesheet" href="css/app.css" />
<script src="_content/Nxt.UI/nxt.js"></script>
```
```csharp
builder.Services.AddSingleton<INxtLocale>(new FixedLocale(FixedLocale.Arabic)); // or an adapter
builder.Services.AddNxtUi(o => { o.App = NxtAppId.Radio; o.LogoUrl = "img/logo.png"; });
```
Put `<NxtToastHost />` and `<NxtVersionBanner />` in the main layout.
