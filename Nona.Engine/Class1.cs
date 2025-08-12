using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Nona.Security;
using System.Drawing;
using System.Net.Http;
using System.Text.Json;

namespace Nona.Engine;

public interface IWebEngine
{
    Task<CoreWebView2Environment> GetEnvironmentAsync(string profileName = "Default");
    Task ConfigureWebViewAsync(CoreWebView2 core, IRulesEngine rulesEngine);
    Task CapturePreviewPngAsync(CoreWebView2 core, string filePath);
}

public sealed class WebEngine : IWebEngine
{
    private CoreWebView2Environment? _environment;

    public async Task<CoreWebView2Environment> GetEnvironmentAsync(string profileName = "Default")
    {
        if (_environment != null) return _environment;

        var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", profileName, "WebView2");
        Directory.CreateDirectory(userDataFolder);

        // Optimize WebView2 startup
        var options = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: "--disable-background-networking --disable-background-timer-throttling --disable-renderer-backgrounding --process-per-site")
        {
        };

        _environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder, options: options);
        return _environment;
    }

    public async Task ConfigureWebViewAsync(CoreWebView2 core, IRulesEngine rulesEngine)
    {
        // Leaner defaults for faster startup
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        core.Settings.IsPinchZoomEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;

        // Map virtual host for custom NTP assets
        var appDir = AppContext.BaseDirectory;
        var ntpAssets = Path.Combine(appDir, "Assets", "ntp");
        if (Directory.Exists(ntpAssets))
        {
            core.SetVirtualHostNameToFolderMapping("ntp.nona", ntpAssets, CoreWebView2HostResourceAccessKind.Allow);
        }

        // Basic request blocking hook
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (s, e) =>
        {
            try
            {
                var req = e.Request;
                if (req?.Uri is null) return;
                var uri = new Uri(req.Uri);
                // Allow non-http(s)
                if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    return;

                // Skip blocking for main documents to avoid breaking page load
                if (e.ResourceContext == CoreWebView2WebResourceContext.Document)
                    return;

                // Adblock disabled
                if (rulesEngine.Mode == Nona.Security.BlockingMode.Off)
                    return;

                var docHost = core?.Source is string src && Uri.TryCreate(src, UriKind.Absolute, out var doc) ? doc.Host : string.Empty;
                if (rulesEngine.IsBlocked(uri, docHost))
                {
                    var response = core?.Environment.CreateWebResourceResponse(Stream.Null, 403, "Blocked", "Content-Type: text/plain");
                    if (response != null) e.Response = response;
                }
            }
            catch { }
        };

        await Task.CompletedTask;
    }

    public async Task CapturePreviewPngAsync(CoreWebView2 core, string filePath)
    {
        await using var fs = File.Create(filePath);
        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
    }
}

public interface IDnsResolver
{
    Task<string?> ResolveAsync(string hostname, string provider = "cloudflare");
}

public sealed class DohResolver : IDnsResolver
{
    private static readonly HttpClient Http = new HttpClient
    {
        DefaultRequestHeaders = { { "Accept", "application/dns-json" }, { "User-Agent", "Nona/1.0" } }
    };

    public async Task<string?> ResolveAsync(string hostname, string provider = "cloudflare")
    {
        var endpoint = provider switch
        {
            "google" => "https://dns.google/resolve",
            "quad9" => "https://dns.quad9.net:5053/dns-query",
            _ => "https://cloudflare-dns.com/dns-query"
        };
        var url = $"{endpoint}?name={Uri.EscapeDataString(hostname)}&type=A";
        using var resp = await Http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        using var s = await resp.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(s);
        if (!doc.RootElement.TryGetProperty("Answer", out var answers)) return null;
        foreach (var ans in answers.EnumerateArray())
        {
            if (ans.TryGetProperty("data", out var data)) return data.GetString();
        }
        return null;
    }
}

public interface IDownloadsManager
{
    void Track(CoreWebView2 core);
}

    public sealed class DownloadsManager : IDownloadsManager
    {
        private readonly Nona.Storage.NonaDbContext _db;
        public DownloadsManager(Nona.Storage.NonaDbContext db) { _db = db; }

    public void Track(CoreWebView2 core)
    {
            core.DownloadStarting += (s, e) =>
        {
            var op = e.DownloadOperation;
                var entity = new Nona.Storage.DownloadItem { Url = op.Uri, FilePath = op.ResultFilePath, State = op.State.ToString(), BytesReceived = 0 };
                _ = _db.Downloads.Add(entity);
                _ = _db.SaveChangesAsync();
            op.BytesReceivedChanged += (ss, aa) =>
            {
                    entity.BytesReceived = (long)op.BytesReceived;
                    entity.TotalBytes = (long?)op.TotalBytesToReceive;
                    entity.State = op.State.ToString();
                    _ = _db.SaveChangesAsync();
            };
            op.StateChanged += (ss, aa) =>
            {
                    entity.State = op.State.ToString();
                    entity.FilePath = op.ResultFilePath;
                    _ = _db.SaveChangesAsync();
            };
        };
    }
}

public interface ICommandRegistry
{
    IReadOnlyList<(string id, string title, Action action)> List();
    void Register(string id, string title, Action action);
}

public sealed class CommandRegistry : ICommandRegistry
{
    private readonly List<(string id, string title, Action action)> _items = new();
    public IReadOnlyList<(string id, string title, Action action)> List() => _items;
    public void Register(string id, string title, Action action) => _items.Add((id, title, action));
}
