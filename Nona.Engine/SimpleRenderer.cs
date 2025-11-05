using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace Nona.Engine;

/// <summary>
/// Simple HTML renderer using WPF controls
/// Optimized for performance, renders only essential content
/// </summary>
public class SimpleRenderer
{
    public event EventHandler<string>? NavigationRequested;
    public event EventHandler<string>? FormSubmitted;

    private readonly Dictionary<string, Action<HtmlNode, Panel>> _elementRenderers;

    public SimpleRenderer()
    {
        _elementRenderers = new Dictionary<string, Action<HtmlNode, Panel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["h1"] = RenderHeading1,
            ["h2"] = RenderHeading2,
            ["h3"] = RenderHeading3,
            ["h4"] = RenderHeading4,
            ["h5"] = RenderHeading5,
            ["h6"] = RenderHeading6,
            ["p"] = RenderParagraph,
            ["div"] = RenderDiv,
            ["span"] = RenderSpan,
            ["a"] = RenderLink,
            ["img"] = RenderImage,
            ["br"] = RenderBreak,
            ["hr"] = RenderHorizontalRule,
            ["ul"] = RenderUnorderedList,
            ["ol"] = RenderOrderedList,
            ["li"] = RenderListItem,
            ["textarea"] = RenderTextArea,
            ["select"] = RenderSelect,
            ["label"] = RenderLabel,
            ["input"] = RenderInput,
            ["button"] = RenderButton,
            ["form"] = RenderForm,
            ["table"] = RenderTable,
            ["pre"] = RenderPreformatted,
            ["code"] = RenderCode,
            ["blockquote"] = RenderBlockquote,
            ["strong"] = RenderBold,
            ["b"] = RenderBold,
            ["em"] = RenderItalic,
            ["i"] = RenderItalic,
            // Ignore non-visual elements
            ["script"] = RenderIgnore,
            ["style"] = RenderIgnore,
            ["noscript"] = RenderIgnore,
            ["head"] = RenderIgnore,
            ["#text"] = RenderText
        };
    }

    public ScrollViewer Render(HtmlDocument document)
    {
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, // Disable horizontal for better performance
            Background = Brushes.White
        };

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        // Find body or use root
        var body = FindNode(document.Root, "body") ?? document.Root;
        
        // Render with virtualization hint
        RenderChildren(body, mainPanel);

        scrollViewer.Content = mainPanel;
        
        // Performance optimization: cache measurement
        scrollViewer.IsDeferredScrollingEnabled = true;
        
        return scrollViewer;
    }

    private void RenderChildren(HtmlNode node, Panel parent)
    {
        foreach (var child in node.Children)
        {
            RenderNode(child, parent);
        }
    }

    private void RenderNode(HtmlNode node, Panel parent)
    {
        if (_elementRenderers.TryGetValue(node.Tag, out var renderer))
        {
            renderer(node, parent);
        }
        else if (node.Tag == "#text")
        {
            RenderText(node, parent);
        }
        else
        {
            // Unknown tag, render children
            RenderChildren(node, parent);
        }
    }

    private void RenderIgnore(HtmlNode node, Panel parent)
    {
        // Intentionally do nothing for non-visual elements like <script>, <style>, <noscript>, <head>
    }

    private void RenderHeading1(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 10, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderHeading2(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 8, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderHeading3(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 6, 0, 6),
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderHeading4(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 5, 0, 5),
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderHeading5(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 4),
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderHeading6(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 3),
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderParagraph(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 14,
            Margin = new Thickness(0, 5, 0, 5),
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderDiv(HtmlNode node, Panel parent)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 2, 0, 2)
        };
        RenderChildren(node, panel);
        parent.Children.Add(panel);
    }

    private void RenderSpan(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderText(HtmlNode node, Panel parent)
    {
        if (string.IsNullOrWhiteSpace(node.TextContent))
            return;

        var textBlock = new TextBlock
        {
            Text = node.TextContent,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };
        parent.Children.Add(textBlock);
    }

    private void RenderLink(HtmlNode node, Panel parent)
    {
        var href = node.GetAttribute("href", "#");
        var textBlock = new TextBlock
        {
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };

        var hyperlink = new Hyperlink
        {
            NavigateUri = Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri) ? uri : null,
            Foreground = Brushes.Blue,
            TextDecorations = TextDecorations.Underline
        };

        hyperlink.Inlines.Add(GetTextContent(node));
        hyperlink.RequestNavigate += (s, e) =>
        {
            NavigationRequested?.Invoke(this, href);
            e.Handled = true;
        };

        textBlock.Inlines.Add(hyperlink);
        parent.Children.Add(textBlock);
    }

    private void RenderImage(HtmlNode node, Panel parent)
    {
        var src = node.GetAttribute("src", "");
        var alt = node.GetAttribute("alt", "Image");

        if (string.IsNullOrWhiteSpace(src))
        {
            // Show alt text if no src
            var textBlock = new TextBlock
            {
                Text = $"[Image: {alt}]",
                FontSize = 14,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 5, 0, 5)
            };
            parent.Children.Add(textBlock);
            return;
        }

        try
        {
            var image = new Image
            {
                MaxWidth = 800,
                MaxHeight = 600,
                Margin = new Thickness(0, 5, 0, 5),
                Stretch = Stretch.Uniform
            };

            // Try to load image with lazy loading and caching
            if (Uri.TryCreate(src, UriKind.Absolute, out var uri))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Cache to memory
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // Performance optimization
                // Decode at reduced size for performance
                bitmap.DecodePixelWidth = 800; // Max width
                bitmap.EndInit();
                bitmap.Freeze(); // Make thread-safe and improve performance
                image.Source = bitmap;
                parent.Children.Add(image);
            }
            else
            {
                // Invalid URL, show alt text
                var textBlock = new TextBlock
                {
                    Text = $"[Image: {alt}]",
                    FontSize = 14,
                    Foreground = Brushes.Gray
                };
                parent.Children.Add(textBlock);
            }
        }
        catch
        {
            // Failed to load image, show alt text
            var textBlock = new TextBlock
            {
                Text = $"[Image: {alt}]",
                FontSize = 14,
                Foreground = Brushes.Gray
            };
            parent.Children.Add(textBlock);
        }
    }

    private void RenderBreak(HtmlNode node, Panel parent)
    {
        parent.Children.Add(new TextBlock { Height = 5 });
    }

    private void RenderHorizontalRule(HtmlNode node, Panel parent)
    {
        var separator = new Separator
        {
            Margin = new Thickness(0, 10, 0, 10),
            Background = Brushes.Gray
        };
        parent.Children.Add(separator);
    }

    private void RenderUnorderedList(HtmlNode node, Panel parent)
    {
        var listPanel = new StackPanel
        {
            Margin = new Thickness(20, 5, 0, 5)
        };
        foreach (var child in node.Children.Where(c => c.Tag == "li"))
        {
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };
            itemPanel.Children.Add(new TextBlock { Text = "• ", FontSize = 14, Margin = new Thickness(0, 0, 5, 0) });
            var contentPanel = new StackPanel();
            RenderChildren(child, contentPanel);
            itemPanel.Children.Add(contentPanel);
            listPanel.Children.Add(itemPanel);
        }
        parent.Children.Add(listPanel);
    }

    private void RenderOrderedList(HtmlNode node, Panel parent)
    {
        var listPanel = new StackPanel
        {
            Margin = new Thickness(20, 5, 0, 5)
        };
        var index = 1;
        foreach (var child in node.Children.Where(c => c.Tag == "li"))
        {
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };
            itemPanel.Children.Add(new TextBlock { Text = $"{index}. ", FontSize = 14, Margin = new Thickness(0, 0, 5, 0) });
            var contentPanel = new StackPanel();
            RenderChildren(child, contentPanel);
            itemPanel.Children.Add(contentPanel);
            listPanel.Children.Add(itemPanel);
            index++;
        }
        parent.Children.Add(listPanel);
    }

    private void RenderListItem(HtmlNode node, Panel parent)
    {
        // Handled by list renderers
    }

    private void RenderInput(HtmlNode node, Panel parent)
    {
        var type = node.GetAttribute("type", "text").ToLower();
        var name = node.GetAttribute("name", "");
        var value = node.GetAttribute("value", "");
        var placeholder = node.GetAttribute("placeholder", "");
        var id = node.GetAttribute("id", "");

        if (type == "submit" || type == "button")
        {
            var button = new Button
            {
                Content = value.Length > 0 ? value : "Submit",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(0, 5, 5, 5),
                FontSize = 14
            };
            button.Click += (s, e) => FormSubmitted?.Invoke(this, name);
            parent.Children.Add(button);
        }
        else if (type == "checkbox")
        {
            var checkBox = new CheckBox
            {
                Content = value,
                Margin = new Thickness(0, 5, 0, 5),
                FontSize = 14
            };
            parent.Children.Add(checkBox);
        }
        else if (type == "radio")
        {
            var radioButton = new RadioButton
            {
                Content = value,
                Margin = new Thickness(0, 5, 0, 5),
                FontSize = 14
            };
            parent.Children.Add(radioButton);
        }
        else
        {
            var textBox = new TextBox
            {
                Text = value,
                Width = 300,
                Padding = new Thickness(5),
                Margin = new Thickness(0, 5, 0, 5),
                FontSize = 14,
                Tag = name
            };

            if (!string.IsNullOrEmpty(id))
                textBox.Name = MakeSafeName(id);

            if (!string.IsNullOrEmpty(placeholder))
            {
                // Simple placeholder simulation
                textBox.Foreground = Brushes.Gray;
                textBox.Text = placeholder;
                textBox.GotFocus += (s, e) =>
                {
                    if (textBox.Text == placeholder)
                    {
                        textBox.Text = "";
                        textBox.Foreground = Brushes.Black;
                    }
                };
                textBox.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = placeholder;
                        textBox.Foreground = Brushes.Gray;
                    }
                };
            }

            parent.Children.Add(textBox);
        }
    }

    private void RenderTextArea(HtmlNode node, Panel parent)
    {
        var name = node.GetAttribute("name", "");
        var placeholder = node.GetAttribute("placeholder", "");
        var id = node.GetAttribute("id", "");
        var rows = int.TryParse(node.GetAttribute("rows", "5"), out var r) ? r : 5;
        var cols = int.TryParse(node.GetAttribute("cols", "40"), out var c) ? c : 40;
        var textBox = new TextBox
        {
            Text = GetTextContent(node),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Width = cols * 8,
            Height = rows * 18,
            Padding = new Thickness(5),
            Margin = new Thickness(0, 5, 0, 5),
            FontSize = 14,
            Tag = name
        };
        if (!string.IsNullOrEmpty(id)) textBox.Name = MakeSafeName(id);
        if (!string.IsNullOrEmpty(placeholder) && string.IsNullOrEmpty(textBox.Text))
        {
            textBox.Foreground = Brushes.Gray;
            textBox.Text = placeholder;
            textBox.GotFocus += (s, e) => { if (textBox.Text == placeholder) { textBox.Text = ""; textBox.Foreground = Brushes.Black; } };
            textBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(textBox.Text)) { textBox.Text = placeholder; textBox.Foreground = Brushes.Gray; } };
        }
        parent.Children.Add(textBox);
    }

    private void RenderSelect(HtmlNode node, Panel parent)
    {
        var name = node.GetAttribute("name", "");
        var id = node.GetAttribute("id", "");
        var combo = new ComboBox
        {
            Margin = new Thickness(0, 5, 0, 5),
            Tag = name,
            Width = 300
        };
        if (!string.IsNullOrEmpty(id)) combo.Name = MakeSafeName(id);
        foreach (var opt in node.Children.Where(c => c.Tag == "option"))
        {
            combo.Items.Add(new ComboBoxItem
            {
                Content = GetTextContent(opt),
                Tag = opt.GetAttribute("value", GetTextContent(opt))
            });
        }
        parent.Children.Add(combo);
    }

    private void RenderLabel(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            Text = GetTextContent(node),
            FontSize = 14,
            Margin = new Thickness(0, 3, 5, 0)
        };
        parent.Children.Add(textBlock);
    }

    private static string MakeSafeName(string id)
    {
        foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) id = id.Replace(ch, '_');
        return new string(id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    }

    private void RenderButton(HtmlNode node, Panel parent)
    {
        var button = new Button
        {
            Content = GetTextContent(node),
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(0, 5, 5, 5),
            FontSize = 14
        };
        parent.Children.Add(button);
    }

    private void RenderForm(HtmlNode node, Panel parent)
    {
        var formPanel = new StackPanel
        {
            Margin = new Thickness(0, 10, 0, 10)
        };
        RenderChildren(node, formPanel);
        parent.Children.Add(formPanel);
    }

    private void RenderTable(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            Text = "[Table content - simplified view]",
            FontSize = 14,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 10, 0, 10)
        };
        parent.Children.Add(textBlock);
    }

    private void RenderPreformatted(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            Text = GetTextContent(node),
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 5, 0, 5),
            TextWrapping = TextWrapping.Wrap
        };
        parent.Children.Add(textBlock);
    }

    private void RenderCode(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            Text = GetTextContent(node),
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 13,
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            Padding = new Thickness(3),
            TextWrapping = TextWrapping.Wrap
        };
        parent.Children.Add(textBlock);
    }

    private void RenderBlockquote(HtmlNode node, Panel parent)
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(10, 5, 0, 5),
            Margin = new Thickness(10, 10, 10, 10),
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
        };
        var panel = new StackPanel();
        RenderChildren(node, panel);
        border.Child = panel;
        parent.Children.Add(border);
    }

    private void RenderBold(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void RenderItalic(HtmlNode node, Panel parent)
    {
        var textBlock = new TextBlock
        {
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };
        SetTextContent(textBlock, node);
        parent.Children.Add(textBlock);
    }

    private void SetTextContent(TextBlock textBlock, HtmlNode node)
    {
        textBlock.Text = GetTextContent(node);
    }

    private string GetTextContent(HtmlNode node)
    {
        if (node.IsTextNode)
            return node.TextContent ?? "";

        var text = "";
        foreach (var child in node.Children)
        {
            if (child.IsTextNode)
                text += child.TextContent ?? "";
            else
                text += GetTextContent(child);
        }
        return text;
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

