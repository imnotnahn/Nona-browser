Nona Browser (WPF + WebView2, .NET 8)

Nona is a lightweight desktop browser for Windows, focused on performance and privacy. It is built with .NET 8 + WPF and uses Microsoft Edge WebView2 (Chromium) as the rendering engine. The architecture is layered for clarity, extensibility, and tuning.

Features
- Lightweight native WPF UI; optimized memory footprint at idle.
- Multi-tier blocking: host, wildcard, substring (Aho-Corasick), regex, with intelligent whitelisting; 3 modes: Off / Balanced / Strict.
- HTTPS-Only upgrader; YouTube ads suppressed while keeping playback working. TikTok is fully whitelisted to avoid content playback issues.
- JSON theming (dark/light/modern) with hot-reload.
- History, Bookmarks (bookmark bar), basic Downloads tracking, Hard Refresh (Ctrl+F5), Command Palette (Ctrl+K).

Ecosystem & Technology
- C#: .NET 8, WPF (XAML)
- Engine: Microsoft Edge WebView2
- Storage: EF Core + SQLite (History, Bookmarks, Thumbnails, Downloads), JSON settings
- DI/Hosting: Microsoft.Extensions.Hosting / DependencyInjection
- Logging: Serilog (writes to `nona.log`)
- Tests: xUnit

Requirements
- Windows 10/11 x64
- .NET SDK 8.0+ (for development/build)
- Microsoft Edge WebView2 Runtime (Stable). Download at: `https://developer.microsoft.com/microsoft-edge/webview2/`

Quick start (Dev)
```powershell
dotnet build
dotnet test
dotnet run --project .\Nona.App\Nona.App.csproj
```

Release build
- Option 1: Use the all-in-one packaging script
  - Run `buildrelease.bat` to create `Nona-Browser-Release` folder containing: `Nona.exe` (single-file) + `Assets` + `runtimes` + required native dlls (WebView2Loader, e_sqlite3, ...).
- Option 2: Manual publish
  - Single-file (self-contained):
  ```powershell
  dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=false -o publish-single
  ```
  - Framework-dependent (to collect assets/runtime):
  ```powershell
  dotnet publish -c Release -r win-x64 -o publish
  ```

Usage quick keys
- Ctrl+L: focus address bar; Ctrl+T / Ctrl+W: open/close tab
- F5 / Ctrl+R: Reload; Ctrl+F5: Hard Refresh
- Ctrl+H / Ctrl+J / Ctrl+K: History / Downloads / Command Palette
- Alt+Left / Alt+Right: Back / Forward

Architecture overview
```mermaid
flowchart LR
  subgraph UI[WPF UI]
    MW[MainWindow]
    Windows[Settings/History/Downloads/Profiles]
    Styles[Styles + Assets]
  end

  subgraph App[Nona.App]
    MW -->|DI| Host[Generic Host]
  end

  Host --> Core[Nona.Core\nTabManager, Models]
  Host --> Engine[Nona.Engine\nWebEngine, Downloads, DoH]
  Host --> Security[Nona.Security\nRulesEngine, HTTPS Upgrader]
  Host --> Storage[Nona.Storage\nDbContext, Repos]
  Host --> Theme[Nona.Theming\nThemeService, Watcher]

  Engine -->|WebResourceRequested| Security
  Engine -->|CapturePreview| Storage
  Theme -->|ApplyToResources| UI
  Storage -->|SQLite| DB[(nona.db)]
```

Performance comparison (measured)
- Metrics are based on `result.txt` and updated after correcting process accounting (see notes). Values are indicative and environment-dependent.

| Browser | Cold start (ms) | Warm start (ms) | Idle CPU (%) | Idle RAM 1 tab (MB) | Idle RAM ~10 tabs total (MB) | Package size (MB) |
|---|---:|---:|---:|---:|---:|---:|
| Nona | 945 | 1030 | 0.2 | 202 | 1320 | 165 (single-file) |
| Brave | 423 | 286 | 1.1 | 291 | 1680 | — |
| Microsoft Edge | 520 | 300 | 0.6 | 260 | 1500 | — |
| Mozilla Firefox | 580 | 320 | 0.7 | 240 | - | — |

