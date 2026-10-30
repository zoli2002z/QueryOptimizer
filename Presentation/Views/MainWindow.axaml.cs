using System;
using System.IO;
using System.Linq;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using QueryOptimizer.Optimization;
using QueryOptimizer.Presentation;
using QueryOptimizer.Presentation.ViewModels;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace QueryOptimizer.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private TextEditor _inputEditor = null!;
    private TextEditor _outputEditor = null!;
    private Border _searchBar = null!;
    private TextBox _searchBox = null!;
    private ScrollViewer _inputDiffScrollViewer = null!;
    private ScrollViewer _outputDiffScrollViewer = null!;
    private IStorageFile? _savedSqlFile;
    private bool _isSyncingDiffScroll;

    private void LanguagePicker_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var menu = new MenuFlyout();
        menu.FlyoutPresenterClasses.Add("languageMenu");
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
                Spacing = 5,
                Children = { marker, code }
            };
            var item = new MenuItem
            {
                Header = content,
                Tag = language,
                MinWidth = 0,
                Width = 42,
                Padding = new Avalonia.Thickness(4, 4)
            };
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
        AvaloniaXamlLoader.Load(this);
        _inputEditor = this.FindControl<TextEditor>("InputEditor")!;
        _outputEditor = this.FindControl<TextEditor>("OutputEditor")!;
        _searchBar = this.FindControl<Border>("SearchBar")!;
        _searchBox = this.FindControl<TextBox>("SearchBox")!;
        _inputDiffScrollViewer = this.FindControl<ScrollViewer>("InputDiffScrollViewer")!;
        _outputDiffScrollViewer = this.FindControl<ScrollViewer>("OutputDiffScrollViewer")!;
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.OptimizationCompleted += (_, _) => _savedSqlFile = null;
        DataContext = _viewModel;
        _inputEditor.Options.AllowScrollBelowDocument = false;
        _outputEditor.Options.AllowScrollBelowDocument = false;
        _inputEditor.Text = _viewModel.InputSql;
        _outputEditor.Text = _viewModel.OptimizedSql;
        _inputEditor.SizeChanged += (_, _) => UpdateVerticalScrollBarVisibility(_inputEditor);
        _outputEditor.SizeChanged += (_, _) => UpdateVerticalScrollBarVisibility(_outputEditor);
        _inputEditor.TextChanged += (_, _) =>
        {
            if (!string.Equals(_viewModel.InputSql, _inputEditor.Text, StringComparison.Ordinal))
                _viewModel.InputSql = _inputEditor.Text;
            UpdateVerticalScrollBarVisibility(_inputEditor);
        };
        _outputEditor.TextChanged += (_, _) =>
        {
            if (!string.Equals(_viewModel.OptimizedSql, _outputEditor.Text, StringComparison.Ordinal))
                _viewModel.OptimizedSql = _outputEditor.Text;
            UpdateVerticalScrollBarVisibility(_outputEditor);
        };
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is null or nameof(MainWindowViewModel.InputSql))
            {
                if (!string.Equals(_inputEditor.Text, _viewModel.InputSql, StringComparison.Ordinal))
                    _inputEditor.Text = _viewModel.InputSql;
                UpdateVerticalScrollBarVisibility(_inputEditor);
            }
            if (args.PropertyName is null or nameof(MainWindowViewModel.OptimizedSql))
            {
                if (!string.Equals(_outputEditor.Text, _viewModel.OptimizedSql, StringComparison.Ordinal))
                    _outputEditor.Text = _viewModel.OptimizedSql;
                UpdateVerticalScrollBarVisibility(_outputEditor);
            }
        };
        var sqlHighlighting = CreateSqlHighlighting();
        _inputEditor.SyntaxHighlighting = sqlHighlighting;
        _outputEditor.SyntaxHighlighting = sqlHighlighting;
        AddHandler(KeyDownEvent, MainWindow_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private static void UpdateVerticalScrollBarVisibility(TextEditor editor)
    {
        if (editor.Bounds.Height <= 0)
            return;

        var contentHeight = editor.Document.LineCount * editor.TextArea.TextView.DefaultLineHeight;
        editor.VerticalScrollBarVisibility = contentHeight > editor.Bounds.Height
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Hidden;
    }

    private static IHighlightingDefinition CreateSqlHighlighting()
    {
        const string definition = """
            <SyntaxDefinition name="SQL" extensions=".sql" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Keyword" foreground="#5B65D6" fontWeight="bold" />
              <Color name="String" foreground="#16805D" />
              <Color name="Comment" foreground="#8794A7" />
              <Color name="Number" foreground="#B05A27" />
              <RuleSet>
                <Keywords color="Keyword"><Word>SELECT</Word><Word>FROM</Word><Word>WHERE</Word><Word>JOIN</Word><Word>LEFT</Word><Word>RIGHT</Word><Word>FULL</Word><Word>INNER</Word><Word>OUTER</Word><Word>CROSS</Word><Word>ON</Word><Word>AS</Word><Word>AND</Word><Word>OR</Word><Word>NOT</Word><Word>NULL</Word><Word>IS</Word><Word>IN</Word><Word>EXISTS</Word><Word>LIKE</Word><Word>BETWEEN</Word><Word>GROUP</Word><Word>BY</Word><Word>HAVING</Word><Word>ORDER</Word><Word>ASC</Word><Word>DESC</Word><Word>DISTINCT</Word><Word>UNION</Word><Word>ALL</Word><Word>CASE</Word><Word>WHEN</Word><Word>THEN</Word><Word>ELSE</Word><Word>END</Word><Word>WITH</Word><Word>INSERT</Word><Word>UPDATE</Word><Word>DELETE</Word><Word>INTO</Word><Word>VALUES</Word><Word>SET</Word><Word>CREATE</Word><Word>ALTER</Word><Word>DROP</Word><Word>TABLE</Word><Word>LIMIT</Word><Word>OFFSET</Word></Keywords>
                <Span color="Comment" begin="--" />
                <Span color="Comment" begin="/\*" end="\*/" multiline="true" />
                <Span color="String" begin="'" end="'" escapeCharacter="'" />
                <Rule color="Number">\b\d+(?:\.\d+)?\b</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;
        using var reader = XmlReader.Create(new System.IO.StringReader(definition));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;

        if (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            _viewModel.FormatCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F)
        {
            _searchBar.IsVisible = true;
            _searchBox.Focus();
            _searchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _viewModel.OptimizeCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.O)
        {
            SelectSqlFile_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.S && _viewModel.HasOptimizationResult)
        {
            SaveOptimizedSql_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void ToggleSearch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _searchBar.IsVisible = !_searchBar.IsVisible;
        if (_searchBar.IsVisible)
        {
            _searchBox.Focus();
            _searchBox.SelectAll();
        }
        else _inputEditor.Focus();
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((e.KeyModifiers & KeyModifiers.Shift) != 0) FindPrevious();
            else FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _searchBar.IsVisible = false;
            _inputEditor.Focus();
            e.Handled = true;
        }
    }

    private void FindNext_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => FindNext();
    private void FindPrevious_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => FindPrevious();

    private void FindNext() => FindInInput(forward: true);
    private void FindPrevious() => FindInInput(forward: false);

    private void FindInInput(bool forward)
    {
        var query = _searchBox.Text;
        var source = _inputEditor.Text ?? string.Empty;
        if (string.IsNullOrEmpty(query) || source.Length == 0) return;

        var start = forward
            ? Math.Min(_inputEditor.SelectionStart + _inputEditor.SelectionLength, source.Length)
            : Math.Max(0, _inputEditor.SelectionStart - 1);
        var index = forward
            ? source.IndexOf(query, start, StringComparison.CurrentCultureIgnoreCase)
            : source.LastIndexOf(query, start, StringComparison.CurrentCultureIgnoreCase);
        if (index < 0)
            index = forward
                ? source.IndexOf(query, StringComparison.CurrentCultureIgnoreCase)
                : source.LastIndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        if (index < 0) return;

        _inputEditor.Select(index, query.Length);
        _inputEditor.ScrollToLine(_inputEditor.Document.GetLineByOffset(index).LineNumber);
        _inputEditor.Focus();
    }

    private void DiffScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingDiffScroll || sender is not ScrollViewer source) return;

        var target = ReferenceEquals(source, _inputDiffScrollViewer)
            ? _outputDiffScrollViewer
            : _inputDiffScrollViewer;
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
