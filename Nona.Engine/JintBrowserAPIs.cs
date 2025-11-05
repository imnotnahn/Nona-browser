using System;
using System.Collections.Generic;

namespace Nona.Engine;

/// <summary>
/// Navigator API - Browser information
/// </summary>
public class JintNavigator
{
    public string userAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Nona/1.0";
    public string appName { get; set; } = "Netscape";
    public string appVersion { get; set; } = "5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
    public string appCodeName { get; set; } = "Mozilla";
    public string platform { get; set; } = "Win32";
    public string product { get; set; } = "Gecko";
    public string productSub { get; set; } = "20030107";
    public string vendor { get; set; } = "Google Inc.";
    public string vendorSub { get; set; } = "";
    public string language { get; set; } = "en-US";
    public string[] languages { get; set; } = new[] { "en-US", "en" };
    public bool onLine { get; set; } = true;
    public bool cookieEnabled { get; set; } = true;
    public bool doNotTrack { get; set; } = false;
    public int maxTouchPoints { get; set; } = 0;
    public int hardwareConcurrency { get; set; } = 8;
    public long deviceMemory { get; set; } = 8;
    
    // Anti-headless detection
    public bool webdriver { get; set; } = false;
    
    // Methods
    public bool javaEnabled() => false;
    public bool taintEnabled() => false;
    
    public object[] plugins => new object[] {
        new { name = "Chrome PDF Plugin", filename = "internal-pdf-viewer", description = "Portable Document Format", length = 2 },
        new { name = "Chrome PDF Viewer", filename = "mhjfbmdgcfjbbpaeojofohoefgiehjai", description = "Portable Document Format", length = 2 },
        new { name = "Native Client", filename = "internal-nacl-plugin", description = "", length = 2 }
    };
    
    public object[] mimeTypes => new object[] {
        new { type = "application/pdf", suffixes = "pdf", description = "Portable Document Format", enabledPlugin = plugins[0] },
        new { type = "text/pdf", suffixes = "pdf", description = "Portable Document Format", enabledPlugin = plugins[0] }
    };
    
    // Geolocation API stub
    public object geolocation => new JintGeolocation();
    
    // ServiceWorker stub
    public object? serviceWorker => null;
    
    // MediaDevices stub
    public object mediaDevices => new JintMediaDevices();
    
    // Permissions stub
    public object permissions => new JintPermissions();
    
    // Clipboard stub
    public object clipboard => new JintClipboard();
    
    // Battery stub
    public object? getBattery() => null;
    
    // User Agent Data
    public object userAgentData => new
    {
        brands = new[] {
            new { brand = "Chromium", version = "120" },
            new { brand = "Nona", version = "1" }
        },
        mobile = false,
        platform = "Windows"
    };
}

/// <summary>
/// Location API - URL information and navigation
/// </summary>
public class JintLocation
{
    public string href { get; set; } = "";
    public string protocol { get; set; } = "https:";
    public string host { get; set; } = "";
    public string hostname { get; set; } = "";
    public string port { get; set; } = "";
    public string pathname { get; set; } = "/";
    public string search { get; set; } = "";
    public string hash { get; set; } = "";
    public string origin { get; set; } = "";
    
    public Action<string>? OnNavigate { get; set; }
    
    public void assign(string url)
    {
        href = url;
        OnNavigate?.Invoke(url);
    }
    
    public void replace(string url)
    {
        href = url;
        OnNavigate?.Invoke(url);
    }
    
    public void reload(bool forceReload = false)
    {
        OnNavigate?.Invoke(href);
    }
    
    public override string ToString() => href;
}

/// <summary>
/// Screen API - Screen information
/// </summary>
public class JintScreen
{
    public int width { get; set; } = 1920;
    public int height { get; set; } = 1080;
    public int availWidth { get; set; } = 1920;
    public int availHeight { get; set; } = 1040;
    public int colorDepth { get; set; } = 24;
    public int pixelDepth { get; set; } = 24;
    public string orientation => "landscape-primary";
}

/// <summary>
/// History API - Browser history
/// </summary>
public class JintHistory
{
    private readonly List<string> _history = new();
    private int _currentIndex = 0;
    
    public int length => _history.Count;
    public string state => "";
    public int scrollRestoration => 0;
    
    public Action? OnBack { get; set; }
    public Action? OnForward { get; set; }
    
    public void back()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            OnBack?.Invoke();
        }
    }
    
    public void forward()
    {
        if (_currentIndex < _history.Count - 1)
        {
            _currentIndex++;
            OnForward?.Invoke();
        }
    }
    
    public void go(int delta)
    {
        var newIndex = _currentIndex + delta;
        if (newIndex >= 0 && newIndex < _history.Count)
        {
            _currentIndex = newIndex;
            if (delta < 0)
                OnBack?.Invoke();
            else if (delta > 0)
                OnForward?.Invoke();
        }
    }
    
    public void pushState(object state, string title, string? url = null)
    {
        // Simplified
        if (url != null)
            _history.Add(url);
    }
    
    public void replaceState(object state, string title, string? url = null)
    {
        // Simplified
    }
}

/// <summary>
/// LocalStorage API
/// </summary>
public class JintStorage
{
    private readonly Dictionary<string, string> _storage = new();
    
    public int length => _storage.Count;
    