```mermaid
%%{init: {"theme": "default"}}%%
xychart-beta
  title "Idle RAM ~10 tabs total (MB) — lower is better"
  x-axis ["Nona","Brave","Microsoft Edge"]
  y-axis "MB" 0 --> 2000
  bar [1320, 1680, 1500]
```

Notes
- Cold/Warm start: Nona is intentionally slower than established browsers. Reasons include .NET/WPF startup (JIT), DI host construction, DB schema ensure, theming load, and WebView2 environment boot. These trade-offs are acceptable given Nona’s goals for simplicity and memory efficiency at steady state.
- Process accounting: On Windows, Nona uses multiple processes (e.g., `Nona.exe` and `WebView2*`). To compare fairly with Chromium-based browsers that show an aggregated "Brave" process, sum Nona’s `Nona.exe` + `WebView2*` working sets. After correction, Nona’s ~10-tab total RAM is comparable to Brave or lower.

Why the earlier RAM discrepancy?
- Different process models in Task Manager: Nona (WebView2 host) appears as multiple processes (`Nona.exe`, `WebView2Manager`, and child WebView2 processes). Brave often appears aggregated under the `Brave` process name. If you read only `Nona.exe`, memory looks artificially low.
- How to measure properly: In Task Manager → Details, filter and sum `Nona.exe` + `WebView2*` working sets for Nona; compare with Brave’s aggregated processes. Alternatively, use Resource Monitor or scripts to sum by process name.
- What design choices still help: Nona shares a single WebView2 environment per window (`WebEngine.GetEnvironmentAsync`), applies lean defaults in `ConfigureWebViewAsync`, and blocks many third‑party requests (`ExtendedRulesEngine`). These reduce background work and network/CPU overhead, but total memory with many active tabs remains largely dictated by the Chromium multi-process model, so totals are similar to Brave.

Trade-offs and limitations
- Start-up time: As noted, Nona is slower on cold and warm start due to .NET runtime, DI setup, first-time JIT, and environment initialization. These are areas for future improvement (e.g., trimming, ReadyToRun, delayed services).
- Compatibility: Blocking is conservative on main documents to avoid breakage, but strict mode can still impact some sites. TikTok is intentionally whitelisted.
- Feature scope: Reduced background features and no extensions keep memory low but also mean fewer capabilities than mainstream browsers.

Troubleshooting
- Missing WebView2 Runtime: install the Stable channel from Microsoft.
- SQLite error "no such table: History": delete the old DB at `%LOCALAPPDATA%\Nona\Default\nona.db` and run again; the app calls `EnsureCreated()` early in `App.xaml.cs`.
- XAML Style target mismatch: verify `TargetType` for each Style, especially in the Settings window.
- `Assets/nona.ico` not found: ensure the file exists and the resource path is correct; update `ApplicationIcon` in the csproj and XAML if needed.
- Logs: check `nona.log` in the working directory for runtime errors.

Folder structure
```
Nona.App/          WPF UI, Windows, Assets (themes, ntp, rules), Styles
Nona.Engine/       WebView2 environment/config, request blocking hook, downloads manager
Nona.Security/     HTTPS-only upgrader, RulesEngine (block/whitelist), BlockingMode
Nona.Storage/      EF Core Sqlite DbContext, repositories, SettingsStore (JSON)
Nona.Core/         Domain models, TabManager, ProfileManager, Session
Nona.Theming/      ThemeService + watcher
Nona.Tests/        xUnit tests
publish/           Output framework-dependent
publish-single/    Output single-file self-contained
Nona-Browser-Release/  Merged release folder (exe + assets + runtimes)
```

Roadmap
- Add more filter lists (EasyList/EasyPrivacy/uBO), advanced pattern caches.
- Basic extension host, multi-user profiles, full bookmarks manager, configurable DoH, session restore UI.
- Context-aware blocking (first/third-party), optional cosmetic rules.

License
MIT
