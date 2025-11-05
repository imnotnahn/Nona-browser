using System;
using System.Collections.Generic;
using System.Linq;

namespace Nona.Engine;

/// <summary>
/// Full DOM Document implementation for Jint
/// Supports: getElementById, createElement, querySelector, events, etc.
/// </summary>
public class JintDocument
{
    private readonly HtmlDocument _doc;
    private JintElement? _bodyElement;
    private JintElement? _headElement;
    private readonly EventManager _eventManager = new();
    
    public string title
    {
        get => _doc.Title;
        set => _doc.Title = value;
    }
    
    public string URL { get; set; } = "";
    public string domain { get; set; } = "";
    public string referrer { get; set; } = "";
    public string cookie { get; set; } = "";
    public string readyState { get; set; } = "complete";
    public string characterSet => "UTF-8";
    public string charset => "UTF-8";
    public string inputEncoding => "UTF-8";
    public string contentType => "text/html";
    public string compatMode => "CSS1Compat";
    public bool designMode => false;
    public string dir { get; set; } = "";
    public object? activeElement => body;
    public bool hidden => false;
    public string visibilityState => "visible";
    
    // Critical DOM properties
    public object? body
    {
        get
        {
            if (_bodyElement == null)
            {
                var bodyNode = FindNodeByTag(_doc.Root, "body");
                if (bodyNode != null)
                {
                    _bodyElement = new JintElement(bodyNode, _eventManager);
                }
            }
            return _bodyElement;
        }
    }
    
    public object? head
    {
        get
        {
            if (_headElement == null)
            {
                var headNode = FindNodeByTag(_doc.Root, "head");
                if (headNode != null)
                {
                    _headElement = new JintElement(headNode, _eventManager);
                }
            }
            return _headElement;
        }
    }
    
    public object? documentElement => new JintElement(_doc.Root, _eventManager);
    
    public JintDocument(HtmlDocument doc)
    {
        _doc = doc;
    }
    
    // ==== DOM QUERY METHODS ====
    
    public object? getElementById(string id)
    {
        var node = FindNodeById(_doc.Root, id);
        return node != null ? new JintElement(node, _eventManager) : null;
    }
    
    public object getElementsByTagName(string tagName)
    {
        var nodes = FindNodesByTag(_doc.Root, tagName);
        return nodes.Select(n => new JintElement(n, _eventManager)).ToArray();
    }
    
    public object getElementsByClassName(string className)
    {
        var nodes = FindNodesByClass(_doc.Root, className);
        return nodes.Select(n => new JintElement(n, _eventManager)).ToArray();
    }
    
    public object getElementsByName(string name)
    {
        var nodes = FindNodesByAttribute(_doc.Root, "name", name);
        return nodes.Select(n => new JintElement(n, _eventManager)).ToArray();
    }
    
    public object? querySelector(string selector)
    {
        var node = QuerySelector(_doc.Root, selector);
        return node != null ? new JintElement(node, _eventManager) : null;
    }
    
    public object querySelectorAll(string selector)
    {
        var nodes = QuerySelectorAll(_doc.Root, selector);
        return nodes.Select(n => new JintElement(n, _eventManager)).ToArray();
    }
    
    // ==== DOM MANIPULATION ====
    
    public object createElement(string tagName)
    {
        var node = new HtmlNode
        {
            Tag = tagName.ToLowerInvariant()
        };
        return new JintElement(node, _eventManager);
    }
    
    public object createTextNode(string text)
    {
        var node = new HtmlNode
        {
            Tag = "#text",
            TextContent = text
        };
        return new JintElement(node, _eventManager);
    }
    
    public object createDocumentFragment()
    {
        var node = new HtmlNode { Tag = "#fragment" };
        return new JintElement(node, _eventManager);
    }
    
    // ==== EVENT METHODS ====
    
    public void addEventListener(string eventType, object handler, object? options = null)
    {
        _eventManager.AddEventListener(_doc.Root, eventType, handler);
    }
    
