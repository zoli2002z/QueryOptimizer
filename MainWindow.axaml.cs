using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using System.Text.RegularExpressions;

namespace QueryOptimizer;

public partial class MainWindow : Window
{
    private OptimizationResult? _lastOptimizationResult;
    private bool _isPreviewDiffMode;

    public MainWindow()
    {
        InitializeComponent();
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

    private void OptimizeSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var query = InputQueryEditor.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            StatusText.Text = "Enter a query to optimize";
            return;
        }

        // run optimization and collect its rewrite details
        _lastOptimizationResult = SqlOptimizer.OptimizeWithDetails(query);

        // show the optimized query and its analysis notes
        OptimizedQueryEditor.Text = _lastOptimizationResult.OptimizedSql;
        BuildDiffPreview(_lastOptimizationResult);
        OptimizationFindings.Text = string.Join(Environment.NewLine + Environment.NewLine, _lastOptimizationResult.PerformanceNotes);

        // enable preview only when the query changed
        if (this.FindControl<Button>("PreviewToggleBtn") is { } previewBtn)
        {
            previewBtn.IsEnabled = _lastOptimizationResult.HasChanges;
            previewBtn.Content = _lastOptimizationResult.HasChanges
                ? "Preview changes"
                : "Preview changes";
        }
        if (this.FindControl<Button>("CopyBtn") is { } copyBtn)
        {
            copyBtn.IsEnabled = true;
        }

        // start each new result in the clean view
        _isPreviewDiffMode = false;
        UpdatePreviewState();

        // show a summary of the optimization result
        StatusText.Text = _lastOptimizationResult.SummaryStatus;
    }

    private void PreviewToggle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_lastOptimizationResult is null || !_lastOptimizationResult.HasChanges)
        {
            return;
        }

        _isPreviewDiffMode = !_isPreviewDiffMode;
        UpdatePreviewState();
    }

    private void UpdatePreviewState()
    {
        if (_lastOptimizationResult is null) return;

        if (this.FindControl<Button>("PreviewToggleBtn") is { } btn)
        {
            btn.Content = _isPreviewDiffMode ? "Show optimized SQL" : "Preview changes";
        }

        OptimizedQueryEditor.IsVisible = !_isPreviewDiffMode;
        if (this.FindControl<Border>("DiffViewBorder") is { } diffView)
            diffView.IsVisible = _isPreviewDiffMode;
    }

    private void BuildDiffPreview(OptimizationResult result)
    {
        var panel = this.FindControl<StackPanel>("DiffContentPanel");
        if (panel is null) return;

        panel.Children.Clear();
        var reasonsByLine = BuildDiffReasonMap(result);
        for (var lineIndex = 0; lineIndex < result.DiffLines.Count; lineIndex++)
        {
            var line = result.DiffLines[lineIndex];
            // assign each change a color and its source line number
            var (background, foreground, marker, number) = line.Type switch
            {
                DiffType.Added => ("#E8F7EE", "#176B43", "+", line.NewLineNumber?.ToString() ?? ""),
                DiffType.Removed => ("#FDEDEE", "#A9363E", "-", line.OldLineNumber?.ToString() ?? ""),
                _ => ("#F5F7FA", "#53647B", "·", line.NewLineNumber?.ToString() ?? line.OldLineNumber?.ToString() ?? "")
            };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("42,*") };
            row.Children.Add(new TextBlock
            {
                Text = number,
                Foreground = Brush.Parse("#91A0B3"),
                FontSize = 11,
                FontFamily = "Cascadia Mono,Consolas,monospace",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            var content = new TextBlock
            {
                Text = $"{marker}  {line.Text}",
                Foreground = Brush.Parse(foreground),
                FontSize = 12,
                FontFamily = "Cascadia Mono,Consolas,monospace",
                TextWrapping = TextWrapping.NoWrap
            };
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            var diffRow = new Border
            {
                Background = Brush.Parse(background),
                CornerRadius = new Avalonia.CornerRadius(5),
                Padding = new Avalonia.Thickness(9, 5),
                Child = row
            };

            if (line.Type != DiffType.Unchanged)
            {
                // show rewrite details only when a changed line is hovered
                if (reasonsByLine.TryGetValue(lineIndex, out var reasons))
                {
                    ToolTip.SetTip(diffRow, string.Join(Environment.NewLine + Environment.NewLine,
                        reasons.Select(rewrite => $"{rewrite.Title}{Environment.NewLine}{rewrite.WhyItMatters}")));
                }
            }

            panel.Children.Add(diffRow);
        }
    }

    private static Dictionary<int, IReadOnlyList<AppliedRewrite>> BuildDiffReasonMap(OptimizationResult result)
    {
        var groups = new List<List<int>>();
        List<int>? currentGroup = null;
        for (var index = 0; index < result.DiffLines.Count; index++)
        {
            if (result.DiffLines[index].Type == DiffType.Unchanged)
            {
                currentGroup = null;
                continue;
            }

            if (currentGroup is null)
            {
                currentGroup = new List<int>();
                groups.Add(currentGroup);
            }
            currentGroup.Add(index);
        }

        var reasonsByLine = new Dictionary<int, IReadOnlyList<AppliedRewrite>>();
        var assignedRewriteIndexes = new HashSet<int>();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            // match visible diff text to the snippets recorded by each rewrite
            var matchingIndexes = Enumerable.Range(0, result.AppliedRewrites.Count)
                .Where(rewriteIndex => !assignedRewriteIndexes.Contains(rewriteIndex) && group.Any(index =>
                    SnippetMatchesLine(result.AppliedRewrites[rewriteIndex].BeforeSnippet, result.DiffLines[index].Text) ||
                    SnippetMatchesLine(result.AppliedRewrites[rewriteIndex].AfterSnippet, result.DiffLines[index].Text)))
                .ToList();

            if (matchingIndexes.Count == 0 && result.AppliedRewrites.Count > assignedRewriteIndexes.Count)
            {
                // use rewrite order when snippets are abbreviated or span multiple lines
                matchingIndexes.Add(Enumerable.Range(0, result.AppliedRewrites.Count)
                    .First(rewriteIndex => !assignedRewriteIndexes.Contains(rewriteIndex)));
            }

            foreach (var rewriteIndex in matchingIndexes)
                assignedRewriteIndexes.Add(rewriteIndex);
            var matching = matchingIndexes.Select(rewriteIndex => result.AppliedRewrites[rewriteIndex]).ToList();
            foreach (var lineIndex in group)
                reasonsByLine[lineIndex] = matching;
        }

        return reasonsByLine;
    }

    private static bool SnippetMatchesLine(string snippet, string line)
    {
        // expand shorthand markers to match the relevant part of a sql line
        if (string.IsNullOrWhiteSpace(snippet))
            return false;

        var segments = snippet.Split("...", StringSplitOptions.None)
            .Select(segment => string.Join(@"\s+", segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(Regex.Escape)))
            .Where(segment => segment.Length > 0)
            .ToList();
        if (segments.Count == 0 || snippet.Trim() is "(Removed)" or "WHERE" or "SELECT")
            return false;

        var pattern = string.Join(".*?", segments);
        return Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private async void CopyOptimizedSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // copy the clean optimized query even when diff preview is open
        if (_lastOptimizationResult is null)
        {
            StatusText.Text = "Optimize a query before copying the result";
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            StatusText.Text = "Clipboard is unavailable";
            return;
        }

        await clipboard.SetTextAsync(_lastOptimizationResult.OptimizedSql);
        StatusText.Text = "Optimized SQL copied to clipboard";
    }
}
