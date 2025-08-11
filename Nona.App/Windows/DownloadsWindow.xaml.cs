using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Nona.App.Windows;

public partial class DownloadsWindow : Window
{
    private readonly Nona.Storage.NonaDbContext _db;
    public DownloadsWindow()
    {
        InitializeComponent();
        _db = ((App)Application.Current).Services.GetRequiredService<Nona.Storage.NonaDbContext>();
        LoadGrid();
    }

    private void LoadGrid()
    {
        Grid.ItemsSource = _db.Downloads.AsNoTracking().AsEnumerable().OrderByDescending(d => d.CreatedAt).ToList();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var item = Grid.SelectedItem as Nona.Storage.DownloadItem;
        if (item == null) return;
        var dir = Path.GetDirectoryName(item.FilePath);
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
        {
            Process.Start("explorer.exe", dir);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}


