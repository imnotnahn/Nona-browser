using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Nona.Security;

public enum BlockingMode
{
    Off,
    Balanced,
    Strict
}

public interface IRulesEngine
{
    BlockingMode Mode { get; set; }
    bool IsBlocked(Uri requestUri, string documentHost);
    Task LoadListsAsync(IEnumerable<string> listFilePaths);
}

// Modern ad blocking engine with intelligent whitelisting
public sealed class ExtendedRulesEngine : IRulesEngine
{
    private readonly HashSet<string> _blockedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _whitelistedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Regex> _regexRules = new();
    private readonly List<Regex> _whitelistRegexRules = new();
    private readonly AhoCorasick _substringMatcher = new();
    private readonly List<string> _wildcardPatterns = new();
    private readonly List<string> _whitelistPatterns = new();
    private readonly ConcurrentDictionary<string, bool> _cache = new();
    
    // Essential domain suffixes that should never be blocked (host.EndsWith)
    private readonly List<string> _essentialDomainSuffixes = new()
    {
        // Video streaming essentials
        ".youtube.com", ".googlevideo.com", ".ytimg.com", ".ggpht.com", ".googleusercontent.com",

        // CDN and essential services
        ".gstatic.com", ".googleapis.com", ".jsdelivr.net", ".cloudflare.com", ".unpkg.com",

        // Social media core functionality & CDNs
        ".facebook.com", ".fbcdn.net", ".cdninstagram.com", ".instagram.com", ".x.com", ".twitter.com",

        // TikTok ecosystems
        ".tiktok.com", ".tiktokcdn.com", ".tiktokcdn-us.com", ".tiktokv.com", ".ibyteimg.com", ".byteimg.com", ".bytefcdn.com", ".bytefcdn-oversea.com",

        // Essential Microsoft services
        ".microsoft.com", ".microsoftonline.com", ".live.com", ".office.com", ".outlook.com", ".xbox.com",

        // Payment and security
        ".paypal.com", ".stripe.com", ".visa.com", ".mastercard.com", ".recaptcha.net"
    };

    public BlockingMode Mode { get; set; } = BlockingMode.Balanced;
    public bool DebugMode { get; set; } = false;

    public bool IsBlocked(Uri requestUri, string documentHost)
    {
        if (Mode == BlockingMode.Off) return false;
        
        var url = requestUri.ToString();
        var host = requestUri.Host;
        
        // Check cache first
        if (_cache.TryGetValue(url, out var cachedResult))
            return cachedResult;

        var isBlocked = CheckBlocking(url, host, documentHost);
        
        // Debug logging
        if (DebugMode)
        {
            var action = isBlocked ? "BLOCKED" : "ALLOWED";
            Console.WriteLine($"[AdBlock] {action}: {host} - {url}");
        }
        
        // Cache result (limit cache size to prevent memory issues)
        if (_cache.Count < 10000)
            _cache.TryAdd(url, isBlocked);
        
        return isBlocked;
    }

    private bool CheckBlocking(string url, string host, string documentHost)
    {
        // 0. Never block essential domains (suffix match)
        if (_essentialDomainSuffixes.Any(sfx => host.EndsWith(sfx, StringComparison.OrdinalIgnoreCase)))
            return false;
            
        // Site-wide exemptions
        // Disable adblock entirely on TikTok properties per product decision
        if (IsTikTokRelated(documentHost, url))
            return false;

        // Special handling for YouTube & TikTok
        if (IsYouTubeRelated(host, url))
            return IsYouTubeAdBlocked(url);
        if (IsTikTokRelated(host, url))
            return IsTikTokAdBlocked(url);
            
        // 1. Check whitelist first (@@rules)
        if (_whitelistedHosts.Contains(host))
            return false;
            
        foreach (var pattern in _whitelistPatterns)
        {
            if (IsWildcardMatch(url, pattern) || IsWildcardMatch(host, pattern))
                return false;
        }
        
        foreach (var regex in _whitelistRegexRules)
        {
            try
            {
                if (regex.IsMatch(url))
                    return false;
            }
            catch { }
        }

        // 2. Check if it's a legitimate resource
        if (IsLegitimateResource(url, host, documentHost))
            return false;

        // 3. Check exact host blocking
        if (_blockedHosts.Contains(host))
            return true;

        // 4. Check wildcard host patterns
        foreach (var pattern in _wildcardPatterns)
        {
            if (IsWildcardMatch(host, pattern) || IsWildcardMatch(url, pattern))
                return true;
        }

        // 5. Check substring patterns (fastest)
        if (_substringMatcher.ContainsAny(url))
        {
            // Double-check if it's not a false positive
            if (IsFalsePositive(url, host, documentHost))
                return false;
            return true;
        }

        // 6. Check regex patterns (slowest, do last)
        foreach (var regex in _regexRules)
        {
            try
            {
                if (regex.IsMatch(url))
                {
                    if (IsFalsePositive(url, host, documentHost))
                        return false;
                    return true;
                }
            }
            catch { }
        }

        // 7. Advanced checks for strict mode
        if (Mode == BlockingMode.Strict)
        {
            return IsStrictModeBlocked(url, host, documentHost);
        }

        return false;
    }

