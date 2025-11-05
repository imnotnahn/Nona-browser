using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nona.Engine;

/// <summary>
/// Lightweight HTML parser for basic rendering
/// </summary>
public class HtmlParser
{
    public HtmlDocument Parse(string html)
    {
        var doc = new HtmlDocument();
        if (string.IsNullOrWhiteSpace(html))
            return doc;

        // Remove styles for performance (keep scripts for JS execution)
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        // Remove noscript tags since we have JS enabled
        html = Regex.Replace(html, @"<noscript[^>]*>[\s\S]*?</noscript>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        doc.Root = ParseNode(html, 0, out _);
        return doc;
    }

    private HtmlNode ParseNode(string html, int startIndex, out int endIndex)
    {
        var root = new HtmlNode { Tag = "root" };
        endIndex = startIndex;

        var i = startIndex;
        while (i < html.Length)
        {
            // Find next tag
            var tagStart = html.IndexOf('<', i);
            if (tagStart == -1)
            {
                // No more tags, add remaining text
                if (i < html.Length)
                {
                    var text = html.Substring(i);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        root.Children.Add(new HtmlNode 
                        { 
                            Tag = "#text", 
                            TextContent = DecodeHtml(text.Trim()) 
                        });
                    }
                }
                break;
            }

            // Add text before tag
            if (tagStart > i)
            {
                var text = html.Substring(i, tagStart - i);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    root.Children.Add(new HtmlNode 
                    { 
                        Tag = "#text", 
                        TextContent = DecodeHtml(text.Trim()) 
                    });
                }
            }

            var tagEnd = html.IndexOf('>', tagStart);
            if (tagEnd == -1) break;

            var tagContent = html.Substring(tagStart + 1, tagEnd - tagStart - 1);

            // Check for closing tag
            if (tagContent.StartsWith("/"))
            {
                endIndex = tagEnd + 1;
                return root;
            }

            // Check for self-closing or void elements
            var isSelfClosing = tagContent.EndsWith("/") || 
                IsVoidElement(tagContent.Split(' ')[0]);

            // Parse tag and attributes
            var parts = tagContent.TrimEnd('/').Split(new[] { ' ' }, 2);
            var tagName = parts[0].ToLower();
            var attributes = parts.Length > 1 ? ParseAttributes(parts[1]) : new Dictionary<string, string>();

            var node = new HtmlNode
            {
                Tag = tagName,
                Attributes = attributes
            };

            if (isSelfClosing)
            {
                root.Children.Add(node);
                i = tagEnd + 1;
            }
            else
            {
                // Find closing tag
                var closingTag = $"</{tagName}";
                var closingIndex = FindClosingTag(html, tagEnd + 1, tagName);
                
                if (closingIndex != -1)
                {
                    // Parse children
                    var innerHtml = html.Substring(tagEnd + 1, closingIndex - tagEnd - 1);
                    node.InnerHtml = innerHtml;
                    node.Children = ParseChildren(innerHtml);
                    
                    root.Children.Add(node);
                    i = closingIndex + tagName.Length + 3; // Skip </tagname>
                }
                else
                {
                    // No closing tag found, treat as self-closing
                    root.Children.Add(node);
                    i = tagEnd + 1;
                }
            }
        }

        endIndex = html.Length;
        return root;
    }

    private List<HtmlNode> ParseChildren(string html)
    {
        var children = new List<HtmlNode>();
        if (string.IsNullOrWhiteSpace(html))
            return children;

        var tempRoot = ParseNode(html, 0, out _);
        return tempRoot.Children;
    }

    private int FindClosingTag(string html, int startIndex, string tagName)
    {
        var depth = 1;
        var i = startIndex;
        var openTag = $"<{tagName}";
        var closeTag = $"</{tagName}";

        while (i < html.Length && depth > 0)
        {
            var nextOpen = html.IndexOf(openTag, i, StringComparison.OrdinalIgnoreCase);
            var nextClose = html.IndexOf(closeTag, i, StringComparison.OrdinalIgnoreCase);

            if (nextClose == -1)
                return -1;

            if (nextOpen != -1 && nextOpen < nextClose)
            {
                // Check if it's actually an opening tag (not part of text)
                var nextChar = nextOpen + openTag.Length;
                if (nextChar < html.Length && (html[nextChar] == ' ' || html[nextChar] == '>' || html[nextChar] == '/'))
                {
                    depth++;
                    i = nextOpen + openTag.Length;
                }
                else
                {
                    i = nextOpen + 1;
                }
            }
            else
            {
                depth--;
                if (depth == 0)
                    return nextClose;
                i = nextClose + closeTag.Length;
            }
        }

        return -1;
    }

    private Dictionary<string, string> ParseAttributes(string attributeString)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Simple attribute parsing
        var regex = new Regex(@"(\w+)\s*=\s*[""']([^""']*)[""']");
        var matches = regex.Matches(attributeString);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3)
            {
                attributes[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }

        return attributes;
    }

    private bool IsVoidElement(string tag)
    {
        var voidElements = new[] { "area", "base", "br", "col", "embed", "hr", "img", "input", 
            "link", "meta", "param", "source", "track", "wbr" };
        return voidElements.Contains(tag.ToLower());
    }

    private string DecodeHtml(string text)
    {
        return text
            .Replace("&nbsp;", " ")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&amp;", "&")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'");
    }
}

/// <summary>
/// Represents an HTML document
/// </summary>
public class HtmlDocument
{
    public HtmlNode Root { get; set; } = new HtmlNode();
    public string Title { get; set; } = "";

    public void UpdateTitle()
    {
        var titleNode = FindNode(Root, "title");
        if (titleNode != null)
        {
            Title = titleNode.TextContent ?? string.Join("", titleNode.Children
                .Where(c => c.Tag == "#text")
                .Select(c => c.TextContent));
        }
    }

    private HtmlNode? FindNode(HtmlNode node, string tag)
    {
        if (node.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            return node;

        foreach (var child in node.Children)
        {
            var found = FindNode(child, tag);
            if (found != null) return found;
        }

        return null;
    }
}

/// <summary>
/// Represents an HTML node (element or text)
/// </summary>
public class HtmlNode
{
    public string Tag { get; set; } = "";
    public Dictionary<string, string> Attributes { get; set; } = new();
    public List<HtmlNode> Children { get; set; } = new();
    public string? TextContent { get; set; }
    public string InnerHtml { get; set; } = "";

    public string GetAttribute(string name, string defaultValue = "")
    {
        return Attributes.TryGetValue(name, out var value) ? value : defaultValue;
    }

    public bool IsTextNode => Tag == "#text";
}