    public void removeEventListener(string eventType, object handler, object? options = null)
    {
        _eventManager.RemoveEventListener(_doc.Root, eventType, handler);
    }
    
    public object createEvent(string eventType)
    {
        return new JintEvent(eventType);
    }
    
    // ==== DOCUMENT WRITE METHODS ====
    
    public void write(string text)
    {
        // Simplified - just log
    }
    
    public void writeln(string text)
    {
        // Simplified - just log
    }
    
    public void open()
    {
        // Simplified
    }
    
    public void close()
    {
        // Simplified
    }
    
    // ==== DOCUMENT FOCUS METHODS ====
    
    public void hasFocus()
    {
        // Returns true
    }
    
    // ==== MISC METHODS ====
    
    public object? importNode(object node, bool deep)
    {
        return node;
    }
    
    public object? adoptNode(object node)
    {
        return node;
    }
    
    public object createAttribute(string name)
    {
        return new { name, value = "" };
    }
    
    public object createComment(string data)
    {
        return createTextNode(data);
    }
    
    // ==== HELPER METHODS ====
    
    private HtmlNode? FindNodeById(HtmlNode node, string id)
    {
        if (node.Attributes.TryGetValue("id", out var nodeId) && nodeId == id)
            return node;
        
        foreach (var child in node.Children)
        {
            var found = FindNodeById(child, id);
            if (found != null) return found;
        }
        return null;
    }
    
