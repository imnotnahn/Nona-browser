using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Nona.App.Windows;

public class CommandItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public System.Action Action { get; set; } = () => { };
}

public partial class CommandPaletteWindow : Window
{
    private readonly Nona.Engine.ICommandRegistry _registry;
    private List<CommandItem> _allCommands = new();

    public CommandPaletteWindow()
    {
        InitializeComponent();
        _registry = ((App)Application.Current).Services.GetRequiredService<Nona.Engine.ICommandRegistry>();
        
        // Load all commands
        _allCommands = _registry.List()
            .Select(cmd => new CommandItem 
            { 
                Id = cmd.id, 
                Title = cmd.title, 
                Action = cmd.action 
            })
            .ToList();
        
        Refresh();
        Query.TextChanged += (_, __) => Refresh();
        Query.KeyDown += Query_KeyDown;
        List.KeyDown += List_KeyDown;
        
        // Focus the query box when window opens
        Loaded += (_, __) => Query.Focus();
        
        // Handle escape key to close
        KeyDown += (s, e) => 
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    private void Query_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Down:
                if (List.Items.Count > 0)
                {
                    List.Focus();
                    List.SelectedIndex = 0;
                }
                e.Handled = true;
                break;
            case Key.Up:
                if (List.Items.Count > 0)
                {
                    List.Focus();
                    List.SelectedIndex = List.Items.Count - 1;
                }
                e.Handled = true;
                break;
        }
    }

    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Up when List.SelectedIndex == 0:
                Query.Focus();
                e.Handled = true;
                break;
        }
    }

    private void Refresh()
    {
        var q = Query.Text?.Trim() ?? string.Empty;
        var filteredCommands = _allCommands;
        
        if (!string.IsNullOrEmpty(q))
        {
            filteredCommands = _allCommands
                .Where(cmd => 
                    cmd.Title.Contains(q, System.StringComparison.OrdinalIgnoreCase) ||
                    cmd.Id.Contains(q, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        
        List.ItemsSource = filteredCommands;
        
        // Auto-select first item
        if (filteredCommands.Count > 0)
        {
            List.SelectedIndex = 0;
        }
    }

    private void ExecuteSelected()
    {
        var selectedCommand = List.SelectedItem as CommandItem;
        if (selectedCommand == null) return;
        
        try
        {
            selectedCommand.Action?.Invoke();
            Close();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error executing command: {ex.Message}", "Command Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => ExecuteSelected();
}


