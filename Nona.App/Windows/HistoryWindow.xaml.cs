using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Nona.App.Windows;

public sealed class HistoryItemVM
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
}

public partial class HistoryWindow : Window
{
    private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<Nona.Storage.NonaDbContext> _dbFactory;
    public HistoryWindow()
    {
        InitializeComponent();
        _dbFactory = ((App)Application.Current).Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Nona.Storage.NonaDbContext>>();
        LoadList();
        // Debounce filter change to avoid frequent DB queries
        System.Windows.Threading.DispatcherTimer? timer = null;
        Filter.TextChanged += (_, __) =>
        {
            timer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Stop();
            timer.Tick -= Timer_Tick;
            timer.Tick += Timer_Tick;
            timer.Start();
        };
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (sender is System.Windows.Threading.DispatcherTimer t)
        {
            t.Stop();
            t.Tick -= Timer_Tick;
        }
        LoadList();
    }

    private void LoadList()
    {
        try
        {
            var q = Filter.Text?.Trim() ?? string.Empty;
            
            // Get history items from database
            using var db = _dbFactory.CreateDbContext();
            var historyQuery = db.History.AsNoTracking();
            
            if (!string.IsNullOrEmpty(q))
            {
                historyQuery = historyQuery.Where(h => 
                    (h.Title != null && h.Title.Contains(q)) || 
                    h.Url.Contains(q));
            }
            
            var hist = historyQuery
                .OrderByDescending(h => h.Id) // Use Id instead of VisitedAt to avoid SQLite DateTimeOffset issue
                .Take(500)
                .ToList();
            
            var vms = hist.Select(h => new HistoryItemVM
            {
                Title = string.IsNullOrWhiteSpace(h.Title) ? h.Url : h.Title,
                Url = h.Url,
                ThumbnailPath = null // Skip thumbnail lookup for now to avoid performance issues
            }).ToList();
            
            List.ItemsSource = vms;
        }
        catch (System.Exception ex)
        {
            // If there's an error, show empty list
            List.ItemsSource = new List<HistoryItemVM>();
            System.Diagnostics.Debug.WriteLine($"History load error: {ex.Message}");
        }
    }
}


