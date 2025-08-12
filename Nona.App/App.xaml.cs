﻿using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Nona.Core;
using Nona.Storage;
using Nona.Security;
using Nona.Theming;
using Nona.Engine;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using System;

namespace Nona.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    public IServiceProvider Services => _host!.Services;

    protected override void OnStartup(StartupEventArgs e)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.File("nona.log")
            .CreateLogger();

        // Global exception logging to avoid silent process exits
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            try { Log.Error(ex.ExceptionObject as Exception, "UnhandledException"); } catch { }
        };
        DispatcherUnhandledException += (s, ex) =>
        {
            try { Log.Error(ex.Exception, "DispatcherUnhandledException"); } catch { }
            ex.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (s, ex) =>
        {
            try { Log.Error(ex.Exception, "UnobservedTaskException"); } catch { }
            ex.SetObserved();
        };

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TabManager>().AddSingleton<ITabManager>(sp => sp.GetRequiredService<TabManager>());
                // Use DbContextFactory to avoid long-lived DbContext instances and improve responsiveness
                services.AddDbContextFactory<NonaDbContext>();
                services.AddSingleton<IHistoryRepository, HistoryRepository>();
                services.AddSingleton<IBookmarksRepository, BookmarksRepository>();
                services.AddSingleton<IThumbnailRepository, ThumbnailRepository>();
                services.AddSingleton<ISettingsStore, JsonSettingsStore>();
                services.AddSingleton<IRulesEngine, ExtendedRulesEngine>();
                services.AddSingleton<IHttpsOnlyUpgrader, HttpsOnlyUpgrader>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IWebEngine, WebEngine>();
                services.AddSingleton<IThemeWatcher, ThemeWatcher>();
                services.AddSingleton<IDnsResolver, DohResolver>();
                services.AddSingleton<IDownloadsManager, DownloadsManager>();
                services.AddSingleton<ICommandRegistry, CommandRegistry>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();

        base.OnStartup(e);
        // Ensure database schema exists
        try
        {
            using var scope = _host.Services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NonaDbContext>>();
            using var db = dbFactory.CreateDbContext();
            db.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EnsureCreated failed");
        }
        // Apply theme from saved file or default
        var theme = _host.Services.GetRequiredService<IThemeService>();
        var currentThemePath = Path.Combine(AppContext.BaseDirectory, "Assets", "themes", "current.json");
        var defaultThemePath = Path.Combine(AppContext.BaseDirectory, "Assets", "themes", "modern.json");
        
        // Load current theme if exists, otherwise load default
        var themePath = File.Exists(currentThemePath) ? currentThemePath : defaultThemePath;
        if (File.Exists(themePath))
        {
            theme.LoadFromFile(themePath);
            theme.ApplyToResources(Current.Resources);
        }

        var main = _host.Services.GetRequiredService<MainWindow>();
        main.Show();

        // Start theme watcher for live preview
        try { _host.Services.GetRequiredService<IThemeWatcher>().StartWatching(AppContext.BaseDirectory); } catch { }

        // Pre-warm WebView2 environment on background to reduce first-tab cost
        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var engine = _host.Services.GetRequiredService<IWebEngine>();
                    await engine.GetEnvironmentAsync();
                }
                catch { }
            });
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

