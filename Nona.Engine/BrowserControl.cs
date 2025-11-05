using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nona.Security;

namespace Nona.Engine;

/// <summary>
/// Custom browser control that replaces WebView2
/// Lightweight and optimized for performance
/// </summary>
public class BrowserControl : UserControl
{
    private readonly HtmlParser _parser;
    private readonly SimpleRenderer _renderer;
    private readonly IHttpEngine _httpEngine;
    private IRulesEngine? _rulesEngine;
    private CancellationTokenSource? _navigationCts;
    private JintWebEngine? _jsEngine;
    private readonly List<string> _networkLog = new();

    private string _currentUrl = "";
    private string _documentTitle = "New Tab";
    private ScrollViewer? _contentViewer;
    private readonly Border _loadingBorder;
    private readonly TextBlock _loadingText;

    public event EventHandler? NavigationStarting;
    public event EventHandler<NavigationCompletedEventArgs>? NavigationCompleted;
    public event EventHandler? TitleChanged;

    public bool EnableJavaScript { get; set; } = true;

    public string Source
    {
        get => _currentUrl;
        set => Navigate(value);
    }

    public string DocumentTitle
    {
        get => _documentTitle;
        private set
        {
            if (_documentTitle != value)
            {
                _documentTitle = value;
                TitleChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool CanGoBack { get; private set; }
    public bool CanGoForward { get; private set; }

    private readonly System.Collections.Generic.Stack<string> _backStack = new(10); // Initial capacity for performance
    private readonly System.Collections.Generic.Stack<string> _forwardStack = new(10);

    public BrowserControl()
    {
        _parser = new HtmlParser();
        _renderer = new SimpleRenderer();
        _httpEngine = new HttpEngine();

        // Set up navigation handler
        _renderer.NavigationRequested += OnNavigationRequested;
        _renderer.FormSubmitted += OnFormSubmitted;

        // Loading indicator
        _loadingBorder = new Border
        {
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };

        _loadingText = new TextBlock
        {
            Text = "Loading...",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray
        };

        _loadingBorder.Child = _loadingText;

        var grid = new Grid();
        grid.Children.Add(_loadingBorder);

        Content = grid;
        Background = Brushes.White;
        
        // Cleanup on unload
        Unloaded += BrowserControl_Unloaded;
    }

    public void ConfigureRulesEngine(IRulesEngine rulesEngine)
    {
        _rulesEngine = rulesEngine;
    }

    public void Navigate(string url)
    {
        _ = NavigateAsync(url);
    }

    public async Task NavigateAsync(string url)
    {
        // Cancel any pending navigation (with disposal safety)
        try
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - safe to ignore
        }
        
        _navigationCts = new CancellationTokenSource();
        var cancellationToken = _navigationCts.Token;

        try
        {
            // Add current URL to back stack if navigating to a new page
            if (!string.IsNullOrEmpty(_currentUrl) && _currentUrl != url)
            {
                _backStack.Push(_currentUrl);
                _forwardStack.Clear(); // Clear forward stack on new navigation
                CanGoBack = true;
                CanGoForward = false;
            }

            _currentUrl = url;
            NavigationStarting?.Invoke(this, EventArgs.Empty);

            // Show loading indicator
            ShowLoading(true, $"Loading {url}...");

            // Fetch page content
            var response = await _httpEngine.FetchAsync(url, _currentUrl, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            if (!response.IsSuccess)
            {
                ShowError(response.ErrorMessage);
                NavigationCompleted?.Invoke(this, new NavigationCompletedEventArgs
                {
                    IsSuccess = false,
                    Url = url,
                    ErrorMessage = response.ErrorMessage
                });
                return;
            }

            // Parse HTML
            ShowLoading(true, "Rendering page...");
            var document = _parser.Parse(response.Content);
            document.UpdateTitle();
            DocumentTitle = document.Title.Length > 0 ? document.Title : GetUrlHost(url);
            _lastDocument = document; // Save for DevTools
            
            // Parse URL for location object
            var uri = new Uri(url);

            // Initialize JavaScript engine if enabled
            if (EnableJavaScript)
            {
                try
                {
                    // === STEP 1: CLEANUP ===
                    _networkLog.Add($"[NAV] === NAVIGATING TO: {url} ===");
                    
                    if (_jsEngine != null)
                    {
                        _networkLog.Add("[CLEANUP] Disposing old V8 engine...");
                        try
                        {
                            _jsEngine.Dispose();
                            _jsEngine = null;
                        }
                        catch (Exception disposeEx)
                        {
                            _networkLog.Add($"[WARNING] Disposal error: {disposeEx.Message}");
                        }
                    }
                    
                    // === STEP 2: CREATE ENGINE ===
                    _networkLog.Add("[INIT] Creating Jint engine...");
                    _jsEngine = new JintWebEngine();
                    _networkLog.Add("[INIT] Jint engine created successfully");
                    
                    // === STEP 3: PREPARE HOST OBJECTS ===
                    _networkLog.Add("[INIT] Preparing browser environment objects...");
                    
                    var jintDoc = new JintDocument(document);
                    jintDoc.URL = url;
                    jintDoc.domain = uri.Host;
                    
                    var navigator = new JintNavigator();
                    
                    var location = new JintLocation
                    {
                        href = url,
                        protocol = uri.Scheme + ":",
                        host = uri.Authority,
                        hostname = uri.Host,
                        port = uri.Port.ToString(),
                        pathname = uri.AbsolutePath,
                        search = uri.Query,
                        hash = uri.Fragment,
                        origin = $"{uri.Scheme}://{uri.Authority}"
                    };
                    location.OnNavigate = newUrl => { _ = NavigateAsync(newUrl); };
                    
                    var screen = new JintScreen();
                    var history = new JintHistory();
                    history.OnBack = () => GoBack();
                    history.OnForward = () => GoForward();
                    
                    var localStorage = new JintStorage();
                    var sessionStorage = new JintStorage();
                    var performance = new JintPerformance();
                    
                    // === STEP 4: ADD HOST OBJECTS ===
                    _networkLog.Add("[INIT] Adding host objects to Jint global scope...");
                    
                    _jsEngine.SetValue("document", jintDoc);
                    _networkLog.Add("[INIT] ✓ document");
                    
                    _jsEngine.SetValue("navigator", navigator);
                    _networkLog.Add("[INIT] ✓ navigator");
                    
                    _jsEngine.SetValue("location", location);
                    _networkLog.Add("[INIT] ✓ location");
                    
                    _jsEngine.SetValue("screen", screen);
                    _jsEngine.SetValue("history", history);
                    _jsEngine.SetValue("localStorage", localStorage);
                    _jsEngine.SetValue("sessionStorage", sessionStorage);
                    _jsEngine.SetValue("performance", performance);
                    
                    // === STEP 5: SETUP WINDOW OBJECT ===
                    _networkLog.Add("[INIT] Executing window setup script...");
                    
                    try
                    {
                        _jsEngine.Execute(@"
                        // Setup window as global object
                        var window = this;
                        
                        // Re-assign globals to window for compatibility
                        window.document = document;
                        window.navigator = navigator;
                        window.location = location;
                        window.screen = screen;
                        window.history = history;
                        window.localStorage = localStorage;
                        window.sessionStorage = sessionStorage;
                        window.performance = performance;
                        
                        // Window dimensions
                        window.innerWidth = 1920;
                        window.innerHeight = 1080;
                        window.outerWidth = 1920;
                        window.outerHeight = 1080;
                        
                        window.devicePixelRatio = 1;
                        
                        // Google-specific objects
                        window.google = {};
                        window.gbar = {};
                        
                        // Frame references
                        window.self = window;
                        window.top = window;
                        window.parent = window;
                        window.frames = window;
                        
                        // Also set global aliases
                        var self = window;
                        var top = window;
                        var parent = window;
                        
                        // XMLHttpRequest constructor
                        var XMLHttpRequest = function() {
                            this.readyState = 0;
                            this.status = 0;
                            this.responseText = '';
                            this.open = function() {};
                            this.send = function() {};
                            this.setRequestHeader = function() {};
                            this.getAllResponseHeaders = function() { return ''; };
                            this.getResponseHeader = function() { return null; };
                        };
                        window.XMLHttpRequest = XMLHttpRequest;
                        
                        // Image constructor
                        var Image = function(width, height) {
                            var img = document.createElement('img');
                            if (width !== undefined) img.width = width;
                            if (height !== undefined) img.height = height;
                            return img;
                        };
                        window.Image = Image;
                        
                        // Option constructor
                        var Option = function(text, value, defaultSelected, selected) {
                            var option = document.createElement('option');
                            if (text !== undefined) option.text = text;
                            if (value !== undefined) option.value = value;
                            return option;
                        };
                        window.Option = Option;
                        
                        // HTMLElement, Element, Node base constructors
                        window.Node = function() {};
                        window.Element = function() {};
                        window.HTMLElement = function() {};
                        window.HTMLDivElement = function() {};
                        window.HTMLSpanElement = function() {};
                        window.HTMLInputElement = function() {};
                        window.HTMLFormElement = function() {};
                        window.HTMLAnchorElement = function() {};
                        window.HTMLButtonElement = function() {};
                        window.HTMLImageElement = function() {};
                        window.HTMLScriptElement = function() {};
                        window.Text = function() {};
                        window.Comment = function() {};
                        window.Document = function() {};
                        window.DocumentFragment = function() {};
                        
                        // Add classList if not present
                        if (document.body && !document.body.classList) {
                            document.body.classList = {
                                add: function() {},
                                remove: function() {},
                                contains: function() { return false; },
                                toggle: function() {}
                            };
                        }
                        
                        // Add focus/blur methods
                        window.focus = function() {};
                        window.blur = function() {};
                        window.close = function() {};
                        window.open = function() { return window; };
                        window.print = function() {};
                        window.stop = function() {};
                        
                        // Add scroll methods
                        window.scroll = function() {};
                        window.scrollTo = function() {};
                        window.scrollBy = function() {};
                        
                        // Add scroll properties
                        window.scrollX = 0;
                        window.scrollY = 0;
                        window.pageXOffset = 0;
                        window.pageYOffset = 0;
                        
                        // Add screen properties
                        window.screenX = 0;
                        window.screenY = 0;
                        window.screenLeft = 0;
                        window.screenTop = 0;
                        
                        // Crypto API stub
                        window.crypto = {
                            getRandomValues: function(arr) {
                                for (var i = 0; i < arr.length; i++) {
                                    arr[i] = Math.floor(Math.random() * 256);
                                }
                                return arr;
                            }
                        };
                        
                        // Add fetch stub
                        window.fetch = function(url, options) {
                            return new Promise(function(resolve, reject) {
                                reject(new Error('Fetch not implemented'));
                            });
                        };
                        
                        // Add Event constructor
                        window.Event = function(type, options) {
                            this.type = type;
                            this.bubbles = options && options.bubbles || false;
                            this.cancelable = options && options.cancelable || false;
                        };
                        
                        // Add more global properties
                        window.closed = false;
                        window.name = '';
                        window.status = '';
                        window.length = 0;
                        
                        // Ensure console is accessible
                        if (typeof console === 'undefined') {
                            window.console = {
                                log: function() {},
                                warn: function() {},
                                error: function() {},
                                info: function() {},
                                debug: function() {}
                            };
                        }
                        ");
                        
                        _networkLog.Add("[INIT] ✓ Window setup completed successfully");
                    }
                    catch (Jint.Runtime.JavaScriptException scriptEx)
                    {
                        _networkLog.Add($"[ERROR] Window setup script failed: {scriptEx.Message}");
                        throw;
                    }
                    
                    // === STEP 6: VERIFY ENVIRONMENT ===
                    _networkLog.Add("[INIT] Verifying environment...");
                    try
                    {
                        var result = _jsEngine.Evaluate("typeof document") ?? "undefined";
                        _networkLog.Add($"[DEBUG] typeof document = {result}");
                        
                        result = _jsEngine.Evaluate("typeof navigator") ?? "undefined";
                        _networkLog.Add($"[DEBUG] typeof navigator = {result}");
                        
                        result = _jsEngine.Evaluate("typeof window") ?? "undefined";
                        _networkLog.Add($"[DEBUG] typeof window = {result}");
                        
                        result = _jsEngine.Evaluate("typeof Image") ?? "undefined";
                        _networkLog.Add($"[DEBUG] typeof Image = {result}");
                        
                        _networkLog.Add("[INIT] ✓✓✓ Environment ready");
                        _jsEngine.Execute("console.log('✓ Jint JavaScript engine fully initialized')");
                        
                        // Trigger DOMContentLoaded after setup
                        try
                        {
                            _jsEngine.Execute(@"
                                if (typeof document !== 'undefined' && document.addEventListener) {
                                    // Simulate DOMContentLoaded
                                    setTimeout(function() {
                                        var event = new Event('DOMContentLoaded', { bubbles: true, cancelable: true });
                                        if (document.dispatchEvent) {
                                            document.dispatchEvent(event);
                                        }
                                    }, 10);
                                    
                                    // Simulate load event
                                    setTimeout(function() {
                                        var event = new Event('load', { bubbles: false, cancelable: false });
                                        if (window.dispatchEvent) {
                                            window.dispatchEvent(event);
                                        }
                                    }, 50);
                                }
                            ");
                            _networkLog.Add("[INIT] ✓ DOMContentLoaded event scheduled");
                        }
                        catch (Exception eventEx)
                        {
                            _networkLog.Add($"[WARNING] Event trigger error: {eventEx.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _networkLog.Add($"[WARNING] Environment check error: {ex.Message}");
                    }
                    
                    // === STEP 7: EXECUTE PAGE SCRIPTS ===
                    _networkLog.Add("[SCRIPTS] Extracting inline scripts from HTML...");
                    var scripts = ExtractScripts(response.Content);
                    _networkLog.Add($"[SCRIPTS] Found {scripts.Count} script blocks");
                    
                    int scriptIndex = 0;
                    foreach (var script in scripts)
                    {
                        if (!string.IsNullOrWhiteSpace(script))
                        {
                            scriptIndex++;
                            try
                            {
                                _networkLog.Add($"[SCRIPTS] Executing script block {scriptIndex}...");
                                _jsEngine.Execute(script);
                                _networkLog.Add($"[SCRIPTS] ✓ Script {scriptIndex} completed");
                            }
                            catch (Exception ex)
                            {
                                // Log but continue - some scripts may fail gracefully
                                var errorMsg = ex.Message;
                                System.Diagnostics.Debug.WriteLine($"Script {scriptIndex} error: {errorMsg}");
                                _networkLog.Add($"[SCRIPTS] ✗ Script {scriptIndex} ERROR: {errorMsg}");
                            }
                        }
                    }
                    
                    _networkLog.Add($"[SCRIPTS] Script execution completed ({scriptIndex} blocks processed)");
                }
                catch (Exception ex)
                {
                    // V8 initialization failed - log and disable JS for this page
                    var errorMsg = $"Failed to initialize V8 engine: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    _networkLog.Add($"[CRITICAL] {errorMsg}");
                    
                    // Show error in UI
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            $"JavaScript engine initialization failed:\n\n{ex.Message}\n\n" +
                            $"Please ensure V8 native libraries are installed.\n" +
                            $"Page will load without JavaScript support.",
                            "V8 Engine Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    });
                }
            }

            // Clear old content to free memory
            if (_contentViewer != null)
            {
                _contentViewer.Content = null;
                _contentViewer = null;
            }

            // Render document
            _contentViewer = _renderer.Render(document);
            
            // Log network request
            _networkLog.Add($"[{DateTime.Now:HH:mm:ss}] GET {url} - {response.StatusCode}");
            
            // Update UI on UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                var grid = (Grid)Content;
                grid.Children.Clear();
                grid.Children.Add(_contentViewer);
                grid.Children.Add(_loadingBorder);
                ShowLoading(false);
            });

            NavigationCompleted?.Invoke(this, new NavigationCompletedEventArgs
            {
                IsSuccess = true,
                Url = response.FinalUrl
            });
        }
        catch (OperationCanceledException)
        {
            // Navigation was cancelled
        }
        catch (Exception ex)
        {
            ShowError($"Error loading page: {ex.Message}");
            NavigationCompleted?.Invoke(this, new NavigationCompletedEventArgs
            {
                IsSuccess = false,
                Url = url,
                ErrorMessage = ex.Message
            });
        }
    }

    public void GoBack()
    {
        if (_backStack.Count > 0)
        {
            _forwardStack.Push(_currentUrl);
            var url = _backStack.Pop();
            CanGoBack = _backStack.Count > 0;
            CanGoForward = true;
            Navigate(url);
        }
    }

    public void GoForward()
    {
        if (_forwardStack.Count > 0)
        {
            _backStack.Push(_currentUrl);
            var url = _forwardStack.Pop();
            CanGoForward = _forwardStack.Count > 0;
            CanGoBack = true;
            Navigate(url);
        }
    }

    public void Reload()
    {
        if (!string.IsNullOrEmpty(_currentUrl))
        {
            Navigate(_currentUrl);
        }
    }

    public void Stop()
    {
        _navigationCts?.Cancel();
        ShowLoading(false);
    }

    private void OnNavigationRequested(object? sender, string url)
    {
        // Handle relative URLs
        var absoluteUrl = GetAbsoluteUrl(url);
        Navigate(absoluteUrl);
    }

    private void OnFormSubmitted(object? sender, string formName)
    {
        // Handle form submission
        // For now, just log it
        System.Diagnostics.Debug.WriteLine($"Form submitted: {formName}");
    }

    private string GetAbsoluteUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        // Try to make relative URL absolute
        if (Uri.TryCreate(_currentUrl, UriKind.Absolute, out var baseUri))
        {
            if (Uri.TryCreate(baseUri, url, out var absoluteUri))
                return absoluteUri.ToString();
        }

        return url;
    }

    private string GetUrlHost(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }

    private void ShowLoading(bool show, string? message = null)
    {
        Dispatcher.Invoke(() =>
        {
            _loadingBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (message != null)
            {
                _loadingText.Text = message;
            }
        });
    }

    private void ShowError(string errorMessage)
    {
        Dispatcher.Invoke(() =>
        {
            var errorPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            errorPanel.Children.Add(new TextBlock
            {
                Text = "Failed to load page",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            errorPanel.Children.Add(new TextBlock
            {
                Text = errorMessage,
                FontSize = 14,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var grid = (Grid)Content;
            grid.Children.Clear();
            grid.Children.Add(errorPanel);
            grid.Children.Add(_loadingBorder);
            ShowLoading(false);
        });
    }

    private void BrowserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        // Cancel any ongoing navigation (with disposal safety)
        try
        {
            _navigationCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - safe to ignore
        }
        
        try
        {
            _navigationCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - safe to ignore
        }
        
        // Dispose JavaScript engine
        try
        {
            _jsEngine?.Dispose();
        }
        catch (Exception)
        {
            // Ignore disposal errors
        }
    }

    private List<string> ExtractScripts(string html)
    {
        var scripts = new List<string>();
        var scriptTagPattern = @"<script[^>]*>(.*?)</script>";
        var matches = System.Text.RegularExpressions.Regex.Matches(html, scriptTagPattern, 
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                scripts.Add(match.Groups[1].Value);
            }
        }
        
        return scripts;
    }

    private HtmlDocument? _lastDocument;

    public void OpenDevTools()
    {
        var doc = _lastDocument ?? new HtmlDocument();
        
        if (_jsEngine == null)
        {
            _jsEngine = new JintWebEngine();
            var jintDoc = new JintDocument(doc);
            _jsEngine.SetValue("document", jintDoc);
        }

        var devTools = new JintDevToolsWindow(doc, _jsEngine, _networkLog);
        devTools.Show();
    }
}

public class NavigationCompletedEventArgs : EventArgs
{
    public bool IsSuccess { get; set; }
    public string Url { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

// Host objects for JavaScript environment
public class NavigatorObject
{
    public string userAgent { get; set; } = "";
    public string appName { get; set; } = "";
    public string appVersion { get; set; } = "";
    public string platform { get; set; } = "";
    public string language { get; set; } = "";
    public bool onLine { get; set; } = true;
    
    // Critical for Google: javaEnabled() must return true
    public bool javaEnabled() => true;
}

public class LocationObject
{
    public string href { get; set; } = "";
    public string protocol { get; set; } = "https:";
    public string host { get; set; } = "";
    public string hostname { get; set; } = "";
    public string pathname { get; set; } = "";
}

public class WindowObject
{
    public NavigatorObject navigator { get; set; } = new();
    public LocationObject location { get; set; } = new();
    public int innerWidth { get; set; } = 1920;
    public int innerHeight { get; set; } = 1080;
    public int outerWidth { get; set; } = 1920;
    public int outerHeight { get; set; } = 1080;
}

