using System;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace QueryOptimizer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        //Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://QueryOptimizer/logo/query_optimizer_logo.png")));
    }

    private async void SelectSqlFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select SQL file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("SQL files") { Patterns = new[] { "*.sql" } },
                FilePickerFileTypes.All
            }
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            InputQueryEditor.Text = await reader.ReadToEndAsync();
            StatusText.Text = $"Loaded {file.Name}";
        }
        catch (IOException)
        {
            StatusText.Text = $"Could not read {file.Name}";
        }
        catch (UnauthorizedAccessException)
        {
            StatusText.Text = $"Access denied for {file.Name}";
        }
    }

    private void InputQuery_DragOver(object? sender, DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        var isSqlFile = file is not null &&
                        Path.GetExtension(file.Name).Equals(".sql", StringComparison.OrdinalIgnoreCase);

        e.DragEffects = isSqlFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void InputQuery_Drop(object? sender, DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        if (file is null ||
            !file.Path.IsFile ||
            !Path.GetExtension(file.Name).Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            InputQueryEditor.Text = await File.ReadAllTextAsync(file.Path.LocalPath);
            StatusText.Text = $"Loaded {file.Name}";
            e.Handled = true;
        }
        catch (IOException)
        {
            StatusText.Text = $"Could not read {file.Name}";
        }
        catch (UnauthorizedAccessException)
        {
            StatusText.Text = $"Access denied for {file.Name}";
        }
    }
}
