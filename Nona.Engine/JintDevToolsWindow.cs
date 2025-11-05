using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Nona.Engine
{
    public class JintDevToolsWindow : Window
    {
        private readonly HtmlDocument _document;
        private readonly JintWebEngine _jsEngine;
        private readonly List<string> _networkLog;
        private readonly TreeView _domTree;
        private readonly TextBox _consoleInput;
        private readonly TextBlock _consoleOutput;
        private readonly ListBox _networkList;

        public JintDevToolsWindow(HtmlDocument document, JintWebEngine jsEngine, List<string> networkLog)
        {
            _document = document;
            _jsEngine = jsEngine;
            _networkLog = networkLog;

            Title = "DevTools - Nona Browser (Jint)";
            Width = 1000;
            Height = 700;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            Foreground = Brushes.White;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top: TabControl for DOM/Network
            var tabControl = new TabControl
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            // DOM Tab
            _domTree = new TreeView
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            var domTab = new TabItem { Header = "DOM Tree", Content = new ScrollViewer { Content = _domTree } };
            tabControl.Items.Add(domTab);

            // Network Tab
            _networkList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            };
            var networkTab = new TabItem { Header = "Network", Content = new ScrollViewer { Content = _networkList } };
            tabControl.Items.Add(networkTab);

            Grid.SetRow(tabControl, 0);
            mainGrid.Children.Add(tabControl);

            // Bottom: Console
            var consolePanel = new DockPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
                LastChildFill = true
            };

            var consoleLabel = new TextBlock
            {
                Text = "Console (Jint)",
                Foreground = Brushes.LightGray,
                Padding = new Thickness(5),
                FontWeight = FontWeights.Bold
            };
            DockPanel.SetDock(consoleLabel, Dock.Top);
            consolePanel.Children.Add(consoleLabel);

            _consoleOutput = new TextBlock
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
                Foreground = Brushes.LightGreen,
                Padding = new Thickness(5),
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            };
            var consoleScroll = new ScrollViewer { Content = _consoleOutput, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            DockPanel.SetDock(consoleScroll, Dock.Top);
            consolePanel.Children.Add(consoleScroll);

            _consoleInput = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = Brushes.Gray,
                Padding = new Thickness(5),
                FontFamily = new FontFamily("Consolas")
            };
            _consoleInput.KeyDown += ConsoleInput_KeyDown;
            DockPanel.SetDock(_consoleInput, Dock.Bottom);
            consolePanel.Children.Add(_consoleInput);

            Grid.SetRow(consolePanel, 1);
            mainGrid.Children.Add(consolePanel);

            Content = mainGrid;

            Loaded += (s, e) =>
            {
                BuildDomTree();
                LoadNetworkLog();
            };
        }

        private void BuildDomTree()
        {
            _domTree.Items.Clear();
            if (_document?.Root != null)
            {
                var rootItem = CreateTreeItem(_document.Root);
                _domTree.Items.Add(rootItem);
            }
        }

        private TreeViewItem CreateTreeItem(HtmlNode node)
        {
            var item = new TreeViewItem
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };

            var header = new TextBlock();
            header.Inlines.Add(new System.Windows.Documents.Run($"<{node.Tag}>")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(136, 176, 255))
            });

            if (node.Attributes.ContainsKey("id"))
            {
                header.Inlines.Add(new System.Windows.Documents.Run($" id=\"{node.Attributes["id"]}\"")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254))
                });
            }

            if (node.Attributes.ContainsKey("class"))
            {
                header.Inlines.Add(new System.Windows.Documents.Run($" class=\"{node.Attributes["class"]}\"")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(206, 145, 120))
                });
            }

            if (!string.IsNullOrWhiteSpace(node.TextContent) && node.Children.Count == 0)
            {
                var trimmed = node.TextContent.Trim();
                if (trimmed.Length > 50) trimmed = trimmed.Substring(0, 50) + "...";
                header.Inlines.Add(new System.Windows.Documents.Run($" \"{trimmed}\"")
                {
                    Foreground = Brushes.LightGray
                });
            }

            item.Header = header;

            foreach (var child in node.Children)
            {
                item.Items.Add(CreateTreeItem(child));
            }

            return item;
        }

        private void LoadNetworkLog()
        {
            _networkList.Items.Clear();
            foreach (var log in _networkLog)
            {
                var textBlock = new TextBlock
                {
                    Text = log,
                    Padding = new Thickness(5),
                    Foreground = log.Contains("[ERROR]") ? Brushes.Red :
                                 log.Contains("[WARN]") ? Brushes.Yellow :
                                 log.Contains("[LOG]") ? Brushes.LightGreen :
                                 Brushes.White
                };
                _networkList.Items.Add(textBlock);
            }

            if (_networkList.Items.Count > 0)
            {
                _networkList.ScrollIntoView(_networkList.Items[_networkList.Items.Count - 1]);
            }
        }

        private void ConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var code = _consoleInput.Text;
                _consoleInput.Clear();

                if (string.IsNullOrWhiteSpace(code)) return;

                _consoleOutput.Text += $"> {code}\n";

                try
                {
                    var result = _jsEngine.Evaluate(code);
                    var resultText = result == null ? "null" :
                                     result is Jint.Native.JsValue jsValue ? jsValue.ToString() :
                                     result.ToString();
                    _consoleOutput.Text += $"{resultText}\n";
                }
                catch (Jint.Runtime.JavaScriptException jsEx)
                {
                    _consoleOutput.Text += $"[JS ERROR] {jsEx.Error}: {jsEx.Message}\n";
                }
                catch (Exception ex)
                {
                    _consoleOutput.Text += $"[ERROR] {ex.Message}\n";
                }

                _consoleOutput.Text += "\n";
            }
        }
    }
}