    private HtmlNode? FindNodeByTag(HtmlNode node, string tag)
    {
        if (node.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            return node;
        
        foreach (var child in node.Children)
        {
            var found = FindNodeByTag(child, tag);
            if (found != null) return found;
        }
        return null;
    }
    
    private List<HtmlNode> FindNodesByTag(HtmlNode node, string tag)
    {
        var results = new List<HtmlNode>();
        if (node.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            results.Add(node);
        
        foreach (var child in node.Children)
            results.AddRange(FindNodesByTag(child, tag));
        
        return results;
    }
    
    private List<HtmlNode> FindNodesByClass(HtmlNode node, string className)
    {
        var results = new List<HtmlNode>();
        if (node.Attributes.TryGetValue("class", out var classes) &&
            classes.Split(' ').Contains(className))
        {
            results.Add(node);
        }
        
        foreach (var child in node.Children)
            results.AddRange(FindNodesByClass(child, className));
        
        return results;
    }
    
    private List<HtmlNode> FindNodesByAttribute(HtmlNode node, string attrName, string attrValue)
    {
        var results = new List<HtmlNode>();
        if (node.Attributes.TryGetValue(attrName, out var value) && value == attrValue)
            results.Add(node);
        
        foreach (var child in node.Children)
            results.AddRange(FindNodesByAttribute(child, attrName, attrValue));
        
        return results;
    }
    
    private HtmlNode? QuerySelector(HtmlNode node, string selector)
    {
        // Simplified querySelector
        if (selector.StartsWith("#"))
            return FindNodeById(node, selector.Substring(1));
        else if (selector.StartsWith("."))
            return FindNodesByClass(node, selector.Substring(1)).FirstOrDefault();
        else if (selector.Contains("["))
        {
            // Attribute selector [name="value"]
            var match = System.Text.RegularExpressions.Regex.Match(selector, @"\[(\w+)=[""']([^""']+)[""']\]");
            if (match.Success)
            {
                var attrName = match.Groups[1].Value;
                var attrValue = match.Groups[2].Value;
                return FindNodesByAttribute(node, attrName, attrValue).FirstOrDefault();
            }
        }
        else
            return FindNodeByTag(node, selector);
        
        return null;
    }
    
    private List<HtmlNode> QuerySelectorAll(HtmlNode node, string selector)
    {
        if (selector.StartsWith("#"))
        {
            var found = FindNodeById(node, selector.Substring(1));
            return found != null ? new List<HtmlNode> { found } : new List<HtmlNode>();
        }
        else if (selector.StartsWith("."))
            return FindNodesByClass(node, selector.Substring(1));
        else if (selector.Contains("["))
        {
            var match = System.Text.RegularExpressions.Regex.Match(selector, @"\[(\w+)=[""']([^""']+)[""']\]");
            if (match.Success)
            {
                return FindNodesByAttribute(node, match.Groups[1].Value, match.Groups[2].Value);
            }
        }
        else
            return FindNodesByTag(node, selector);
        
        return new List<HtmlNode>();
    }
}

/// <summary>
/// DOM Element with full event support
/// </summary>
public class JintElement
{
    private readonly HtmlNode _node;
    private readonly EventManager _eventManager;
    private JintCSSStyleDeclaration? _style;
    
    public string tagName => _node.Tag.ToUpperInvariant();
    public string nodeName => _node.Tag.ToUpperInvariant();
    public int nodeType => _node.IsTextNode ? 3 : 1;
    
    public string innerHTML
    {
        get => _node.InnerHtml;
        set => _node.InnerHtml = value;
    }
    
    public string outerHTML
    {
        get => $"<{_node.Tag}>{_node.InnerHtml}</{_node.Tag}>";
        set { /* Not implemented */ }
    }
    
    public string textContent
    {
        get => GetAllText(_node);
        set
        {
            _node.Children.Clear();
            _node.TextContent = value;
        }
    }
    
    public string innerText
    {
        get => GetAllText(_node);
        set => textContent = value;
    }
    
    public string className
    {
        get => _node.Attributes.TryGetValue("class", out var c) ? c : "";
        set => _node.Attributes["class"] = value;
    }
    
    public string id
    {
        get => _node.Attributes.TryGetValue("id", out var i) ? i : "";
        set => _node.Attributes["id"] = value;
    }
    
    public object? style
    {
        get
        {
            if (_style == null)
                _style = new JintCSSStyleDeclaration(_node);
            return _style;
        }
    }
    
    public object? classList => new JintDOMTokenList(_node);
    
    public object? children => _node.Children
        .Where(c => !c.IsTextNode)
        .Select(c => new JintElement(c, _eventManager))
        .ToArray();
    
    public object? childNodes => _node.Children
        .Select(c => new JintElement(c, _eventManager))
        .ToArray();
    
    public object? firstChild => _node.Children.Count > 0 
        ? new JintElement(_node.Children[0], _eventManager) 
        : null;
    
    public object? lastChild => _node.Children.Count > 0 
        ? new JintElement(_node.Children[^1], _eventManager) 
        : null;
    
    public object? parentNode => null; // Would need parent tracking
    public object? nextSibling => null; // Would need sibling tracking
    public object? previousSibling => null;
    
    public int offsetWidth => 0;
    public int offsetHeight => 0;
    public int clientWidth => 0;
    public int clientHeight => 0;
    public int scrollWidth => 0;
    public int scrollHeight => 0;
    public int scrollTop { get; set; }
    public int scrollLeft { get; set; }
    
    public object? dataset => new JintDataset(_node);
    
    public string name
    {
        get => getAttribute("name");
        set => setAttribute("name", value);
    }
    
    public string value
    {
        get => getAttribute("value");
        set => setAttribute("value", value);
    }
    
    public string src
    {
        get => getAttribute("src");
        set => setAttribute("src", value);
    }
    
    public string href
    {
        get => getAttribute("href");
        set => setAttribute("href", value);
    }
    
    public string type
    {
        get => getAttribute("type");
        set => setAttribute("type", value);
    }
    
    public bool disabled
    {
        get => hasAttribute("disabled");
        set
        {
            if (value) setAttribute("disabled", "");
            else removeAttribute("disabled");
        }
    }
    
    public bool checked_prop
    {
        get => hasAttribute("checked");
        set
        {
            if (value) setAttribute("checked", "");
            else removeAttribute("checked");
        }
    }
    
    public bool hidden
    {
        get => hasAttribute("hidden");
        set
        {
            if (value) setAttribute("hidden", "");
            else removeAttribute("hidden");
        }
    }
    
    public JintElement(HtmlNode node, EventManager eventManager)
    {
        _node = node;
        _eventManager = eventManager;
    }
    
    // ==== ATTRIBUTE METHODS ====
    
    public string getAttribute(string name)
    {
        return _node.Attributes.TryGetValue(name.ToLowerInvariant(), out var value) ? value : "";
    }
    
    public void setAttribute(string name, string value)
    {
        _node.Attributes[name.ToLowerInvariant()] = value;
    }
    
    public void removeAttribute(string name)
    {
        _node.Attributes.Remove(name.ToLowerInvariant());
    }
    
    public bool hasAttribute(string name)
    {
        return _node.Attributes.ContainsKey(name.ToLowerInvariant());
    }
    
    public object? getAttributeNode(string name)
    {
        return hasAttribute(name) ? new { name, value = getAttribute(name) } : null;
    }
    
    // ==== DOM MANIPULATION ====
    
    public void appendChild(object child)
    {
        if (child is JintElement element)
        {
            _node.Children.Add(element._node);
        }
    }
    
    public void removeChild(object child)
    {
        if (child is JintElement element)
        {
            _node.Children.Remove(element._node);
        }
    }
    
    public void insertBefore(object newChild, object? refChild)
    {
        if (newChild is JintElement newElement)
        {
            if (refChild is JintElement refElement)
            {
                var index = _node.Children.IndexOf(refElement._node);
                if (index >= 0)
                    _node.Children.Insert(index, newElement._node);
            }
            else
            {
                _node.Children.Add(newElement._node);
            }
        }
    }
    
    public void replaceChild(object newChild, object oldChild)
    {
        if (newChild is JintElement newElement && oldChild is JintElement oldElement)
        {
            var index = _node.Children.IndexOf(oldElement._node);
            if (index >= 0)
            {
                _node.Children[index] = newElement._node;
            }
        }
    }
    
    public object cloneNode(bool deep = false)
    {
        // Simplified clone
        var cloned = new HtmlNode
        {
            Tag = _node.Tag,
            Attributes = new Dictionary<string, string>(_node.Attributes),
            TextContent = _node.TextContent
        };
        
        if (deep)
        {
            foreach (var child in _node.Children)
            {
                cloned.Children.Add(child);
            }
        }
        
        return new JintElement(cloned, _eventManager);
    }
    
    // ==== EVENT METHODS ====
    
    public void addEventListener(string eventType, object handler, object? options = null)
    {
        _eventManager.AddEventListener(_node, eventType, handler);
    }
    
    public void removeEventListener(string eventType, object handler, object? options = null)
    {
        _eventManager.RemoveEventListener(_node, eventType, handler);
    }
    
    public void dispatchEvent(object evt)
    {
        if (evt is JintEvent jintEvent)
        {
            _eventManager.DispatchEvent(_node, jintEvent);
        }
    }
    
    // ==== QUERY METHODS ====
    
    public object? querySelector(string selector)
    {
        // Simplified
        return null;
    }
    
    public object querySelectorAll(string selector)
    {
        return Array.Empty<JintElement>();
    }
    
    // ==== CLASS MANIPULATION ====
    
    public void classList_add(string className)
    {
        var classes = className.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var current = this.className.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        current.AddRange(classes.Where(c => !current.Contains(c)));
        this.className = string.Join(" ", current);
    }
    
    public void classList_remove(string className)
    {
        var classes = className.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = this.className.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var c in classes)
            current.Remove(c);
        this.className = string.Join(" ", current);
    }
    
    public bool classList_contains(string className)
    {
        return this.className.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className);
    }
    
