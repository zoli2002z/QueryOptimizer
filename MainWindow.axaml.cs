using System;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Controls;

namespace QueryOptimizer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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