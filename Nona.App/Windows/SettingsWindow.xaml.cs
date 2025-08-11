using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Nona.Storage;
using Nona.Core;
using Nona.Theming;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Linq;
using Microsoft.Win32;

namespace Nona.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private SettingsModel _currentSettings = new();
    private ThemeSchema _currentTheme;

    public SettingsWindow()
    {
        InitializeComponent();
        _settingsStore = ((App)Application.Current).Services.GetRequiredService<ISettingsStore>();
        _themeService = ((App)Application.Current).Services.GetRequiredService<IThemeService>();
        _currentTheme = _themeService.Current;
        
        Loaded += SettingsWindow_Loaded;
        Sidebar.SelectedIndex = 0;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _currentSettings = await _settingsStore.LoadAsync();
        _currentTheme = _themeService.Current;
        ShowGeneralSettings();
    }

    private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sidebar.SelectedItem is not ListBoxItem item) return;
        
        // Get the text from the StackPanel content
        string content = string.Empty;
        if (item.Content is StackPanel stackPanel && stackPanel.Children.Count > 1 && stackPanel.Children[1] is TextBlock textBlock)
        {
            content = textBlock.Text;
        }
        
        switch (content)
        {
            case "General":
                ShowGeneralSettings();
                break;
            case "Privacy & Security":
                ShowPrivacySettings();
                break;
            case "Appearance":
                ShowAppearanceSettings();
                break;
            case "Advanced Theme":
                ShowAdvancedThemeSettings();
                break;
            case "Downloads":
                ShowDownloadsSettings();
                break;
            case "Keyboard Shortcuts":
                ShowKeyboardShortcuts();
                break;
            case "About":
                ShowAboutSettings();
                break;
        }
    }

    private void ShowGeneralSettings()
    {
        var panel = new StackPanel();
        
        // Title
        panel.Children.Add(new TextBlock 
        { 
            Text = "General Settings", 
            FontSize = 18, 
            FontWeight = FontWeights.Bold, 
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentTheme.ForegroundColor))
        });

        // Default Search Engine
        var searchEngineLabel = new TextBlock { Text = "Default Search Engine:", Margin = new Thickness(0, 0, 0, 5) };
        searchEngineLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        panel.Children.Add(searchEngineLabel);
        var searchCombo = new ComboBox 
        { 
            Height = 32,
            Margin = new Thickness(0, 0, 0, 15)
        };
        searchCombo.SetResourceReference(ComboBox.BackgroundProperty, "TextBoxColor");
        searchCombo.SetResourceReference(ComboBox.ForegroundProperty, "ForegroundColor");
        searchCombo.SetResourceReference(ComboBox.BorderBrushProperty, "TextBoxBorderColor");
        
        var searchEngines = new[] { "Google", "Bing", "DuckDuckGo", "Yahoo" };
        foreach (var engine in searchEngines)
            searchCombo.Items.Add(engine);
            
        // Set current selection based on settings
        searchCombo.SelectedItem = _currentSettings.SearchEngine;
        
        searchCombo.SelectionChanged += (s, e) => 
        {
            if (searchCombo.SelectedItem != null)
            {
                _currentSettings.SearchEngine = searchCombo.SelectedItem.ToString() ?? "Bing";
            }
        };
        
        panel.Children.Add(searchCombo);

        // Active Profile
        var activeProfileLabel = new TextBlock { Text = "Active Profile:", Margin = new Thickness(0, 0, 0, 5) };
        activeProfileLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        panel.Children.Add(activeProfileLabel);
        var profileBox = new TextBox 
        { 
            Text = _currentSettings.ActiveProfile, 
            Margin = new Thickness(0, 0, 0, 15),
            Height = 30
        };
        profileBox.SetResourceReference(TextBox.BackgroundProperty, "TextBoxColor");
        profileBox.SetResourceReference(TextBox.ForegroundProperty, "ForegroundColor");
        profileBox.SetResourceReference(TextBox.BorderBrushProperty, "TextBoxBorderColor");
        profileBox.TextChanged += (s, e) => _currentSettings.ActiveProfile = profileBox.Text;
        panel.Children.Add(profileBox);

        ContentHost.Content = panel;
    }

    private void ShowPrivacySettings()
    {
        var panel = new StackPanel();
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "Privacy & Security", 
            FontSize = 18, 
            FontWeight = FontWeights.Bold, 
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentTheme.ForegroundColor))
        });

        // HTTPS Only
        var httpsCheck = new CheckBox 
        { 
            Content = "Force HTTPS connections when possible", 
            IsChecked = _currentSettings.HttpsOnly,
            Margin = new Thickness(0, 0, 0, 15)
        };
        httpsCheck.SetResourceReference(CheckBox.ForegroundProperty, "ForegroundColor");
        httpsCheck.Checked += (s, e) => _currentSettings.HttpsOnly = true;
        httpsCheck.Unchecked += (s, e) => _currentSettings.HttpsOnly = false;
        panel.Children.Add(httpsCheck);

        // Content Blocking toggle
        var blockingCheck = new CheckBox 
        { 
            Content = "Enable content blocking (ads, trackers)", 
            IsChecked = _currentSettings.EnableBlocking,
            Margin = new Thickness(0, 0, 0, 15)
        };
        blockingCheck.SetResourceReference(CheckBox.ForegroundProperty, "ForegroundColor");
        blockingCheck.Checked += (s, e) => _currentSettings.EnableBlocking = true;
        blockingCheck.Unchecked += (s, e) => _currentSettings.EnableBlocking = false;
        panel.Children.Add(blockingCheck);

        // Blocking Mode selection
        panel.Children.Add(CreateThemedTextBlock("Blocking Mode:", 12, FontWeights.Normal, new Thickness(0,0,0,5)));
        var modeCombo = new ComboBox { Height = 30, Margin = new Thickness(0,0,0,15) };
        modeCombo.SetResourceReference(ComboBox.BackgroundProperty, "TextBoxColor");
        modeCombo.SetResourceReference(ComboBox.ForegroundProperty, "ForegroundColor");
        modeCombo.SetResourceReference(ComboBox.BorderBrushProperty, "TextBoxBorderColor");
        modeCombo.Items.Add("Off");
        modeCombo.Items.Add("Balanced");
        modeCombo.Items.Add("Strict");
        modeCombo.SelectedItem = string.IsNullOrWhiteSpace(_currentSettings.BlockingMode) ? "Balanced" : _currentSettings.BlockingMode;
        modeCombo.SelectionChanged += (s, e) => _currentSettings.BlockingMode = modeCombo.SelectedItem?.ToString() ?? "Balanced";
        panel.Children.Add(modeCombo);

        ContentHost.Content = panel;
    }

    private void ShowAppearanceSettings()
    {
        var panel = new StackPanel();
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "Appearance", 
            FontSize = 18, 
            FontWeight = FontWeights.Bold, 
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentTheme.ForegroundColor))
        });

        // Theme Selection
        var themeLabel = new TextBlock { Text = "Theme:", Margin = new Thickness(0, 0, 0, 5) };
        themeLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        panel.Children.Add(themeLabel);
        var themeCombo = new ComboBox 
        { 
            Margin = new Thickness(0, 0, 0, 15),
            Height = 30
        };
        themeCombo.SetResourceReference(ComboBox.BackgroundProperty, "TextBoxColor");
        themeCombo.SetResourceReference(ComboBox.ForegroundProperty, "ForegroundColor");
        themeCombo.SetResourceReference(ComboBox.BorderBrushProperty, "TextBoxBorderColor");
        themeCombo.Items.Add("Dark");
        themeCombo.Items.Add("Light");
        themeCombo.Items.Add("Modern");
        themeCombo.SelectedItem = _currentTheme.Name;
        themeCombo.SelectionChanged += (s, e) => 
        {
            var selectedTheme = themeCombo.SelectedItem?.ToString()?.ToLower();
            if (selectedTheme != null)
            {
                var themePath = Path.Combine(AppContext.BaseDirectory, "Assets", "themes", $"{selectedTheme}.json");
                if (File.Exists(themePath))
                {
                    _themeService.LoadFromFile(themePath);
                    _currentTheme = _themeService.Current;
                    _themeService.ApplyToResources(Application.Current.Resources);
                    _currentSettings.ThemeFile = $"Assets/themes/{selectedTheme}.json";
                }
            }
        };
        panel.Children.Add(themeCombo);

        ContentHost.Content = panel;
    }

    private void ShowAdvancedThemeSettings()
    {
        var scrollViewer = new ScrollViewer 
        { 
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(10),
            MaxHeight = 400
        };
        
        var panel = new StackPanel();
        
        var titleBlock = new TextBlock 
        { 
            Text = "Advanced Theme Settings", 
            FontSize = 18, 
            FontWeight = FontWeights.Bold, 
            Margin = new Thickness(0, 0, 0, 20)
        };
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        panel.Children.Add(titleBlock);

        // Create comprehensive advanced theme settings
        panel.Children.Add(CreateAdvancedThemeContent());

        scrollViewer.Content = panel;
        ContentHost.Content = scrollViewer;
    }

    private StackPanel CreateAdvancedThemeContent()
    {
        var container = new StackPanel();
        
        // Color customization section
        var colorHeader = new TextBlock 
        { 
            Text = "Color Customization", 
            FontSize = 14, 
            FontWeight = FontWeights.SemiBold, 
            Margin = new Thickness(0, 0, 0, 10)
        };
        colorHeader.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");
        container.Children.Add(colorHeader);
        
        var colorSettings = new[]
        {
            ("Accent Color", nameof(_currentTheme.AccentColor)),
            ("Background Color", nameof(_currentTheme.BackgroundColor)),
            ("Foreground Color", nameof(_currentTheme.ForegroundColor)),
            ("Button Color", nameof(_currentTheme.ButtonColor)),
            ("Button Hover Color", nameof(_currentTheme.ButtonHoverColor)),
            ("Text Box Color", nameof(_currentTheme.TextBoxColor)),
            ("Menu Color", nameof(_currentTheme.MenuColor)),
            ("Tab Active Color", nameof(_currentTheme.TabActiveColor)),
            ("Tab Inactive Color", nameof(_currentTheme.TabInactiveColor))
        };

        foreach (var (label, propertyName) in colorSettings)
        {
            container.Children.Add(CreateColorSetting(label, propertyName));
        }

        // Layout settings
        var layoutHeader = new TextBlock 
        { 
            Text = "Layout & Appearance", 
            FontSize = 14, 
            FontWeight = FontWeights.SemiBold, 
            Margin = new Thickness(0, 20, 0, 10)
        };
        layoutHeader.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");
        container.Children.Add(layoutHeader);
        
        // Window opacity slider
        container.Children.Add(CreateSliderSetting("Window Opacity", 
            () => _currentTheme.WindowOpacity, 
            (value) => { _currentTheme.WindowOpacity = value; this.Opacity = value; },
            0.5, 1.0, value => $"{(value * 100):F0}%"));

        // Corner radius sliders
        container.Children.Add(CreateSliderSetting("Button Corner Radius", 
            () => _currentTheme.ButtonCornerRadius, 
            (value) => _currentTheme.ButtonCornerRadius = value,
            0, 20, value => $"{value:F0}px"));

        // Font settings
        var typographyHeader = new TextBlock 
        { 
            Text = "Typography", 
            FontSize = 14, 
            FontWeight = FontWeights.SemiBold, 
            Margin = new Thickness(0, 20, 0, 10)
        };
        typographyHeader.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");
        container.Children.Add(typographyHeader);
        
        container.Children.Add(CreateFontSettings());

        // Background image settings
        var backgroundHeader = new TextBlock 
        { 
            Text = "Background Image", 
            FontSize = 14, 
            FontWeight = FontWeights.SemiBold, 
            Margin = new Thickness(0, 20, 0, 10)
        };
        backgroundHeader.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");
        container.Children.Add(backgroundHeader);
        
        container.Children.Add(CreateBackgroundImageSettings());

        return container;
    }

    private StackPanel CreateColorSetting(string label, string propertyName)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        
        var labelBlock = new TextBlock 
        { 
            Text = label, 
            Margin = new Thickness(0, 0, 0, 8),
            FontWeight = FontWeights.Medium
        };
        labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        container.Children.Add(labelBlock);
        
        var colorPanel = new Grid();
        colorPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        colorPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        colorPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        
        var colorBox = new TextBox 
        { 
            Height = 32,
            Text = (string)_currentTheme.GetType().GetProperty(propertyName)?.GetValue(_currentTheme) ?? "#000000",
            Style = (Style)Application.Current.FindResource("ModernTextBoxStyle"),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(colorBox, 0);
        
        var colorButton = new Button 
        { 
            Content = "Choose", 
            Height = 32, 
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)Application.Current.FindResource("ModernButtonStyle")
        };
        Grid.SetColumn(colorButton, 1);
        
        // Color preview
        var colorPreview = new Border 
        { 
            Width = 24, 
            Height = 24, 
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        try 
        {
            var currentColor = (string)_currentTheme.GetType().GetProperty(propertyName)?.GetValue(_currentTheme) ?? "#000000";
            colorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentColor));
        }
        catch 
        {
            colorPreview.Background = Brushes.Black;
        }
        Grid.SetColumn(colorPreview, 2);
        
        colorButton.Click += (s, e) =>
        {
            // Simple color input dialog
            var inputDialog = new Window
            {
                Title = "Choose Color",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "Enter color (e.g., #FF0000 for red):", Margin = new Thickness(0, 0, 0, 10) });
            
            var input = new TextBox { Text = colorBox.Text, Height = 30 };
            panel.Children.Add(input);
            
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var okBtn = new Button { Content = "OK", Width = 60, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var cancelBtn = new Button { Content = "Cancel", Width = 60, Height = 30 };
            
            okBtn.Click += (ss, ee) => { colorBox.Text = input.Text; inputDialog.Close(); };
            cancelBtn.Click += (ss, ee) => inputDialog.Close();
            
            buttonPanel.Children.Add(okBtn);
            buttonPanel.Children.Add(cancelBtn);
            panel.Children.Add(buttonPanel);
            
            inputDialog.Content = panel;
            inputDialog.ShowDialog();
        };
        
        colorBox.TextChanged += (s, e) =>
        {
            try
            {
                var property = _currentTheme.GetType().GetProperty(propertyName);
                property?.SetValue(_currentTheme, colorBox.Text);
                _themeService.ApplyToResources(Application.Current.Resources);
            }
            catch { }
        };
        
        colorPanel.Children.Add(colorBox);
        colorPanel.Children.Add(colorButton);
        colorPanel.Children.Add(colorPreview);
        container.Children.Add(colorPanel);
        
        return container;
    }

    private StackPanel CreateSliderSetting(string label, Func<double> getter, Action<double> setter, 
        double min, double max, Func<double, string> formatter)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        
        // Header with label and value
        var headerPanel = new Grid();
        headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        var headerLabel = new TextBlock 
        { 
            Text = label, 
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        Grid.SetColumn(headerLabel, 0);
        
        var valueLabel = new TextBlock 
        { 
            Text = formatter(getter()), 
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        valueLabel.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");
        Grid.SetColumn(valueLabel, 2);
        
        headerPanel.Children.Add(headerLabel);
        headerPanel.Children.Add(valueLabel);
        container.Children.Add(headerPanel);
        
        var slider = new Slider 
        { 
            Minimum = min, 
            Maximum = max, 
            Value = getter(),
            Margin = new Thickness(0, 8, 0, 0),
            Height = 20,
            IsSnapToTickEnabled = false,
            IsMoveToPointEnabled = true,
            TickFrequency = (max - min) / 100,
            SmallChange = (max - min) / 100,
            LargeChange = (max - min) / 20
        };
        
        // Add smooth value change handling
        bool isUpdating = false;
        slider.ValueChanged += (s, e) => 
        {
            if (isUpdating) return;
            isUpdating = true;
            
            setter(slider.Value);
            valueLabel.Text = formatter(slider.Value);
            _themeService.ApplyToResources(Application.Current.Resources);
            
            isUpdating = false;
        };
        
        container.Children.Add(slider);
        return container;
    }

    private StackPanel CreateFontSettings()
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        
        // Font family
        var fontFamilyLabel = new TextBlock 
        { 
            Text = "Font Family", 
            Margin = new Thickness(0, 0, 0, 5)
        };
        fontFamilyLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        container.Children.Add(fontFamilyLabel);
        
        var fontCombo = new ComboBox 
        { 
            Height = 32, 
            Width = 200
        };
        fontCombo.SetResourceReference(ComboBox.BackgroundProperty, "TextBoxColor");
        fontCombo.SetResourceReference(ComboBox.ForegroundProperty, "ForegroundColor");
        fontCombo.SetResourceReference(ComboBox.BorderBrushProperty, "TextBoxBorderColor");
        var systemFonts = new[] { "Segoe UI", "Arial", "Calibri", "Consolas", "Times New Roman", "Verdana" };
        foreach (var font in systemFonts)
            fontCombo.Items.Add(font);
            
        fontCombo.SelectedItem = _currentTheme.FontFamily;
        fontCombo.SelectionChanged += (s, e) => 
        {
            _currentTheme.FontFamily = fontCombo.SelectedItem?.ToString() ?? "Segoe UI";
            _themeService.ApplyToResources(Application.Current.Resources);
        };
        
        container.Children.Add(fontCombo);
        
        // Font size
        container.Children.Add(CreateSliderSetting("Font Size", 
            () => _currentTheme.FontSize, 
            (value) => _currentTheme.FontSize = (int)value,
            8, 24, value => $"{value:F0}px"));
        
        return container;
    }

    private StackPanel CreateBackgroundImageSettings()
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        
        var bgPathLabel = new TextBlock 
        { 
            Text = "Background Image Path", 
            Margin = new Thickness(0, 0, 0, 5)
        };
        bgPathLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        container.Children.Add(bgPathLabel);
        
        var bgPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var bgBox = new TextBox 
        { 
            Width = 250, 
            Height = 32,
            Text = _currentTheme.BackgroundImagePath,
            Style = (Style)Application.Current.FindResource("ModernTextBoxStyle")
        };
        var bgButton = new Button 
        { 
            Content = "Browse", 
            Width = 70, 
            Height = 32, 
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)Application.Current.FindResource("ModernButtonStyle")
        };
        var clearButton = new Button 
        { 
            Content = "Clear", 
            Width = 60, 
            Height = 32, 
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)Application.Current.FindResource("ModernButtonStyle")
        };
        
        bgButton.Click += (s, e) =>
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (openDialog.ShowDialog() == true)
            {
                bgBox.Text = openDialog.FileName;
                _currentTheme.BackgroundImagePath = openDialog.FileName;
                _themeService.ApplyToResources(Application.Current.Resources);
                UpdateMainWindowBackground();
            }
        };
        
        clearButton.Click += (s, e) =>
        {
            bgBox.Text = "";
            _currentTheme.BackgroundImagePath = "";
            _themeService.ApplyToResources(Application.Current.Resources);
            UpdateMainWindowBackground();
        };
        
        bgBox.TextChanged += (s, e) =>
        {
            _currentTheme.BackgroundImagePath = bgBox.Text;
            _themeService.ApplyToResources(Application.Current.Resources);
            UpdateMainWindowBackground();
        };
        
        bgPanel.Children.Add(bgBox);
        bgPanel.Children.Add(bgButton);
        bgPanel.Children.Add(clearButton);
        container.Children.Add(bgPanel);
        
        // Background opacity
        container.Children.Add(CreateSliderSetting("Background Image Opacity", 
            () => _currentTheme.BackgroundImageOpacity, 
            (value) => { 
                _currentTheme.BackgroundImageOpacity = value; 
                UpdateMainWindowBackground(); 
            },
            0.0, 1.0, value => $"{(value * 100):F0}%"));
        
        return container;
    }

    private void UpdateMainWindowBackground()
    {
        // Find the main window and update its background
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.UpdateBackgroundImage();
    }

    private TextBlock CreateThemedTextBlock(string text, double fontSize = 12, FontWeight? fontWeight = null, Thickness? margin = null, string foregroundResource = "ForegroundColor")
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeights.Normal
        };
        
        if (margin.HasValue)
            textBlock.Margin = margin.Value;
            
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, foregroundResource);
        return textBlock;
    }

    private void ShowDownloadsSettings()
    {
        var panel = new StackPanel();
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "Downloads", 
            FontSize = 18, 
            FontWeight = FontWeights.Bold, 
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentTheme.ForegroundColor))
        });

        var downloadLabel = new TextBlock { Text = "Download settings coming soon...", Margin = new Thickness(0, 0, 0, 15) };
        downloadLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
        panel.Children.Add(downloadLabel);

        ContentHost.Content = panel;
    }

    private void ShowKeyboardShortcuts()
    {
        var panel = new StackPanel();
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "Keyboard Shortcuts", 
            FontSize = 18, 
            FontWeight = FontWeights.Bold, 
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentTheme.ForegroundColor))
        });

        var shortcuts = new[]
        {
            ("New Tab", "Ctrl+T"),
            ("Close Tab", "Ctrl+W"),
            ("Focus Address Bar", "Ctrl+L"),
            ("Refresh Page", "F5 or Ctrl+R"),
            ("Go Back", "Alt+Left or Ctrl+B"),
            ("Go Forward", "Alt+Right"),
            ("Open Downloads", "Ctrl+J"),
            ("Open History", "Ctrl+H"),
            ("Open Command Palette", "Ctrl+K")
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < shortcuts.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var actionText = CreateThemedTextBlock(shortcuts[i].Item1, 12, FontWeights.Normal, new Thickness(0, 5, 0, 5));
            var shortcutText = CreateThemedTextBlock(shortcuts[i].Item2, 12, FontWeights.Bold, new Thickness(0, 5, 0, 5), "AccentColor");
            
            Grid.SetRow(actionText, i);
            Grid.SetColumn(actionText, 0);
            Grid.SetRow(shortcutText, i);
            Grid.SetColumn(shortcutText, 1);
            
            grid.Children.Add(actionText);
            grid.Children.Add(shortcutText);
        }

        panel.Children.Add(grid);

        // Command Palette Info
        panel.Children.Add(CreateThemedTextBlock("Command Palette", 14, FontWeights.Bold, new Thickness(0, 30, 0, 10)));

        var commandPaletteDesc = CreateThemedTextBlock("The Command Palette (Ctrl+K) provides quick access to browser functions. Type to search for commands and press Enter to execute them.", 
            12, FontWeights.Normal, new Thickness(0, 0, 0, 15));
        commandPaletteDesc.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(commandPaletteDesc);

        ContentHost.Content = panel;
    }

    private void ShowAboutSettings()
    {
        var panel = new StackPanel();
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "About Nona Browser", 
            FontSize = 18, 
            FontWeight = FontWeights.Bold, 
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentTheme.ForegroundColor))
        });

        panel.Children.Add(CreateThemedTextBlock("Nona Browser v1.0", 14, FontWeights.Bold, new Thickness(0, 0, 0, 10)));

        var aboutDesc = CreateThemedTextBlock("A modern, privacy-focused web browser built with WPF and WebView2.", 
            12, FontWeights.Normal, new Thickness(0, 0, 0, 20));
        aboutDesc.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(aboutDesc);

        ContentHost.Content = panel;
    }

    private async Task ApplySettingsAsync()
    {
        await _settingsStore.SaveAsync(_currentSettings);
        var themePath = Path.Combine(AppContext.BaseDirectory, "Assets", "themes", "current.json");
        _themeService.SaveToFile(themePath);

        // Apply ad blocking mode immediately
        try
        {
            var rules = ((App)Application.Current).Services.GetRequiredService<Nona.Security.IRulesEngine>();
            var mode = (_currentSettings.EnableBlocking, _currentSettings.BlockingMode.ToLowerInvariant()) switch
            {
                (false, _) => Nona.Security.BlockingMode.Off,
                (true, "strict") => Nona.Security.BlockingMode.Strict,
                (true, "balanced") => Nona.Security.BlockingMode.Balanced,
                _ => Nona.Security.BlockingMode.Balanced
            };
            rules.Mode = mode;
        }
        catch { }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        await ApplySettingsAsync();
    }

    private async void Ok_Click(object sender, RoutedEventArgs e)
    {
        await ApplySettingsAsync();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