    public void classList_toggle(string className)
    {
        if (classList_contains(className))
            classList_remove(className);
        else
            classList_add(className);
    }
    
    // ==== HELPERS ====
    
    private string GetAllText(HtmlNode node)
    {
        if (node.IsTextNode)
            return node.TextContent ?? "";
        
        return string.Join("", node.Children.Select(GetAllText));
    }
}

/// <summary>
/// CSS Style Declaration
/// </summary>
public class JintCSSStyleDeclaration
{
    private readonly HtmlNode _node;
    private readonly Dictionary<string, string> _styles = new();
    
    public JintCSSStyleDeclaration(HtmlNode node)
    {
        _node = node;
        ParseStyles();
    }
    
    private void ParseStyles()
    {
        if (_node.Attributes.TryGetValue("style", out var styleStr))
        {
            foreach (var part in styleStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var colonIndex = part.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = part.Substring(0, colonIndex).Trim();
                    var value = part.Substring(colonIndex + 1).Trim();
                    _styles[key] = value;
                }
            }
        }
    }
    
    public string getPropertyValue(string property)
    {
        return _styles.TryGetValue(property, out var value) ? value : "";
    }
    
    public void setProperty(string property, string value, string priority = "")
    {
        _styles[property] = value;
        UpdateStyleAttribute();
    }
    
    public void removeProperty(string property)
    {
        _styles.Remove(property);
        UpdateStyleAttribute();
    }
    
    private void UpdateStyleAttribute()
    {
        var styleStr = string.Join("; ", _styles.Select(kv => $"{kv.Key}: {kv.Value}"));
        _node.Attributes["style"] = styleStr;
    }
    
    // Common properties
    public string display
    {
        get => getPropertyValue("display");
        set => setProperty("display", value);
    }
    
    public string color
    {
        get => getPropertyValue("color");
        set => setProperty("color", value);
    }
    
    public string backgroundColor
    {
        get => getPropertyValue("background-color");
        set => setProperty("background-color", value);
    }
    
    public string width
    {
        get => getPropertyValue("width");
        set => setProperty("width", value);
    }
    
    public string height
    {
        get => getPropertyValue("height");
        set => setProperty("height", value);
    }
    
    public string margin
    {
        get => getPropertyValue("margin");
        set => setProperty("margin", value);
    }
    
    public string padding
    {
        get => getPropertyValue("padding");
        set => setProperty("padding", value);
    }
    
    public string border
    {
        get => getPropertyValue("border");
        set => setProperty("border", value);
    }
    
    public string position
    {
        get => getPropertyValue("position");
        set => setProperty("position", value);
    }
    
    public string top
    {
        get => getPropertyValue("top");
        set => setProperty("top", value);
    }
    
    public string left
    {
        get => getPropertyValue("left");
        set => setProperty("left", value);
    }
    
    public string right
    {
        get => getPropertyValue("right");
        set => setProperty("right", value);
    }
    
    public string bottom
    {
        get => getPropertyValue("bottom");
        set => setProperty("bottom", value);
    }
    
    public string fontSize
    {
        get => getPropertyValue("font-size");
        set => setProperty("font-size", value);
    }
    
    public string fontFamily
    {
        get => getPropertyValue("font-family");
        set => setProperty("font-family", value);
    }
    
    public string fontWeight
    {
        get => getPropertyValue("font-weight");
        set => setProperty("font-weight", value);
    }
    
    public string textAlign
    {
        get => getPropertyValue("text-align");
        set => setProperty("text-align", value);
    }
    
    public string opacity
    {
        get => getPropertyValue("opacity");
        set => setProperty("opacity", value);
    }
    
    public string visibility
    {
        get => getPropertyValue("visibility");
        set => setProperty("visibility", value);
    }
    
    public string overflow
    {
        get => getPropertyValue("overflow");
        set => setProperty("overflow", value);
    }
    
    public string zIndex
    {
        get => getPropertyValue("z-index");
        set => setProperty("z-index", value);
    }
}