    public string? getItem(string key)
    {
        return _storage.TryGetValue(key, out var value) ? value : null;
    }
    
    public void setItem(string key, string value)
    {
        _storage[key] = value;
    }
    
    public void removeItem(string key)
    {
        _storage.Remove(key);
    }
    
    public void clear()
    {
        _storage.Clear();
    }
    
    public string? key(int index)
    {
        if (index >= 0 && index < _storage.Count)
            return _storage.Keys.ToArray()[index];
        return null;
    }
}

/// <summary>
/// Performance API stub
/// </summary>
public class JintPerformance
{
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    
    public object timing => new
    {
        navigationStart = _startTime.ToUnixTimeMilliseconds(),
        domLoading = _startTime.ToUnixTimeMilliseconds() + 50,
        domInteractive = _startTime.ToUnixTimeMilliseconds() + 100,
        domComplete = _startTime.ToUnixTimeMilliseconds() + 200,
        loadEventEnd = _startTime.ToUnixTimeMilliseconds() + 250
    };
    
    public object navigation => new { type = 0, redirectCount = 0 };
    
    public double now()
    {
        return (DateTimeOffset.UtcNow - _startTime).TotalMilliseconds;
    }
    
    public void mark(string name) { }
    public void measure(string name, string? startMark = null, string? endMark = null) { }
    public void clearMarks(string? name = null) { }
    public void clearMeasures(string? name = null) { }
}

/// <summary>
/// XMLHttpRequest API stub (simplified)
/// </summary>
public class JintXMLHttpRequest
{
    private readonly System.Net.Http.HttpClient _httpClient = new();
    
    public int readyState { get; private set; } = 0;
    public int status { get; private set; } = 0;
    public string statusText { get; private set; } = "";
    public string responseText { get; private set; } = "";
    public string response => responseText;
    public string responseType { get; set; } = "";
    public int timeout { get; set; } = 0;
    
    public object? onreadystatechange { get; set; }
    public object? onload { get; set; }
    public object? onerror { get; set; }
    public object? ontimeout { get; set; }
    
    public void open(string method, string url, bool async = true)
    {
        readyState = 1;
    }
    
    public void send(string? data = null)
    {
        readyState = 2;
        // Would need async HTTP request here
        // For now, just simulate completion
        Task.Run(async () =>
        {
            try
            {
                readyState = 3;
                // Simulate network request
                await Task.Delay(100);
                
                readyState = 4;
                status = 200;
                statusText = "OK";
                responseText = "{}";
                
                // Trigger onload
            }
            catch
            {
                // Trigger onerror
            }
        });
    }
    
    public void setRequestHeader(string header, string value) { }
    public string getResponseHeader(string header) => "";
    public string getAllResponseHeaders() => "";
    public void abort() { }
}

/// <summary>
/// Fetch API stub
/// </summary>
public class JintFetch
{
    public static object fetch(string url, object? options = null)
    {
        // Return a Promise that resolves to Response
        return new
        {
            then = new Func<Action<object>, object>(callback =>
            {
                // Simulate async response
                Task.Run(async () =>
                {
                    await Task.Delay(100);
                    var response = new JintResponse
                    {
                        status = 200,
                        ok = true,
                        url = url
                    };
                    callback(response);
                });
                return null;
            })
        };
    }
}

public class JintResponse
{
    public int status { get; set; }
    public bool ok { get; set; }
    public string url { get; set; } = "";
    public string statusText { get; set; } = "OK";
    
    public object json()
    {
        // Return Promise
        return new { then = new Func<Action<object>, object>(callback =>
        {
            callback(new { });
            return null;
        })};
    }
    
    public object text()
    {
        return new { then = new Func<Action<object>, object>(callback =>
        {
            callback("");
            return null;
        })};
    }
}

/// <summary>
/// Image constructor
/// </summary>
public class JintImage
{
    public string src { get; set; } = "";
    public int width { get; set; }
    public int height { get; set; }
    public string alt { get; set; } = "";
    public bool complete { get; set; } = false;
    public int naturalWidth => width;
    public int naturalHeight => height;
    
    public object? onload { get; set; }
    public object? onerror { get; set; }
}

// Stub implementations for other APIs

public class JintGeolocation
{
    public void getCurrentPosition(object success, object? error = null, object? options = null)
    {
        // Stub - would invoke error callback
    }
    
    public int watchPosition(object success, object? error = null, object? options = null)
    {
        return 0;
    }
    
    public void clearWatch(int watchId) { }
}

public class JintMediaDevices
{
    public object getUserMedia(object constraints)
    {
        // Return rejected Promise
        return new { then = new Func<Action<object>, Action<object>, object>((success, error) =>
        {
            error?.Invoke(new Exception("Not supported"));
            return null;
        })};
    }
}

public class JintPermissions
{
    public object query(object permissionDesc)
    {
        // Return Promise resolving to PermissionStatus
        return new { then = new Func<Action<object>, object>(callback =>
        {
            callback(new { state = "denied" });
            return null;
        })};
    }
}

public class JintClipboard
{
    public object writeText(string text)
    {
        // Return resolved Promise
        return new { then = new Func<Action<object>, object>(callback =>
        {
            callback(null);
            return null;
        })};
    }
    
    public object readText()
    {
        return new { then = new Func<Action<object>, object>(callback =>
        {
            callback("");
            return null;
        })};
    }
}

