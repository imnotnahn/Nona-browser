using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Nona.App.Windows;

public partial class ProfilesWindow : Window
{
    private readonly string _baseDir;
    public ProfilesWindow()
    {
        InitializeComponent();
        _baseDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Nona");
        LoadList();
    }

    private void LoadList()
    {
        if (!Directory.Exists(_baseDir)) Directory.CreateDirectory(_baseDir);
        var profiles = Directory.GetDirectories(_baseDir).Select(Path.GetFileName).ToList();
        if (!profiles.Contains("Default")) profiles.Insert(0, "Default");
        List.ItemsSource = profiles;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = "Profile_" + System.DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(Path.Combine(_baseDir, name));
        LoadList();
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        var selected = List.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selected)) return;
        MessageBox.Show($"Selected profile: {selected} (restart app to apply)");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}


