using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Generic;

namespace Nona.Core;

public enum NavigationState
{
    Idle,
    Loading,
    Completed,
    Failed
}

public sealed class TabModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "New Tab";
    public string Address { get; set; } = "about:blank";
    public NavigationState State { get; set; } = NavigationState.Idle;
    public double ZoomFactor { get; set; } = 1.0;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class Bookmark
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
}

public sealed class HistoryEntry
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset VisitedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Profile
{
    public string Name { get; init; } = "Default";
    public string DataDir { get; init; } = string.Empty;
}

public interface ITabManager
{
    ObservableCollection<TabModel> Tabs { get; }
    TabModel? ActiveTab { get; }
    TabModel NewTab(string? address = null);
    void CloseTab(TabModel tab);
    void Activate(TabModel tab);
}

public interface IHistoryService
{
    Task AddVisitAsync(string url, string? title);
}

public interface IBookmarksService
{
    Task AddAsync(string title, string url, int? parentFolderId = null);
}

public interface ISettingsService
{
    string DefaultSearchTemplate { get; set; }
    bool HttpsOnly { get; set; }
    bool EnableBlocking { get; set; }
    Task SaveAsync();
}

public sealed class SettingsModel
{
    public string DefaultSearchTemplate { get; set; } = "https://www.bing.com/search?q={query}";
    public string SearchEngine { get; set; } = "Bing";
    public bool HttpsOnly { get; set; } = true;
    public bool EnableBlocking { get; set; } = true;
    public string BlockingMode { get; set; } = "Balanced";
    public string ThemeFile { get; set; } = "Assets/themes/dark.json";
    public string ActiveProfile { get; set; } = "Default";
    public bool TelemetryOptIn { get; set; } = false;
    public string? DohProvider { get; set; } = "cloudflare"; // cloudflare, google, quad9 or null
}

public sealed class SessionTab
{
    public string Address { get; set; } = "about:blank";
}

public sealed class SessionState
{
    public List<SessionTab> Tabs { get; set; } = new();
    public int ActiveIndex { get; set; }
}

public interface IProfileManager
{
    string ActiveProfile { get; }
    string GetProfileDataDir(string profileName);
}

public sealed class ProfileManager : IProfileManager
{
    public string ActiveProfile { get; }
    public ProfileManager(string profileName)
    {
        ActiveProfile = string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName;
    }
    public string GetProfileDataDir(string profileName)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(baseDir, "Nona", profileName);
    }
}

public interface ITelemetryService
{
    void RecordNavigation(string url, TimeSpan durationMs, bool success);
}

public sealed class TabManager : ITabManager
{
    public ObservableCollection<TabModel> Tabs { get; } = new();
    public TabModel? ActiveTab { get; private set; }

    public TabModel NewTab(string? address = null)
    {
        var tab = new TabModel { Address = address ?? "about:blank" };
        Tabs.Add(tab);
        Activate(tab);
        return tab;
    }

    public void CloseTab(TabModel tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;
        Tabs.RemoveAt(index);
        if (ActiveTab == tab)
        {
            ActiveTab = Tabs.Count > 0 ? Tabs[Math.Max(0, index - 1)] : null;
        }
    }

    public void Activate(TabModel tab)
    {
        if (!Tabs.Contains(tab)) return;
        ActiveTab = tab;
    }
}
