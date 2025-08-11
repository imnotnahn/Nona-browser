using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Nona.Engine;
using Nona.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nona.Storage;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Windows.Media;
using Nona.App.Windows;

namespace Nona.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IWebEngine _engine;
    private readonly IHttpsOnlyUpgrader _httpsOnly;
    private readonly IRulesEngine _rules;
    private readonly IHistoryRepository _history;
    private readonly IThumbnailRepository? _thumbRepo;

    private WebView2? _web;
    private readonly IBookmarksRepository _bookmarks;
    private readonly ICommandRegistry _commands;
    private readonly IDownloadsManager _downloads;

    public static readonly RoutedUICommand OpenCommandPalette = new RoutedUICommand("Open Command Palette", nameof(OpenCommandPalette), typeof(MainWindow));
    public static readonly RoutedUICommand HardRefreshCommand = new RoutedUICommand("Hard Refresh", nameof(HardRefreshCommand), typeof(MainWindow));

    public MainWindow(IWebEngine engine,
                      IHttpsOnlyUpgrader httpsOnly,
                      IRulesEngine rules,
                      IHistoryRepository history,
                      IBookmarksRepository bookmarks,
                      ICommandRegistry commands,
                      IDownloadsManager downloads,
                      IThumbnailRepository? thumbRepo = null)
    {
        InitializeComponent();
        _engine = engine;
        _httpsOnly = httpsOnly;
        _rules = rules;
        _history = history;
        _bookmarks = bookmarks;
        _commands = commands;
        _downloads = downloads;
        _thumbRepo = thumbRepo;
        CommandBindings.Add(new CommandBinding(OpenCommandPalette, (_, __) => OpenCommandPaletteWindow()));
        CommandBindings.Add(new CommandBinding(HardRefreshCommand, HardRefresh_Executed));
        
        // Initialize background image handling
        Loaded += MainWindow_Loaded;
        CommandBindings.Add(new CommandBinding(NavigationCommands.Refresh, (_, __) => Reload_Click(null!, null!)));
        CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseForward, (_, __) => Forward_Click(null!, null!)));
        RegisterCommands();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RestoreSessionAsync();
        // Load ad blocking rules
        try
        {
            var rulesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "rules");
            if (System.IO.Directory.Exists(rulesPath))
            {
                var files = System.IO.Directory.GetFiles(rulesPath, "*.txt");
                Console.WriteLine($"Loading ad blocking rules from {files.Length} files...");
                await _rules.LoadListsAsync(files);
                Console.WriteLine("Ad blocking rules loaded successfully!");
            }
            else
            {
                Console.WriteLine("Rules directory not found, ad blocking disabled.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading ad blocking rules: {ex.Message}");
        }

        // Apply adblock mode from saved settings
        try
        {
            var settingsStore = ((App)Application.Current).Services.GetRequiredService<Nona.Storage.ISettingsStore>();
            var settings = await settingsStore.LoadAsync();
            var mode = (settings.EnableBlocking, (settings.BlockingMode ?? "Balanced").ToLowerInvariant()) switch
            {
                (false, _) => Nona.Security.BlockingMode.Off,
                (true, "strict") => Nona.Security.BlockingMode.Strict,
                _ => Nona.Security.BlockingMode.Balanced
            };
            _rules.Mode = mode;
        }
        catch { }

        // Load bookmarks to bar
        try { await LoadBookmarksBarAsync(); } catch { }
        
        // Update background image
        UpdateBackgroundImage();
    }

    public void UpdateBackgroundImage()
    {
        try
        {
            var backgroundPath = Application.Current.Resources["BackgroundImagePath"] as string;
            if (!string.IsNullOrWhiteSpace(backgroundPath) && File.Exists(backgroundPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(backgroundPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                
                BackgroundImage.Source = bitmap;
                BackgroundImage.Visibility = Visibility.Visible;
            }
            else
            {
                BackgroundImage.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            BackgroundImage.Visibility = Visibility.Collapsed;
        }
    }

    private System.Windows.Media.Color GetThemeBackgroundMediaColor()
    {
        try
        {
            var hex = Application.Current.Resources["BackgroundColor"]?.ToString() ?? "#FFFFFFFF";
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }
        catch { return System.Windows.Media.Colors.White; }
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        NavigateFromOmnibox();
    }

    private void Omnibox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateFromOmnibox();
        }
    }

    private async void NavigateFromOmnibox()
    {
        if (_web?.CoreWebView2 == null) return;
        var text = Omnibox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        
        Uri uri;
        if (Uri.TryCreate(text, UriKind.Absolute, out var abs))
        {
            uri = abs;
        }
        else
        {
            // Get search engine from settings
            var searchEngine = await GetSearchEngineAsync();
            uri = new Uri(searchEngine + Uri.EscapeDataString(text));
        }
        
        var upgraded = _httpsOnly.TryUpgrade(uri) ?? uri;
        _web.CoreWebView2.Navigate(upgraded.ToString());
    }

    private async Task<string> GetSearchEngineAsync()
    {
        try
        {
            var settingsStore = ((App)Application.Current).Services.GetRequiredService<Nona.Storage.ISettingsStore>();
            var settings = await settingsStore.LoadAsync();
            
            return settings.SearchEngine switch
            {
                "Google" => "https://www.google.com/search?q=",
                "DuckDuckGo" => "https://duckduckgo.com/?q=",
                "Yahoo" => "https://search.yahoo.com/search?p=",
                _ => "https://www.bing.com/search?q="
            };
        }
        catch
        {
            // Fallback to Bing if settings can't be loaded
            return "https://www.bing.com/search?q=";
        }
    }

    private async Task CreateTab(string? initialUrl = null)
    {
        try
        {
            var tab = new TabItem();
            var grid = new Grid();
            var web = new WebView2();
            
            // Add event handlers
            web.NavigationCompleted += Web_NavigationCompleted;
            web.CoreWebView2InitializationCompleted += async (s, e) =>
            {
                if (e.IsSuccess)
                {
                    try
                    {
                        web.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                        web.CoreWebView2.DocumentTitleChanged += (sender, args) =>
                        {
                            // Find the tab that belongs to this WebView2
                            foreach (TabItem tabItem in Tabs.Items)
                            {
                                if (tabItem.Content is Grid grid && 
                                    grid.Children.Count > 0 && 
                                    grid.Children[0] == web)
                                {
                                    var title = web.CoreWebView2.DocumentTitle;
                                    if (string.IsNullOrWhiteSpace(title))
                                    {
                                        try
                                        {
                                            var uri = new Uri(web.CoreWebView2.Source);
                                            title = uri.Host;
                                        }
                                        catch
                                        {
                                            title = "Loading...";
                                        }
                                    }
                                    SetTabHeader(tabItem, title);
                                    
                                    // Update window title if this is the active tab
                                    if (tabItem == Tabs.SelectedItem)
                                    {
                                        Title = title;
                                    }
                                    break;
                                }
                            }
                        };

                        // Handle new window requests
                        web.CoreWebView2.NewWindowRequested += (s, args) => { 
                            args.Handled = true; 
                            web.CoreWebView2.Navigate(args.Uri); 
                        };

                        // Configure web engine and rules
                        await _engine.ConfigureWebViewAsync(web.CoreWebView2, _rules);
                        _downloads.Track(web.CoreWebView2);

                        // Navigate to initial URL if provided
                        if (!string.IsNullOrWhiteSpace(initialUrl))
                        {
                            web.CoreWebView2.Navigate(initialUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error configuring WebView2: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("WebView2 initialization failed");
                }
            };

            grid.Children.Add(web);
            tab.Content = grid;
            Tabs.Items.Add(tab);
            Tabs.SelectedItem = tab;

            _web = web;
            
            // Initialize WebView2 environment
            var env = await _engine.GetEnvironmentAsync();
            await web.EnsureCoreWebView2Async(env);
            try
            {
                // Reduce white flash when creating/switching tabs by matching background color
                var mediaColor = GetThemeBackgroundMediaColor();
                var drawingColor = System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
                web.DefaultBackgroundColor = drawingColor;
            }
            catch { }
            
            // Set initial tab title
            SetTabHeader(tab, "New Tab");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating tab: {ex.Message}");
            MessageBox.Show($"Failed to create new tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e)
    {
        await CreateTab("https://www.bing.com");
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is TabItem ti && ti.Content is Grid g && g.Children.Count > 0)
        {
            _web = g.Children[0] as WebView2;
            if (_web?.CoreWebView2 != null)
            {
                Omnibox.Text = _web.CoreWebView2.Source ?? string.Empty;
                Title = _web.CoreWebView2.DocumentTitle ?? "Nona";
            }
            else
            {
                Omnibox.Text = string.Empty;
                Title = "Nona";
            }
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_web?.CoreWebView2?.CanGoBack == true) _web.CoreWebView2.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_web?.CoreWebView2?.CanGoForward == true) _web.CoreWebView2.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        _web?.CoreWebView2?.Reload();
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (_web?.CoreWebView2 == null) return;
        _web.CoreWebView2.Navigate("https://ntp.nona/index.html");
    }

    private void Web_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_web?.CoreWebView2?.Source != null)
        {
            Omnibox.Text = _web.CoreWebView2.Source;
            
            // Get title with fallback to URL domain
            var docTitle = _web.CoreWebView2.DocumentTitle;
            if (string.IsNullOrWhiteSpace(docTitle))
            {
                try
                {
                    var uri = new Uri(_web.CoreWebView2.Source);
                    docTitle = uri.Host;
                }
                catch
                {
                    docTitle = "Loading...";
                }
            }
            
            Title = docTitle;
            if (Tabs.SelectedItem is TabItem currentTab)
            {
                SetTabHeader(currentTab, docTitle);
            }
            _ = _history.AddAsync(_web.CoreWebView2.Source, _web.CoreWebView2.DocumentTitle);
            _ = SaveThumbnailAsync(_web);
        }
    }

    private void DevTools_Click(object sender, RoutedEventArgs e)
    {
        _web?.CoreWebView2?.OpenDevToolsWindow();
    }

    private void SetTabHeader(TabItem tab, string title)
    {
        // Truncate long titles and set tab header
        var displayTitle = string.IsNullOrWhiteSpace(title) ? "New Tab" : title;
        if (displayTitle.Length > 25)
        {
            displayTitle = displayTitle.Substring(0, 22) + "...";
        }
        tab.Header = displayTitle;
        tab.Style = (Style)FindResource("ModernTabItemStyle");
        
        // Add double-click to close functionality
        tab.MouseDoubleClick += async (s, e) =>
        {
            await CloseTab(tab);
        };
    }

    private async Task CloseTab(TabItem tab)
    {
        var idx = Tabs.Items.IndexOf(tab);
        
        // Properly dispose WebView2 to stop audio/video
        if (tab.Content is Grid grid && grid.Children.Count > 0)
        {
            var webView = grid.Children[0] as WebView2;
            if (webView != null)
            {
                try
                {
                    // Stop any media playback first
                    if (webView.CoreWebView2 != null)
                    {
                        // Execute JavaScript to stop all media
                        await webView.CoreWebView2.ExecuteScriptAsync(@"
                            document.querySelectorAll('audio, video').forEach(el => {
                                el.pause();
                                el.currentTime = 0;
                                el.src = '';
                                el.load();
                            });
                        ");
                        
                        // Navigate to blank page to stop any remaining processes
                        webView.CoreWebView2.Navigate("about:blank");
                        
                        // Wait a moment for the navigation to complete
                        await Task.Delay(100);
                    }
                    
                    // Remove event handlers to prevent memory leaks
                    webView.NavigationCompleted -= Web_NavigationCompleted;
                    
                    // Dispose the WebView2
                    webView.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing WebView2: {ex.Message}");
                    // Force dispose even if there was an error
                    try { webView.Dispose(); } catch { }
                }
            }
        }
        
        Tabs.Items.Remove(tab);
        
        if (Tabs.Items.Count == 0)
        {
            // If no tabs left, create a new one
            _ = CreateTab("https://www.bing.com");
        }
        else if (idx >= 0 && idx < Tabs.Items.Count)
        {
            Tabs.SelectedIndex = Math.Min(idx, Tabs.Items.Count - 1);
        }
        else if (Tabs.Items.Count > 0)
        {
            Tabs.SelectedIndex = Math.Max(0, idx - 1);
        }
    }

    private async Task LoadBookmarksBarAsync()
    {
        if (BookmarkBar == null) return;
        BookmarkBar.Children.Clear();
        var items = await _bookmarks.ListAsync();
        foreach (var b in items)
        {
            var btn = new Button 
            { 
                Content = b.Title, 
                Tag = b,
                Style = (Style)FindResource("BookmarkButtonStyle")
            };
            btn.Click += (_, __) =>
            {
                _web?.CoreWebView2?.Navigate(b.Url);
            };
            
            // Add context menu for bookmark deletion
            var contextMenu = new ContextMenu();
            var deleteItem = new MenuItem { Header = "Delete Bookmark" };
            deleteItem.Click += async (_, __) =>
            {
                var result = MessageBox.Show($"Delete bookmark '{b.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    await _bookmarks.DeleteAsync(b.Id);
                    await LoadBookmarksBarAsync();
                }
            };
            contextMenu.Items.Add(deleteItem);
            btn.ContextMenu = contextMenu;
            
            BookmarkBar.Children.Add(btn);
        }
    }

    private void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        var title = _web?.CoreWebView2?.DocumentTitle ?? "(No title)";
        var url = _web?.CoreWebView2?.Source ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(url))
        {
            _ = _bookmarks.AddAsync(title, url);
            _ = LoadBookmarksBarAsync();
        }
    }

    private void ToggleBookmarkBar_Click(object sender, RoutedEventArgs e)
    {
        BookmarkBarBorder.Visibility = BookmarkBarBorder.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => new SettingsWindow().Show();
    private void OpenHistory_Click(object sender, RoutedEventArgs e) => new HistoryWindow().Show();
    private void OpenProfiles_Click(object sender, RoutedEventArgs e) => new ProfilesWindow().Show();
    private CommandPaletteWindow? _palette;
    private void OpenCommandPaletteWindow()
    {
        if (_palette == null || !_palette.IsVisible)
        {
            _palette = new CommandPaletteWindow { Owner = this };
            _palette.Closed += (_, __) => _palette = null;
            _palette.Show();
        }
        else
        {
            _palette.Activate();
            _palette.Focus();
        }
    }

    private async Task HardRefreshAsync()
    {
        if (_web?.CoreWebView2 == null) return;
        try
        {
            await _web.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.setCacheDisabled", "{\"cacheDisabled\":true}");
            _web.CoreWebView2.Reload();
            await Task.Delay(200);
            await _web.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.setCacheDisabled", "{\"cacheDisabled\":false}");
        }
        catch
        {
            var url = _web.CoreWebView2.Source;
            if (!string.IsNullOrWhiteSpace(url))
            {
                var buster = $"__nocache={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var newUrl = url.Contains("?") ? url + "&" + buster : url + "?" + buster;
                _web.CoreWebView2.Navigate(newUrl);
            }
        }
    }

    private async void HardRefresh_Executed(object? sender, ExecutedRoutedEventArgs e)
    {
        await HardRefreshAsync();
    }

    private async Task SaveThumbnailAsync(WebView2? web)
    {
        if (web?.CoreWebView2 == null || _thumbRepo == null) return;
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", "Default", "thumbs");
            Directory.CreateDirectory(baseDir);
            var url = web.CoreWebView2.Source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return;
            var safeName = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(url)).Replace('/', '_');
            var file = Path.Combine(baseDir, safeName + ".png");
            await _engine.CapturePreviewPngAsync(web.CoreWebView2, file);
            await _thumbRepo.SaveAsync(url, file);
        }
        catch { }
    }

    private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        // Basic downloads UX: let WebView2 download UI run; you can manage e.ResultFilePath and attach handlers
        var op = e.DownloadOperation;
        op.BytesReceivedChanged += (s, a) =>
        {
            // Download progress tracking can be shown in notifications or downloads window
        };
        op.StateChanged += (s, a) =>
        {
            // Download state changes can be shown in notifications or downloads window
        };
        // For pause/resume control, expose UI later and call op.Pause()/op.Resume()
    }

    private async Task RestoreSessionAsync()
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", "Default");
            var sessionFile = Path.Combine(baseDir, "session.json");
            if (!File.Exists(sessionFile))
            {
                await CreateTab("https://www.bing.com");
                return;
            }
            var json = await File.ReadAllTextAsync(sessionFile);
            var session = JsonSerializer.Deserialize<Nona.Core.SessionState>(json) ?? new Nona.Core.SessionState();
            if (session.Tabs.Count == 0)
            {
                await CreateTab("https://www.bing.com");
                return;
            }
            foreach (var t in session.Tabs)
            {
                await CreateTab(t.Address);
            }
            if (session.ActiveIndex >= 0 && session.ActiveIndex < Tabs.Items.Count)
            {
                Tabs.SelectedIndex = session.ActiveIndex;
            }
        }
        catch
        {
            await CreateTab("https://www.bing.com");
        }
    }

    private async Task SaveSessionAsync()
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", "Default");
            Directory.CreateDirectory(baseDir);
            var sessionFile = Path.Combine(baseDir, "session.json");
            var session = new Nona.Core.SessionState
            {
                ActiveIndex = Tabs.SelectedIndex,
                Tabs = Tabs.Items.OfType<TabItem>()
                    .Select(it =>
                    {
                        var web = (it.Content as Grid)?.Children.OfType<WebView2>().FirstOrDefault();
                        var addr = web?.CoreWebView2?.Source ?? "about:blank";
                        return new Nona.Core.SessionTab { Address = addr };
                    })
                    .ToList()
            };
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(sessionFile, json);
        }
        catch { }
    }

    protected override async void OnClosed(EventArgs e)
    {
        await SaveSessionAsync();
        
        // Dispose all WebView2 instances to prevent resource leaks
        try
        {
            foreach (TabItem tab in Tabs.Items)
            {
                if (tab.Content is Grid grid && grid.Children.Count > 0)
                {
                    var webView = grid.Children[0] as WebView2;
                    if (webView != null)
                    {
                        try
                        {
                            if (webView.CoreWebView2 != null)
                            {
                                // Stop all media
                                await webView.CoreWebView2.ExecuteScriptAsync(@"
                                    document.querySelectorAll('audio, video').forEach(el => {
                                        el.pause();
                                        el.currentTime = 0;
                                        el.src = '';
                                        el.load();
                                    });
                                ");
                                webView.CoreWebView2.Navigate("about:blank");
                            }
                            webView.NavigationCompleted -= Web_NavigationCompleted;
                            webView.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error disposing WebView2 on app close: {ex.Message}");
                            try { webView.Dispose(); } catch { }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
        
        base.OnClosed(e);
    }



    // Command handlers for keyboard shortcuts
    private void NewTab_Executed(object sender, ExecutedRoutedEventArgs e) => NewTab_Click(sender, e);
    private void CloseTab_Executed(object sender, ExecutedRoutedEventArgs e) => CloseCurrentTab();
    private void FocusOmnibox_Executed(object sender, ExecutedRoutedEventArgs e) => Omnibox.Focus();
    private void OpenDownloads_Executed(object sender, ExecutedRoutedEventArgs e) => new DownloadsWindow().Show();
    private void OpenHistory_Executed(object sender, ExecutedRoutedEventArgs e) => OpenHistory_Click(sender, e);
    private void GoBack_Executed(object sender, ExecutedRoutedEventArgs e) => Back_Click(sender, e);
    private void OpenCommandPalette_Executed(object sender, ExecutedRoutedEventArgs e) => OpenCommandPaletteWindow();

    private async void CloseCurrentTab()
    {
        if (Tabs.SelectedItem is TabItem currentTab)
        {
            await CloseTab(currentTab);
        }
    }

    private void RegisterCommands()
    {
        // Navigation commands
        _commands.Register("new-tab", "New Tab", () => NewTab_Click(null!, null!));
        _commands.Register("close-tab", "Close Current Tab", CloseCurrentTab);
        _commands.Register("reload", "Reload Page", () => Reload_Click(null!, null!));
        _commands.Register("go-back", "Go Back", () => Back_Click(null!, null!));
        _commands.Register("go-forward", "Go Forward", () => Forward_Click(null!, null!));
        _commands.Register("go-home", "Go to Home Page", () => Home_Click(null!, null!));
        _commands.Register("focus-omnibox", "Focus Address Bar", () => Omnibox.Focus());
        
        // Window commands
        _commands.Register("settings", "Open Settings", () => OpenSettings_Click(null!, null!));
        _commands.Register("history", "Open History", () => OpenHistory_Click(null!, null!));
        _commands.Register("downloads", "Open Downloads", () => new DownloadsWindow().Show());
        _commands.Register("profiles", "Open Profiles", () => OpenProfiles_Click(null!, null!));
        _commands.Register("devtools", "Open Developer Tools", () => DevTools_Click(null!, null!));
        
        // Bookmark commands
        _commands.Register("add-bookmark", "Add Bookmark", () => AddBookmark_Click(null!, null!));
        _commands.Register("refresh-bookmarks", "Refresh Bookmark Bar", () => _ = LoadBookmarksBarAsync());
        
        // Theme commands
        _commands.Register("theme-dark", "Switch to Dark Theme", () => SwitchTheme("dark"));
        _commands.Register("theme-light", "Switch to Light Theme", () => SwitchTheme("light"));
        _commands.Register("theme-modern", "Switch to Modern Theme", () => SwitchTheme("modern"));
        
        // Utility commands
        _commands.Register("clear-history", "Clear Browsing History", ClearHistory);
        _commands.Register("zoom-in", "Zoom In", () => ZoomPage(1.1));
        _commands.Register("zoom-out", "Zoom Out", () => ZoomPage(0.9));
        _commands.Register("zoom-reset", "Reset Zoom", () => ZoomPage(1.0, true));
        _commands.Register("hard-refresh", "Hard Refresh (Ctrl+F5)", () => _ = HardRefreshAsync());
        _commands.Register("full-screen", "Toggle Full Screen", ToggleFullScreen);
        
        // Quick navigation
        _commands.Register("goto-google", "Go to Google", () => NavigateToUrl("https://www.google.com"));
        _commands.Register("goto-github", "Go to GitHub", () => NavigateToUrl("https://www.github.com"));
        _commands.Register("goto-stackoverflow", "Go to Stack Overflow", () => NavigateToUrl("https://stackoverflow.com"));
    }

    private void SwitchTheme(string themeName)
    {
        try
        {
            var themeService = ((App)Application.Current).Services.GetRequiredService<Nona.Theming.IThemeService>();
            var themePath = Path.Combine(AppContext.BaseDirectory, "Assets", "themes", $"{themeName}.json");
            if (File.Exists(themePath))
            {
                themeService.LoadFromFile(themePath);
                themeService.ApplyToResources(Application.Current.Resources);
            }
        }
        catch { }
    }

    private async void ClearHistory()
    {
        var result = MessageBox.Show("Are you sure you want to clear all browsing history?", 
            "Clear History", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                using var scope = ((App)Application.Current).Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Nona.Storage.NonaDbContext>();
                db.History.RemoveRange(db.History);
                await db.SaveChangesAsync();
                // History cleared successfully
            }
            catch (Exception ex)
            {
                // Error clearing history - could show notification
            }
        }
    }

    private void ZoomPage(double factor, bool reset = false)
    {
        if (_web?.CoreWebView2 == null) return;
        
        if (reset)
        {
            _web.ZoomFactor = 1.0;
        }
        else
        {
            _web.ZoomFactor = Math.Max(0.25, Math.Min(4.0, _web.ZoomFactor * factor));
        }
        // Zoom level changed - could show in title or notification
    }

    private void ToggleFullScreen()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        WindowStyle = WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None;
    }

    private void NavigateToUrl(string url)
    {
        if (_web?.CoreWebView2 == null) return;
        _web.CoreWebView2.Navigate(url);
        Omnibox.Text = url;
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void TabList_Click(object sender, RoutedEventArgs e)
    {
        UpdateTabList();
        TabListPopup.IsOpen = true;
    }

    private void UpdateTabList()
    {
        TabListContainer.Children.Clear();

        var tabItems = Tabs.Items.OfType<TabItem>().ToList();

        // Hiển thị tất cả tab, không giới hạn
        for (int i = 0; i < tabItems.Count; i++)
        {
            var tabItem = tabItems[i];
            var isSelected = tabItem == Tabs.SelectedItem;
            
            // Get tab info
            var title = tabItem.Header?.ToString() ?? "New Tab";
            var web = (tabItem.Content as Grid)?.Children.OfType<WebView2>().FirstOrDefault();
            var url = web?.CoreWebView2?.Source ?? "";
            
            // Create tab list item button
            var tabButton = new Button
            {
                Style = (Style)FindResource("TabListItemStyle"),
                Tag = tabItem,
                Margin = new Thickness(1),
                MinHeight = 50
            };

            // Create content with title and URL
            var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
            
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isSelected ? 
                    (Brush)FindResource("AccentColor") : 
                    (Brush)FindResource("ForegroundColor"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 220
            };
            
            var urlBlock = new TextBlock
            {
                Text = GetDisplayUrl(url),
                FontSize = 10,
                Opacity = 0.7,
                Foreground = (Brush)FindResource("ForegroundColor"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 220
            };

            stackPanel.Children.Add(titleBlock);
            if (!string.IsNullOrWhiteSpace(url))
            {
                stackPanel.Children.Add(urlBlock);
            }

            tabButton.Content = stackPanel;
            tabButton.Click += (s, e) =>
            {
                Tabs.SelectedItem = tabItem;
                TabListPopup.IsOpen = false;
            };

            TabListContainer.Children.Add(tabButton);

            // Add separator between tabs (except for last one)
            if (i < tabItems.Count - 1)
            {
                var separator = new Separator
                {
                    Style = (Style)FindResource("ModernSeparatorStyle"),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                TabListContainer.Children.Add(separator);
            }
        }

        // Scroll to selected tab if any
        if (Tabs.SelectedItem != null)
        {
            var selectedIndex = tabItems.IndexOf((TabItem)Tabs.SelectedItem);
            if (selectedIndex >= 0 && selectedIndex < TabListContainer.Children.Count)
            {
                // Scroll to show selected tab
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var selectedElement = TabListContainer.Children[selectedIndex * 2]; // *2 because of separators
                    if (selectedElement is FrameworkElement element)
                    {
                        element.BringIntoView();
                    }
                }));
            }
        }
    }

    private string GetDisplayUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url.Length > 30 ? url.Substring(0, 27) + "..." : url;
        }
    }
}