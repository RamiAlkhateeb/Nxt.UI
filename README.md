# Nxt.UI

Shared Blazor component library for the family of apps: **Makdous** (NxtWeek), **NxtTask** (task-breaker) and **Syrian Radio**.

Each app consumes it as a git submodule at `lib/Nxt.UI` with a `ProjectReference`.

## What's inside
- Design tokens + light/dark theme (`wwwroot/nxt.css`), runtime helpers (`wwwroot/nxt.js`)
- Components: `NxtButton`, `NxtIconButton`, `NxtCard`, `NxtPageHeader`, `NxtBottomSheet`, `NxtEmptyState`, `NxtTextInput`, `NxtChip`, `NxtSegmented`, `NxtSpinner`, `NxtSkeleton`, `NxtToastHost` (+ `ToastService`), `NxtVersionBanner`, `NxtInstallCarousel`, `NxtAppLogo`
- `NxtSettingsPage`: the same settings cards in every app (app sections → language → appearance → AI → install → about)
- AI: `GeminiClient` (model fallback, structured JSON), `AiSettingsService`, and the `AiChat` component
- `wwwroot/logos/`: the family logo SVGs

## Using it in an app
```html
<!-- index.html <head>, before the app's own css -->
<link rel="stylesheet" href="_content/Nxt.UI/nxt.css" />
<link rel="stylesheet" href="css/app.css" />
<script src="_content/Nxt.UI/nxt.js"></script>
```
```csharp
builder.Services.AddSingleton<INxtLocale>(new FixedLocale(FixedLocale.Arabic)); // or an adapter
builder.Services.AddNxtUi(o => { o.App = NxtAppId.Radio; o.LogoUrl = "img/logo.svg"; });
```
Put `<NxtToastHost />` and `<NxtVersionBanner />` in the main layout.
