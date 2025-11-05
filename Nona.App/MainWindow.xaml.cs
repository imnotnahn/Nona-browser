using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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

    private BrowserControl? _web;
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
        
        // Setup Omnibox placeholder behavior
        SetupOmniboxPlaceholder();
    }

    private void SetupOmniboxPlaceholder()
    {
        var placeholderText = "Search or enter website name";
        Omnibox.Foreground = Brushes.Gray;
        
        Omnibox.GotFocus += (s, e) =>
        {
            if (Omnibox.Text == placeholderText)
            {
                Omnibox.Text = "";
                Omnibox.Foreground = (Brush)FindResource("ForegroundColor");
            }
        };
        
        Omnibox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(Omnibox.Text))
            {
                Omnibox.Text = placeholderText;
                Omnibox.Foreground = Brushes.Gray;
            }
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", "Default");
            var sessionFile = Path.Combine(baseDir, "session.json");
            if (File.Exists(sessionFile))
            {
                // Restore in the background without blocking UI
                _ = RestoreSessionAsync();
            }
            else
            {
                // Instant local NTP for fastest perceived startup
                await CreateTab("https://ntp.nona/index.html");
            }
        }
        catch
        {
            await CreateTab("https://ntp.nona/index.html");
        }
            // Load ad blocking rules (background to speed up startup)
            _ = Task.Run(async () =>
            {
                try
                {
                    var rulesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "rules");
                    if (System.IO.Directory.Exists(rulesPath))
                    {
                        var files = System.IO.Directory.GetFiles(rulesPath, "*.txt");
                        await _rules.LoadListsAsync(files);
                    }
                }
                catch { }
            });

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

        // Load bookmarks to bar (does minimal DB read)
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
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                // Decode at approximately control size to save memory/time
                if (BackgroundImage.ActualWidth > 0 && BackgroundImage.ActualHeight > 0)
                {
                    bitmap.DecodePixelWidth = (int)Math.Max(1, BackgroundImage.ActualWidth);
                    bitmap.DecodePixelHeight = (int)Math.Max(1, BackgroundImage.ActualHeight);
                }
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
        if (_web == null) return;
        var text = Omnibox.Text.Trim();
        
        // Ignore if placeholder text
        if (string.IsNullOrWhiteSpace(text) || text == "Search or enter website name") 
            return;
        
        Uri uri;
        
        // Smart URL detection
        if (IsUrl(text))
        {
            // Add https:// if no scheme
            if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                // If it's a Windows absolute path, navigate as file
                if (System.IO.Path.IsPathRooted(text) && System.IO.File.Exists(text))
                {
                    text = new Uri(text).ToString();
                }
                else
                {
                    text = "https://" + text;
                }
            }
            uri = new Uri(text);
        }
        else
        {
            // It's a search query - use search engine
            var searchEngine = await GetSearchEngineAsync();
            uri = new Uri(searchEngine + Uri.EscapeDataString(text));
        }
        
        // Don't upgrade non-http schemes like file://
        var upgraded = (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? (_httpsOnly.TryUpgrade(uri) ?? uri)
            : uri;
        _web.Navigate(upgraded.ToString());
    }

    private bool IsUrl(string text)
    {
        // Check if it looks like a URL:
        // - Contains a dot (domain.com)
        // - Doesn't contain spaces
        // - Starts with http:// or https://
        // - Has TLD pattern
        
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Allow file:// scheme and Windows absolute paths
        if (text.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return true;
        if (System.IO.Path.IsPathRooted(text) && System.IO.File.Exists(text))
            return true;

        // Check for domain pattern: xxx.xxx
        if (text.Contains('.') && !text.Contains(' '))
        {
            // Additional checks
            var parts = text.Split('.');
            if (parts.Length >= 2)
            {
                var lastPart = parts[^1];
                // Check if last part looks like TLD (2-6 chars, no numbers only)
                if (lastPart.Length >= 2 && lastPart.Length <= 6 && !int.TryParse(lastPart, out _))
                {
                    return true;
                }
            }
        }
        
        // Special cases
        if (text.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        
        return false;
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
            var web = _engine.CreateBrowserControl();
            
            // Configure browser with rules engine
            _engine.ConfigureBrowser(web, _rules);
            
            // Add event handlers
            web.NavigationCompleted += (s, e) =>
            {
                Web_NavigationCompleted(s, e);
                
                // Update tab title
                foreach (TabItem tabItem in Tabs.Items)
                {
                    if (tabItem.Content is Grid g && 
                        g.Children.Count > 0 && 
                        g.Children[0] == web)
                    {
                        var title = web.DocumentTitle;
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

            web.TitleChanged += (s, e) =>
            {
                // Find the tab that belongs to this browser
                foreach (TabItem tabItem in Tabs.Items)
                {
                    if (tabItem.Content is Grid g && 
                        g.Children.Count > 0 && 
                        g.Children[0] == web)
                    {
                        var title = web.DocumentTitle;
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

            grid.Children.Add(web);
            tab.Content = grid;
            Tabs.Items.Add(tab);
            Tabs.SelectedItem = tab;

            _web = web;
            
            // Set initial tab title
            SetTabHeader(tab, "New Tab");
            
            // Navigate to initial URL if provided
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                await web.NavigateAsync(initialUrl);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating tab: {ex.Message}");
            MessageBox.Show($"Failed to create new tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e)
    {
        await CreateTab("https://ntp.nona/index.html");
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is TabItem ti && ti.Content is Grid g && g.Children.Count > 0)
        {
            _web = g.Children[0] as BrowserControl;
            if (_web != null)
            {
                Omnibox.Text = _web.Source ?? string.Empty;
                Title = _web.DocumentTitle ?? "Nona";
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
        if (_web?.CanGoBack == true) _web.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_web?.CanGoForward == true) _web.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        _web?.Reload();
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (_web == null) return;
        _web.Navigate("https://ntp.nona/index.html");
    }

    private void Web_NavigationCompleted(object? sender, NavigationCompletedEventArgs e)
    {
        if (_web?.Source != null && e.IsSuccess)
        {
            Omnibox.Text = _web.Source;
            
            // Get title with fallback to URL domain
            var docTitle = _web.DocumentTitle;
            if (string.IsNullOrWhiteSpace(docTitle))
            {
                try
                {
                    var uri = new Uri(_web.Source);
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
            // Queue history write to reduce DB churn
            QueueHistory(_web.Source, _web.DocumentTitle);
        }
    }

    private void DevTools_Click(object sender, RoutedEventArgs e)
    {
        _web?.OpenDevTools();
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
        
        // Clean up browser control
        if (tab.Content is Grid grid && grid.Children.Count > 0)
        {
            var browser = grid.Children[0] as BrowserControl;
            if (browser != null)
            {
                try
                {
                    // Stop any ongoing navigation
                    browser.Stop();
                    
                    // Clear content to free memory
                    browser.Content = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing BrowserControl: {ex.Message}");
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
        
        await Task.CompletedTask;
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
                _web?.Navigate(b.Url);
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
        var title = _web?.DocumentTitle ?? "(No title)";
        var url = _web?.Source ?? string.Empty;
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
        if (_web == null) return;
        
        // For lightweight renderer, just reload the page
        _web.Reload();
        await Task.CompletedTask;
    }

    private async void HardRefresh_Executed(object? sender, ExecutedRoutedEventArgs e)
    {
        await HardRefreshAsync();
    }


    private async Task RestoreSessionAsync()
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", "Default");
            var sessionFile = Path.Combine(baseDir, "session.json");
            if (!File.Exists(sessionFile))
            {
                await CreateTab("https://ntp.nona/index.html");
                return;
            }
            var json = await File.ReadAllTextAsync(sessionFile);
            var session = JsonSerializer.Deserialize<Nona.Core.SessionState>(json) ?? new Nona.Core.SessionState();
            if (session.Tabs.Count == 0)
            {
                await CreateTab("https://ntp.nona/index.html");
                return;
            }
            var isFirst = true;
            foreach (var t in session.Tabs)
            {
                if (isFirst)
                {
                    await CreateTab(t.Address);
                    isFirst = false;
                }
                else
                {
                    _ = CreateTab(t.Address);
                    await Task.Delay(50);
                }
            }
            if (session.ActiveIndex >= 0 && session.ActiveIndex < Tabs.Items.Count)
            {
                Tabs.SelectedIndex = session.ActiveIndex;
            }
        }
        catch
        {
            await CreateTab("https://ntp.nona/index.html");
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
                        var web = (it.Content as Grid)?.Children.OfType<BrowserControl>().FirstOrDefault();
                        var addr = web?.Source ?? "about:blank";
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
        
        // Clean up all browser controls
        try
        {
            foreach (TabItem tab in Tabs.Items)
            {
                if (tab.Content is Grid grid && grid.Children.Count > 0)
                {
                    var browser = grid.Children[0] as BrowserControl;
                    if (browser != null)
                    {
                        try
                        {
                            browser.Stop();
                            browser.Content = null;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error disposing BrowserControl on app close: {ex.Message}");
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

    // Simple in-memory queue for batching history writes
    private static readonly object _historyLock = new();
    private static readonly Queue<(string url, string? title)> _historyQueue = new();
    private static bool _historyFlusherStarted = false;
    private void QueueHistory(string url, string? title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        lock (_historyLock)
        {
            _historyQueue.Enqueue((url, title));
            if (!_historyFlusherStarted)
            {
                _historyFlusherStarted = true;
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        (string url, string? title)[] batch;
                        lock (_historyLock)
                        {
                            if (_historyQueue.Count == 0)
                            {
                                _historyFlusherStarted = false;
                                break;
                            }
                            batch = _historyQueue.ToArray();
                            _historyQueue.Clear();
                        }
                        try
                        {
                            await _history.AddBatchAsync(batch);
                        }
                        catch { }
                        await Task.Delay(150);
                    }
                });
            }
        }
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
                var factory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Nona.Storage.NonaDbContext>>();
                await using var db = factory.CreateDbContext();
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
        // Zoom not supported in lightweight renderer
        MessageBox.Show("Zoom feature is not available in the lightweight rendering engine.", 
            "Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ToggleFullScreen()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        WindowStyle = WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None;
    }

    private void NavigateToUrl(string url)
    {
        if (_web == null) return;
        _web.Navigate(url);
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
            var web = (tabItem.Content as Grid)?.Children.OfType<BrowserControl>().FirstOrDefault();
            var url = web?.Source ?? "";
            
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