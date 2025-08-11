using Microsoft.EntityFrameworkCore;
using Nona.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Nona.Storage;

public sealed class NonaDbContext : DbContext
{
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<HistoryEntry> History => Set<HistoryEntry>();
    public DbSet<PageThumbnail> Thumbnails => Set<PageThumbnail>();
    public DbSet<DownloadItem> Downloads => Set<DownloadItem>();

    private readonly string _databasePath;

    public NonaDbContext()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", "Default");
        Directory.CreateDirectory(baseDir);
        _databasePath = Path.Combine(baseDir, "nona.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_databasePath};Cache=Shared");
        optionsBuilder.EnableSensitiveDataLogging(false);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bookmark>().HasKey(b => b.Id);
        modelBuilder.Entity<Bookmark>().HasIndex(b => b.ParentFolderId);
        modelBuilder.Entity<HistoryEntry>().HasKey(h => h.Id);
        modelBuilder.Entity<HistoryEntry>().HasIndex(h => h.VisitedAt);
        modelBuilder.Entity<PageThumbnail>().HasKey(t => t.Url);
        modelBuilder.Entity<DownloadItem>().HasKey(d => d.Id);
        base.OnModelCreating(modelBuilder);
    }
}

public interface IHistoryRepository
{
    Task AddAsync(string url, string? title);
}

public sealed class HistoryRepository : IHistoryRepository
{
    private readonly NonaDbContext _db;
    public HistoryRepository(NonaDbContext db) { _db = db; }

    public async Task AddAsync(string url, string? title)
    {
        _db.History.Add(new HistoryEntry { Url = url, Title = title ?? string.Empty, VisitedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();
    }
}

public sealed class PageThumbnail
{
    public string Url { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IThumbnailRepository
{
    Task SaveAsync(string url, string filePath);
    Task<string?> GetPathAsync(string url);
}

public sealed class ThumbnailRepository : IThumbnailRepository
{
    private readonly NonaDbContext _db;
    public ThumbnailRepository(NonaDbContext db) { _db = db; }

    public async Task SaveAsync(string url, string filePath)
    {
        var entity = await _db.Thumbnails.FindAsync(url);
        if (entity == null)
        {
            entity = new PageThumbnail { Url = url, FilePath = filePath, CreatedAt = DateTimeOffset.UtcNow };
            _db.Thumbnails.Add(entity);
        }
        else
        {
            entity.FilePath = filePath;
            entity.CreatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetPathAsync(string url)
    {
        var entity = await _db.Thumbnails.FindAsync(url);
        return entity?.FilePath;
    }
}

public sealed class DownloadItem
{
    public long Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long? TotalBytes { get; set; }
    public string State { get; set; } = "Unknown";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IBookmarksRepository
{
    Task AddAsync(string title, string url, int? parentFolderId = null);
    Task<List<Bookmark>> ListAsync(int? parentFolderId = null);
    Task DeleteAsync(int id);
    Task<Bookmark?> FindByUrlAsync(string url);
}

public sealed class BookmarksRepository : IBookmarksRepository
{
    private readonly NonaDbContext _db;
    public BookmarksRepository(NonaDbContext db) { _db = db; }

    public async Task AddAsync(string title, string url, int? parentFolderId = null)
    {
        _db.Bookmarks.Add(new Bookmark { Title = title, Url = url, ParentFolderId = parentFolderId });
        await _db.SaveChangesAsync();
    }

    public async Task<List<Bookmark>> ListAsync(int? parentFolderId = null)
    {
        if (parentFolderId == null) return await _db.Bookmarks.ToListAsync();
        return await _db.Bookmarks.AsQueryable().Where(b => b.ParentFolderId == parentFolderId).ToListAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var bookmark = await _db.Bookmarks.FindAsync(id);
        if (bookmark != null)
        {
            _db.Bookmarks.Remove(bookmark);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Bookmark?> FindByUrlAsync(string url)
    {
        return await _db.Bookmarks.FirstOrDefaultAsync(b => b.Url == url);
    }
}

public interface ISettingsStore
{
    Task<SettingsModel> LoadAsync();
    Task SaveAsync(SettingsModel model);
}

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _settingsPath;

    public JsonSettingsStore()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nona", "Default");
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, "settings.json");
    }

    public async Task<SettingsModel> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new SettingsModel();
        }
        await using var fs = File.OpenRead(_settingsPath);
        var model = await JsonSerializer.DeserializeAsync<SettingsModel>(fs) ?? new SettingsModel();
        return model;
    }

    public async Task SaveAsync(SettingsModel model)
    {
        await using var fs = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(fs, model, new JsonSerializerOptions { WriteIndented = true });
    }
}
