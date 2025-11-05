using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Nona.Engine;

/// <summary>
/// Lightweight HTTP engine for fetching web content
/// </summary>
public interface IHttpEngine
{
    Task<PageResponse> FetchAsync(string url, CancellationToken cancellationToken = default);
    Task<PageResponse> FetchAsync(string url, string? referer, CancellationToken cancellationToken = default);
}

public class PageResponse
{
    public string Url { get; set; } = "";
    public string FinalUrl { get; set; } = "";
    public string Content { get; set; } = "";
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
}

public class HttpEngine : IHttpEngine
{
    private static readonly System.Net.CookieContainer _cookies = new();
    private static readonly HttpClientHandler _handler = new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseCookies = true,
        CookieContainer = _cookies
    };

    private static readonly HttpClient _httpClient = new HttpClient(_handler)
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static HttpEngine()
    {
        // Set user agent to avoid being blocked
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.71 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,vi;q=0.8");
    }

    public Task<PageResponse> FetchAsync(string url, CancellationToken cancellationToken = default)
        => FetchAsync(url, referer: null, cancellationToken);

    public async Task<PageResponse> FetchAsync(string url, string? referer, CancellationToken cancellationToken = default)
    {
        var response = new PageResponse { Url = url };

        try
        {
            // Accept absolute URLs and local file paths
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            {
                uri = absolute;
            }
            else if (System.IO.Path.IsPathRooted(url) && System.IO.File.Exists(url))
            {
                // Convert Windows path to file URI
                uri = new Uri(url);
            }
            else
            {
                response.ErrorMessage = "Invalid URL";
                return response;
            }

            // Handle special URLs
            if (uri.Scheme == "about" || uri.Host == "ntp.nona")
            {
                response.IsSuccess = true;
                response.FinalUrl = url;
                response.StatusCode = 200;
                response.Content = GetSpecialPageContent(url);
                return response;
            }

            // Support file:// for local files
            if (uri.Scheme == Uri.UriSchemeFile)
            {
                var path = uri.LocalPath;
                if (System.IO.File.Exists(path))
                {
                    response.IsSuccess = true;
                    response.FinalUrl = url;
                    response.StatusCode = 200;
                    response.Headers["Content-Type"] = "text/html";
                    response.Content = await System.IO.File.ReadAllTextAsync(path, cancellationToken);
                    return response;
                }
                response.ErrorMessage = "File not found";
                return response;
            }

            // Only support HTTP(S) for network
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                response.ErrorMessage = "Unsupported scheme";
                return response;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            // Add common headers to look more like a real browser
            request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            if (!string.IsNullOrEmpty(referer))
            {
                request.Headers.Referrer = new Uri(referer);
            }

            using var httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            response.StatusCode = (int)httpResponse.StatusCode;
            response.IsSuccess = httpResponse.IsSuccessStatusCode;
            response.FinalUrl = httpResponse.RequestMessage?.RequestUri?.ToString() ?? url;

            // Get headers
            foreach (var header in httpResponse.Headers)
            {
                response.Headers[header.Key] = string.Join(", ", header.Value);
            }

            if (httpResponse.IsSuccessStatusCode)
            {
                response.Content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            }
            else
            {
                response.ErrorMessage = $"HTTP {response.StatusCode}: {httpResponse.ReasonPhrase}";
            }
        }
        catch (TaskCanceledException)
        {
            response.ErrorMessage = "Request timeout";
        }
        catch (HttpRequestException ex)
        {
            response.ErrorMessage = $"Network error: {ex.Message}";
        }
        catch (Exception ex)
        {
            response.ErrorMessage = $"Error: {ex.Message}";
        }

        return response;
    }

    private string GetSpecialPageContent(string url)
    {
        if (url.Contains("ntp.nona") || url == "about:blank")
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>New Tab</title>
    <meta charset=""utf-8"">
</head>
<body style=""font-family: Segoe UI, Arial, sans-serif; text-align: center; padding: 50px; background: #1e1e1e; color: #fff;"">
    <h1>Welcome to Nona Browser</h1>
    <p>A lightweight, performance-focused browser</p>
    <form style=""margin-top: 30px;"">
        <input type=""text"" placeholder=""Search or enter website..."" 
               style=""width: 500px; padding: 12px; font-size: 14px; border-radius: 8px; border: 1px solid #555; background: #2d2d2d; color: #fff;"">
    </form>
</body>
</html>";
        }

        return $"<html><body><h1>Page not found</h1><p>{url}</p></body></html>";
    }
}


