# AGENTS.md

## Must-follow constraints
- **SQLite Limitation:** `sqlite-net-pcl` does not support `List<T>` properties. [cite_start]You must use the `[Ignore]` attribute on list properties and manage relationships manually via Foreign Keys (IDs). [cite: 375, 376, 377]
- **UI Alerts:** Do not call `Application.Current.MainPage.DisplayAlert` directly. [cite_start]You must use `UiHelper.ShowConfirm` or `UiHelper.ShowAlert` to prevent null reference crashes and handle the `.NET 9` windowing model safely. [cite: 130, 134]
- [cite_start]**Navigation:** All cross-platform navigation must be triggered via `NavigationBridge` to ensure the Native XAML TabBar and Blazor WebView stay in sync. [cite: 216]
- **App Startup:** Do not set `MainPage` in `App.xaml.cs`. You must override `CreateWindow` and return a `new Window(mainPage)`.

## UI & XAML Constraints
- **XAML Icon Paths:** Do not assign `Data="{x:Static local:IconPaths...}"` in XAML. This causes a TypeMismatch. You must assign the `Data` property in the code-behind (`.xaml.cs`) using `PathGeometryConverter`.
- **CSS Isolation:** Do not use `<style>` blocks inside `.razor` files for complex animations (like `@@keyframes`). You must use isolated CSS files (`FileName.razor.css`) to avoid Razor linter errors.
- **Click-Through Prevention:** Native UI overlays (like the Nav Bar) must have `InputTransparent="False"` AND `CascadeInputTransparent="False"` set on the container `Grid` to prevent touch events from leaking into the `BlazorWebView`.

## Important locations
- **Database Logic:** `Services/LocalDbService.cs` (Must call `Init()` before any operation).
- **Global Styles:** `wwwroot/css/app.css` (Contains the `--refine-` CSS variables).
- **Native Navigation:** `MainPage.xaml` and `MainPage.xaml.cs`.
- **Icon Definitions:** `IconPaths.cs` (Contains SVG path strings).

## Validation before finishing
- **Ghost Errors:** If `InitializeComponent` is not found or XAML changes don't reflect, you must instruct the user to manually delete `bin` and `obj` folders and `Rebuild`.
- **Null Safety:** Ensure all `Application.Current` calls use null-conditional operators (`?.`) or explicit null checks.
- **Build Mode:** Verify project compiles in `Release` configuration to catch AAB/APK packaging issues.

## Known gotchas
- **Android API:** Minimum supported version is **API 24 (Android 7.0)**. [cite_start]Do not use APIs introduced in later versions without platform checks. [cite: 40, 262]