    private bool IsYouTubeRelated(string host, string url)
    {
        return host.Contains("youtube", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("googlevideo", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("ytimg", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("ggpht", StringComparison.OrdinalIgnoreCase) ||
               (host.Contains("googleapis", StringComparison.OrdinalIgnoreCase) && 
                url.Contains("youtube", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsYouTubeAdBlocked(string url)
    {
        // Only block obvious YouTube ads, not essential functionality
        var youtubeAdPatterns = new[]
        {
            "/ptracking",
            "/api/stats/ads",
            "/pagead/",
            "/doubleclick/",
            "/googleads/",
            "get_midroll_info",
            "ad_break",
            "/ads?",
            "&ad_type=",
            "adunit=",
            "ad_video_pub_id"
        };

        return youtubeAdPatterns.Any(pattern => 
            url.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsLegitimateResource(string url, string host, string documentHost)
    {
        // Check for essential file types
        var essentialExtensions = new[] { ".js", ".css", ".woff", ".woff2", ".ttf", ".eot", ".svg", ".ico", ".json" };
        if (essentialExtensions.Any(ext => url.Contains(ext, StringComparison.OrdinalIgnoreCase)))
        {
            // Allow essential resources from same domain or trusted CDNs
            if (host == documentHost || IsTrustedCDN(host))
                return true;
        }

        // Allow common image types (usually content) from same domain or known CDNs
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };
        if (imageExtensions.Any(ext => url.Contains(ext, StringComparison.OrdinalIgnoreCase)))
        {
            if (host == documentHost || IsTrustedCDN(host))
                return true;
            // Social platforms
            if (documentHost.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) && host.Contains("fbcdn.net", StringComparison.OrdinalIgnoreCase))
                return true;
            if (documentHost.Contains("instagram.com", StringComparison.OrdinalIgnoreCase) && (host.Contains("cdninstagram.com", StringComparison.OrdinalIgnoreCase) || host.Contains("fbcdn.net", StringComparison.OrdinalIgnoreCase)))
                return true;
            if (documentHost.Contains("tiktok", StringComparison.OrdinalIgnoreCase) && (host.Contains("tiktokcdn", StringComparison.OrdinalIgnoreCase) || host.Contains("byte", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // Allow API calls from same domain
        if (host == documentHost && (url.Contains("/api/") || url.Contains("/xhr/") || url.Contains("/ajax/") || url.Contains("/v1/") || url.Contains("/v2/")))
            return true;

        // Allow authentication and session management
        if (url.Contains("/auth/") || url.Contains("/login/") || url.Contains("/session/") || 
            url.Contains("/oauth/") || url.Contains("/sso/"))
            return true;

        return false;
    }

    private bool IsTrustedCDN(string host)
    {
        var trustedCDNs = new[]
        {
            "cdn.jsdelivr.net", "cdnjs.cloudflare.com", "unpkg.com",
            "ajax.googleapis.com", "fonts.googleapis.com", "gstatic.com",
            "bootstrapcdn.com", "fontawesome.com", "jquery.com",
            // Social/CDN
            "fbcdn.net", "cdninstagram.com", "tiktokcdn.com", "tiktokcdn-us.com", "tiktokv.com", "ibyteimg.com", "byteimg.com", "bytefcdn.com", "bytefcdn-oversea.com"
        };
        
        return trustedCDNs.Any(cdn => host.Contains(cdn, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsFalsePositive(string url, string host, string documentHost)
    {
        // Check if this might be a false positive
        
        // Same-domain requests are usually legitimate
        if (host == documentHost)
            return true;
            
        // Essential file types from subdomain
        if (host.EndsWith(documentHost, StringComparison.OrdinalIgnoreCase))
        {
            var essentialTypes = new[] { ".js", ".css", ".json", ".xml", ".woff", ".ttf" };
            if (essentialTypes.Any(type => url.Contains(type)))
                return true;
        }

        // Video streaming URLs
        if (url.Contains("videoplayback") || url.Contains("manifest") ||
            url.Contains("playlist.m3u8") || url.Contains(".mp4") ||
            url.Contains(".webm") || url.Contains(".ts"))
            return true;

        return false;
    }

    private bool IsTikTokRelated(string host, string url)
    {
        return host.Contains("tiktok", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("byte", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTikTokAdBlocked(string url)
    {
        // Block common ad endpoints but allow content endpoints
        var adPatterns = new[] { 
            "/ad/", "/ads/", "/advert/", "/promotion/", 
            "/pixel", "/track", "/analytics", "/telemetry"
        };
        if (adPatterns.Any(p => url.Contains(p, StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }

    private bool IsStrictModeBlocked(string url, string host, string documentHost)
    {
        // Block third-party requests to known ad/tracking domains
        if (documentHost != host)
        {
            var suspiciousKeywords = new[] { "ad", "ads", "track", "analytic", "metric", "pixel", "beacon" };
            if (suspiciousKeywords.Any(keyword => host.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // Block requests with suspicious query parameters
        var suspiciousParams = new[] { "utm_", "fbclid", "gclid", "msclkid", "dclid", "_ga", "mc_eid" };
        if (suspiciousParams.Any(param => url.Contains(param, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static bool IsWildcardMatch(string text, string pattern)
    {
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            try
            {
                return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        return text.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoadListsAsync(IEnumerable<string> listFilePaths)
    {
        var totalRules = 0;
        var processedRules = 0;

        foreach (var path in listFilePaths)
        {
            if (!File.Exists(path)) continue;
            
            try
            {
                var lines = await File.ReadAllLinesAsync(path);
                totalRules += lines.Length;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("!") || trimmed.StartsWith("#")) 
                        continue;

                    processedRules++;
                    ProcessRule(trimmed);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue processing other files
                Console.WriteLine($"Error loading rules from {path}: {ex.Message}");
            }
        }

        // Build the pattern matcher
        _substringMatcher.Build();
        
        Console.WriteLine($"Loaded {processedRules} ad blocking rules from {totalRules} total lines");
        Console.WriteLine($"Blocked - Hosts: {_blockedHosts.Count}, Patterns: {_wildcardPatterns.Count}, Regex: {_regexRules.Count}");
        Console.WriteLine($"Whitelisted - Hosts: {_whitelistedHosts.Count}, Patterns: {_whitelistPatterns.Count}, Regex: {_whitelistRegexRules.Count}");
    }

    private void ProcessRule(string rule)
    {
        try
        {
            // Host blocking rules: ||example.com^
            if (rule.StartsWith("||"))
            {
                var host = rule.Substring(2).TrimEnd('^', '/', '*');
                if (host.Contains('*'))
                {
                    _wildcardPatterns.Add(host);
                }
                else if (host.Length > 0 && IsValidHost(host))
                {
                    _blockedHosts.Add(host);
                }
                return;
            }

            // Regex rules: /pattern/
            if (rule.StartsWith("/") && rule.EndsWith("/") && rule.Length > 2)
            {
                var pattern = rule.Substring(1, rule.Length - 2);
                try
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
                    _regexRules.Add(regex);
                    return;
                }
                catch
                {
                    // If regex compilation fails, treat as substring
                    if (!string.IsNullOrWhiteSpace(pattern))
                        _substringMatcher.AddPattern(pattern);
                }
                return;
            }

            // Cosmetic rules (ignore)
            if (rule.Contains("##") || rule.Contains("#$#") || rule.Contains("#@#"))
            {
                return;
            }

            // Element hiding rules (ignore for network blocking)
            if (rule.Contains("###") || rule.Contains("##."))
            {
                return;
            }

            // Whitelist rules (start with @@)
            if (rule.StartsWith("@@"))
            {
                var whitelistRule = rule.Substring(2); // Remove @@
                
                if (whitelistRule.StartsWith("||"))
                {
                    var host = whitelistRule.Substring(2).TrimEnd('^', '/', '*');
                    if (host.Contains('*'))
                    {
                        _whitelistPatterns.Add(host);
                    }
                    else if (host.Length > 0 && IsValidHost(host))
                    {
                        _whitelistedHosts.Add(host);
                    }
                }
                else if (whitelistRule.StartsWith("/") && whitelistRule.EndsWith("/") && whitelistRule.Length > 2)
                {
                    var pattern = whitelistRule.Substring(1, whitelistRule.Length - 2);
                    try
                    {
                        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
                        _whitelistRegexRules.Add(regex);
                    }
                    catch { }
                }
                else
                {
                    var cleanWhitelistRule = whitelistRule.TrimEnd('^', '$');
                    if (cleanWhitelistRule.Length > 2)
                        _whitelistPatterns.Add(cleanWhitelistRule);
                }
                return;
            }

            // Generic blocking rules
            var cleanRule = rule.TrimEnd('^', '$');
            if (cleanRule.Length > 2)
            {
                if (cleanRule.Contains('*') || cleanRule.Contains('?'))
                {
                    _wildcardPatterns.Add(cleanRule);
                }
                else
                {
                    _substringMatcher.AddPattern(cleanRule);
                }
            }
        }
        catch (Exception ex)
        {
            // Skip invalid rules
            Console.WriteLine($"Skipping invalid rule: {rule} - {ex.Message}");
        }
    }

    private static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        if (host.Length > 253) return false; // Max domain length
        if (host.StartsWith(".") || host.EndsWith(".")) return false;
        if (host.Contains("..")) return false;
        
        // Basic character validation
        foreach (char c in host)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-')
                return false;
        }
        
        return true;
    }
}

public interface IHttpsOnlyUpgrader
{
    Uri? TryUpgrade(Uri input);
}

public sealed class HttpsOnlyUpgrader : IHttpsOnlyUpgrader
{
    public Uri? TryUpgrade(Uri input)
    {
        if (input.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return new UriBuilder(input) { Scheme = "https", Port = -1 }.Uri;
        }
        return input;
    }
}

// Minimal Aho-Corasick implementation for substring matching
internal sealed class AhoCorasick
{
    private sealed class Node
    {
        public Dictionary<char, Node> Next { get; } = new();
        public Node? Fail { get; set; }
        public bool Output { get; set; }
    }
    private readonly Node _root = new();
    private bool _built;

    public void AddPattern(string pattern)
    {
        var node = _root;
        foreach (var ch in pattern)
        {
            if (!node.Next.TryGetValue(ch, out var nxt))
            {
                nxt = new Node();
                node.Next[ch] = nxt;
            }
            node = nxt;
        }
        node.Output = true;
        _built = false;
    }

    public void Build()
    {
        var q = new Queue<Node>();
        foreach (var kv in _root.Next)
        {
            kv.Value.Fail = _root;
            q.Enqueue(kv.Value);
        }
        while (q.Count > 0)
        {
            var current = q.Dequeue();
            foreach (var kv in current.Next)
            {
                var ch = kv.Key;
                var target = kv.Value;
                var f = current.Fail;
                while (f != null && !f.Next.ContainsKey(ch)) f = f.Fail;
                target.Fail = f?.Next.GetValueOrDefault(ch) ?? _root;
                target.Output |= target.Fail.Output;
                q.Enqueue(target);
            }
        }
        _built = true;
    }

    public bool ContainsAny(string text)
    {
        if (!_built) Build();
        var node = _root;
        foreach (var ch in text)
        {
            while (node != null && !node.Next.TryGetValue(ch, out node))
            {
                node = node?.Fail!;
            }
            if (node == null) node = _root;
            if (node.Output) return true;
        }
        return false;
    }
}
