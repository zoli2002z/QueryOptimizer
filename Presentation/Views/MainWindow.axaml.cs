using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using QueryOptimizer.Optimization;
using QueryOptimizer.Presentation.ViewModels;

namespace QueryOptimizer.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private IStorageFile? _savedSqlFile;
    private bool _isSyncingDiffScroll;

    public MainWindow() : this(new MainWindowViewModel(new SqlOptimizationEngine(), new DiffPreviewMapper()))
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.OptimizationCompleted += (_, _) => _savedSqlFile = null;
        DataContext = _viewModel;
    }

    private void DiffScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingDiffScroll || sender is not ScrollViewer source) return;

        var target = ReferenceEquals(source, InputDiffScrollViewer)
            ? OutputDiffScrollViewer
            : InputDiffScrollViewer;
        if (target.Offset == source.Offset) return;

        _isSyncingDiffScroll = true;
        target.Offset = source.Offset;
        _isSyncingDiffScroll = false;
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
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            _viewModel.LoadInput(await reader.ReadToEndAsync(), file.Name);
        }
        catch (IOException)
        {
            _viewModel.SetStatus($"Could not read {file.Name}");
        }
        catch (UnauthorizedAccessException)
        {
            _viewModel.SetStatus($"Access denied for {file.Name}");
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
        if (file is null || !file.Path.IsFile ||
            !Path.GetExtension(file.Name).Equals(".sql", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            _viewModel.LoadInput(await File.ReadAllTextAsync(file.Path.LocalPath), file.Name);
            e.Handled = true;
        }
        catch (IOException)
        {
            _viewModel.SetStatus($"Could not read {file.Name}");
        }
        catch (UnauthorizedAccessException)
        {
            _viewModel.SetStatus($"Access denied for {file.Name}");
        }
    }

    private async void SaveOptimizedSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_viewModel.HasOptimizationResult || string.IsNullOrWhiteSpace(_viewModel.OptimizedSql))
        {
            _viewModel.SetStatus("Optimize a query before saving the result");
            return;
        }

        if (_savedSqlFile is null)
        {
            _savedSqlFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save optimized SQL",
                SuggestedFileName = "query_optimizer_optimized_sql.sql",
                DefaultExtension = "sql",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("SQL files") { Patterns = new[] { "*.sql" } }
                }
            });
            if (_savedSqlFile is null) return;
        }

        var sql = _viewModel.OptimizedSql;
        try
        {
            await using var stream = await _savedSqlFile.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(sql);
            await writer.FlushAsync();
            _viewModel.MarkSaved(_savedSqlFile.Name, sql);
        }
        catch (IOException)
        {
            _viewModel.SetStatus($"Could not save {_savedSqlFile.Name}");
        }
        catch (UnauthorizedAccessException)
        {
            _viewModel.SetStatus($"Access denied for {_savedSqlFile.Name}");
        }
    }

    private async void CopyOptimizedSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_viewModel.HasOptimizationResult)
        {
            _viewModel.SetStatus("Optimize a query before copying the result");
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _viewModel.SetStatus("Clipboard is unavailable");
            return;
        }

        await clipboard.SetTextAsync(_viewModel.SqlToCopy);
        _viewModel.SetStatus("Optimized SQL copied to clipboard");
    }

    private async void CopyPerformanceNotes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _viewModel.SetStatus("Clipboard is unavailable");
            return;
        }

        await clipboard.SetTextAsync(_viewModel.Findings);
        _viewModel.SetStatus("Performance notes copied to clipboard");
    }
}
