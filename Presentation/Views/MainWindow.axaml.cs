using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using QueryOptimizer.Optimization;
using QueryOptimizer.Presentation;
using QueryOptimizer.Presentation.ViewModels;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace QueryOptimizer.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private IStorageFile? _savedSqlFile;
    private bool _isSyncingDiffScroll;

    private void LanguagePicker_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var menu = new MenuFlyout();
        foreach (var language in _viewModel.Languages)
        {
            var marker = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = language.IsSelected ? Brush.Parse("#246BFE") : Brushes.Transparent,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var code = new TextBlock
            {
                Text = language.DisplayCode,
                FontSize = 12,
                FontWeight = language.IsSelected ? FontWeight.Bold : FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Children = { marker, code }
            };
            var item = new MenuItem { Header = content, Tag = language };
            item.Click += LanguageOption_Click;
            menu.Items.Add(item);
        }

        menu.ShowAt(button);
    }

    private void LanguageOption_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: LanguageOption language })
            _viewModel.SelectedLanguage = language;
    }

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
            Title = _viewModel["SelectSqlFile"],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(_viewModel["SqlFiles"]) { Patterns = new[] { "*.sql" } },
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
            _viewModel.SetStatus(string.Format(_viewModel["CouldNotRead"], file.Name));
        }
        catch (UnauthorizedAccessException)
        {
            _viewModel.SetStatus(string.Format(_viewModel["AccessDenied"], file.Name));
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
            _viewModel.SetStatus(string.Format(_viewModel["CouldNotRead"], file.Name));
        }
        catch (UnauthorizedAccessException)
        {
            _viewModel.SetStatus(string.Format(_viewModel["AccessDenied"], file.Name));
        }
    }

    private async void SaveOptimizedSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_viewModel.HasOptimizationResult || string.IsNullOrWhiteSpace(_viewModel.OptimizedSql))
        {
            _viewModel.SetStatus(_viewModel["OptimizeBeforeSave"]);
            return;
        }

        if (_savedSqlFile is null)
        {
            _savedSqlFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _viewModel["SaveSql"],
                SuggestedFileName = "query_optimizer_optimized_sql.sql",
                DefaultExtension = "sql",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(_viewModel["SqlFiles"]) { Patterns = new[] { "*.sql" } }
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
            _viewModel.SetStatus(string.Format(_viewModel["CouldNotSave"], _savedSqlFile.Name));
        }
        catch (UnauthorizedAccessException)
        {
            _viewModel.SetStatus(string.Format(_viewModel["AccessDenied"], _savedSqlFile.Name));
        }
    }

    private async void CopyOptimizedSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_viewModel.HasOptimizationResult)
        {
            _viewModel.SetStatus(_viewModel["OptimizeBeforeCopy"]);
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _viewModel.SetStatus(_viewModel["ClipboardUnavailable"]);
            return;
        }

        await clipboard.SetTextAsync(_viewModel.SqlToCopy);
        _viewModel.SetStatus(_viewModel["CopiedSql"]);
    }

    private async void CopyPerformanceNotes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _viewModel.SetStatus(_viewModel["ClipboardUnavailable"]);
            return;
        }

        await clipboard.SetTextAsync(_viewModel.Findings);
        _viewModel.SetStatus(_viewModel["CopiedNotes"]);
    }
}
