using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Threading;

namespace Nona.Theming;

public sealed class ThemeSchema
{
    public string Name { get; set; } = "Custom";
    public string AccentColor { get; set; } = "#0078D4";
    public string BackgroundColor { get; set; } = "#1E1E1E";
    public string ForegroundColor { get; set; } = "#FFFFFF";
    public string TabActiveColor { get; set; } = "#2D2D2D";
    public string TabInactiveColor { get; set; } = "#202020";
    public string ButtonColor { get; set; } = "#3C3C3C";
    public string ButtonHoverColor { get; set; } = "#4A4A4A";
    public string ButtonBorderColor { get; set; } = "#5A5A5A";
    public string TextBoxColor { get; set; } = "#2D2D2D";
    public string TextBoxBorderColor { get; set; } = "#5A5A5A";
    public string MenuColor { get; set; } = "#2D2D2D";
    public string MenuHoverColor { get; set; } = "#3C3C3C";
    public string StatusBarColor { get; set; } = "#1E1E1E";
    public string BookmarkBarColor { get; set; } = "#252525";
    public string WindowBorderColor { get; set; } = "#5A5A5A";
    public double WindowOpacity { get; set; } = 1.0;
    public double ButtonCornerRadius { get; set; } = 4.0;
    public double WindowCornerRadius { get; set; } = 8.0;
    public int FontSize { get; set; } = 12;
    public string FontFamily { get; set; } = "Segoe UI";
    public string BackgroundImagePath { get; set; } = "";
    public double BackgroundImageOpacity { get; set; } = 0.1;
    public string BackgroundImageStretch { get; set; } = "UniformToFill";
}

public interface IThemeService
{
    ThemeSchema Current { get; }
    void LoadFromFile(string path);
    void SaveToFile(string path);
    void ApplyToResources(ResourceDictionary resources);
}

public sealed class ThemeService : IThemeService
{
    public ThemeSchema Current { get; private set; } = new();

    public void LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var theme = JsonSerializer.Deserialize<ThemeSchema>(json) ?? new ThemeSchema();
        Current = theme;
    }

    public void SaveToFile(string path)
    {
        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public void ApplyToResources(ResourceDictionary resources)
    {
        // Helper method to safely convert colors
        System.Windows.Media.SolidColorBrush SafeConvertColor(string colorString, string fallback = "#000000")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(colorString))
                    colorString = fallback;
                return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString));
            }
            catch
            {
                return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fallback));
            }
        }

        // Convert color strings to SolidColorBrush objects safely
        resources["AccentColor"] = SafeConvertColor(Current.AccentColor, "#007ACC");
        resources["BackgroundColor"] = SafeConvertColor(Current.BackgroundColor, "#FFFFFF");
        resources["ForegroundColor"] = SafeConvertColor(Current.ForegroundColor, "#000000");
        resources["TabActiveColor"] = SafeConvertColor(Current.TabActiveColor, "#FFFFFF");
        resources["TabInactiveColor"] = SafeConvertColor(Current.TabInactiveColor, "#F0F0F0");
        resources["ButtonColor"] = SafeConvertColor(Current.ButtonColor, "#E1E1E1");
        resources["ButtonHoverColor"] = SafeConvertColor(Current.ButtonHoverColor, "#D4D4D4");
        resources["ButtonBorderColor"] = SafeConvertColor(Current.ButtonBorderColor, "#CCCCCC");
        resources["TextBoxColor"] = SafeConvertColor(Current.TextBoxColor, "#FFFFFF");
        resources["TextBoxBorderColor"] = SafeConvertColor(Current.TextBoxBorderColor, "#CCCCCC");
        resources["MenuColor"] = SafeConvertColor(Current.MenuColor, "#F8F8F8");
        resources["MenuHoverColor"] = SafeConvertColor(Current.MenuHoverColor, "#E5E5E5");
        resources["StatusBarColor"] = SafeConvertColor(Current.StatusBarColor, "#F0F0F0");
        resources["BookmarkBarColor"] = SafeConvertColor(Current.BookmarkBarColor, "#F8F8F8");
        resources["WindowBorderColor"] = SafeConvertColor(Current.WindowBorderColor, "#CCCCCC");
        
        // Non-color properties
        resources["WindowOpacity"] = Math.Max(0.1, Math.Min(1.0, Current.WindowOpacity));
        resources["ButtonCornerRadius"] = new CornerRadius(Math.Max(0, Math.Min(50, Current.ButtonCornerRadius)));
        resources["WindowCornerRadius"] = new CornerRadius(Math.Max(0, Math.Min(50, Current.WindowCornerRadius)));
        resources["FontSize"] = (double)Math.Max(8, Math.Min(72, Current.FontSize));
        resources["FontFamily"] = new System.Windows.Media.FontFamily(string.IsNullOrWhiteSpace(Current.FontFamily) ? "Segoe UI" : Current.FontFamily);
        resources["BackgroundImagePath"] = Current.BackgroundImagePath ?? "";
        resources["BackgroundImageOpacity"] = Math.Max(0.0, Math.Min(1.0, Current.BackgroundImageOpacity));
        
        try
        {
            resources["BackgroundImageStretch"] = Enum.Parse<System.Windows.Media.Stretch>(Current.BackgroundImageStretch);
        }
        catch
        {
            resources["BackgroundImageStretch"] = System.Windows.Media.Stretch.UniformToFill;
        }
    }
}

public interface IThemeWatcher
{
    void StartWatching(string baseDir);
}

public sealed class ThemeWatcher : IThemeWatcher
{
    private readonly IThemeService _theme;
    private FileSystemWatcher? _watcher;
    public ThemeWatcher(IThemeService theme) { _theme = theme; }

    public void StartWatching(string baseDir)
    {
        var path = Path.Combine(baseDir, "Assets", "themes");
        if (!Directory.Exists(path)) return;
        _watcher = new FileSystemWatcher(path, "*.json") { IncludeSubdirectories = false, EnableRaisingEvents = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        TryReload(e.FullPath);
    }
    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        TryReload(e.FullPath);
    }
    private void TryReload(string file)
    {
        try
        {
            // small delay for file write completion
            Thread.Sleep(50);
            _theme.LoadFromFile(file);
            Application.Current?.Dispatcher.Invoke(() => _theme.ApplyToResources(Application.Current.Resources));
        }
        catch { }
    }
}
