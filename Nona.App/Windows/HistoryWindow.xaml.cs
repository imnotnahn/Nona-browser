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
    private readonly Nona.Storage.NonaDbContext _db;
    public HistoryWindow()
    {
        InitializeComponent();
        _db = ((App)Application.Current).Services.GetRequiredService<Nona.Storage.NonaDbContext>();
        LoadList();
        Filter.TextChanged += (_, __) => LoadList();
    }

    private void LoadList()
    {
        try
        {
            var q = Filter.Text?.Trim() ?? string.Empty;
            
            // Get history items from database
            var historyQuery = _db.History.AsNoTracking();
            
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