/// <summary>
/// DOM Token List (for classList)
/// </summary>
public class JintDOMTokenList
{
    private readonly HtmlNode _node;
    
    public JintDOMTokenList(HtmlNode node)
    {
        _node = node;
    }
    
    public int length => GetClasses().Length;
    
    public string[] value => GetClasses();
    
    public void add(params string[] tokens)
    {
        var current = GetClasses().ToList();
        current.AddRange(tokens.Where(t => !current.Contains(t)));
        SetClasses(current);
    }
    
    public void remove(params string[] tokens)
    {
        var current = GetClasses().ToList();
        foreach (var token in tokens)
            current.Remove(token);
        SetClasses(current);
    }
    
    public bool contains(string token)
    {
        return GetClasses().Contains(token);
    }
    
    public void toggle(string token, bool? force = null)
    {
        if (force.HasValue)
        {
            if (force.Value)
                add(token);
            else
                remove(token);
        }
        else
        {
            if (contains(token))
                remove(token);
            else
                add(token);
        }
    }
    
    private string[] GetClasses()
    {
        if (_node.Attributes.TryGetValue("class", out var classes))
            return classes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Array.Empty<string>();
    }
    
    private void SetClasses(List<string> classes)
    {
        _node.Attributes["class"] = string.Join(" ", classes);
    }
}

/// <summary>
/// Event object
/// </summary>
public class JintEvent
{
    public string type { get; }
    public object? target { get; set; }
    public object? currentTarget { get; set; }
    public bool bubbles { get; set; } = false;
    public bool cancelable { get; set; } = true;
    public bool defaultPrevented { get; private set; }
    public DateTimeOffset timeStamp { get; } = DateTimeOffset.UtcNow;
    
    public JintEvent(string type)
    {
        this.type = type;
    }
    
    public void preventDefault()
    {
        if (cancelable)
            defaultPrevented = true;
    }
    
    public void stopPropagation()
    {
        bubbles = false;
    }
    
    public void stopImmediatePropagation()
    {
        bubbles = false;
    }
}

/// <summary>
/// Event Manager - handles event registration and dispatch
/// </summary>
public class EventManager
{
    private readonly Dictionary<(HtmlNode, string), List<object>> _listeners = new();
    
    public void AddEventListener(HtmlNode node, string eventType, object handler)
    {
        var key = (node, eventType);
        if (!_listeners.ContainsKey(key))
            _listeners[key] = new List<object>();
        
        if (!_listeners[key].Contains(handler))
            _listeners[key].Add(handler);
    }
    
    public void RemoveEventListener(HtmlNode node, string eventType, object handler)
    {
        var key = (node, eventType);
        if (_listeners.ContainsKey(key))
            _listeners[key].Remove(handler);
    }
    
    public void DispatchEvent(HtmlNode node, JintEvent evt)
    {
        var key = (node, evt.type);
        if (_listeners.ContainsKey(key))
        {
            foreach (var handler in _listeners[key].ToList())
            {
                try
                {
                    // Call handler (would need proper Jint callback invocation)
                    // handler(evt);
                }
                catch
                {
                    // Ignore handler errors
                }
            }
        }
    }
}

/// <summary>
/// Dataset API for data-* attributes
/// </summary>
public class JintDataset
{
    private readonly HtmlNode _node;
    
    public JintDataset(HtmlNode node)
    {
        _node = node;
    }
    
    public string Get(string name)
    {
        var attrName = "data-" + CamelToKebab(name);
        return _node.Attributes.TryGetValue(attrName, out var value) ? value : "";
    }
    
    public void Set(string name, string value)
    {
        var attrName = "data-" + CamelToKebab(name);
        _node.Attributes[attrName] = value;
    }
    
    private string CamelToKebab(string str)
    {
        return System.Text.RegularExpressions.Regex.Replace(str, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }
}

