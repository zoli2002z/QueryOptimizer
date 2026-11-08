using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using QueryOptimizer.Optimization;
using QueryOptimizer.Presentation.Commands;
using QueryOptimizer.Presentation;

namespace QueryOptimizer.Presentation.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ISqlOptimizationService _optimizer;
    private readonly IDiffPreviewMapper _diffPreviewMapper;
    private string _inputSql = string.Empty;
    private string _inputFileName = "QUERY.SQL";
    private string _optimizedSql = string.Empty;
    private string _optimizedFileName = "OPTIMIZED.SQL";
    private string _status = "Ready when you are";
    private string _findings = string.Empty;
    private string? _savedSql;
    private OptimizationResult? _lastOptimizationResult;
    private OptimizationResult? _optimizationBaseline;
    private bool _isPreviewDiffMode;
    private bool _isOutputReadOnly = true;
    private bool _isDarkTheme;
    private string _language = "en";

    public MainWindowViewModel(ISqlOptimizationService optimizer, IDiffPreviewMapper diffPreviewMapper)
    {
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _diffPreviewMapper = diffPreviewMapper ?? throw new ArgumentNullException(nameof(diffPreviewMapper));
        _language = LanguagePreferenceStore.LoadLanguage();
        _status = T("Ready");
        _findings = T("InitialFindings");

        OptimizeCommand = new RelayCommand(Optimize);
        FormatCommand = new RelayCommand(FormatInput);
        TogglePreviewCommand = new RelayCommand(TogglePreview);
        ToggleOutputEditingCommand = new RelayCommand(ToggleOutputEditing);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        _isDarkTheme = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? OptimizationCompleted;

    public string InputSql
    {
        get => _inputSql;
        set => SetProperty(ref _inputSql, value);
    }

    public string InputFileName
    {
        get => _inputFileName;
        private set => SetProperty(ref _inputFileName, value);
    }

    public string OptimizedSql
    {
        get => _optimizedSql;
        set
        {
            if (!SetProperty(ref _optimizedSql, value)) return;
            OnPropertyChanged(nameof(IsUnsaved));
        }
    }

    public string OptimizedFileName
    {
        get => _optimizedFileName;
        private set => SetProperty(ref _optimizedFileName, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Findings
    {
        get => _findings;
        private set => SetProperty(ref _findings, value);
    }

    public bool IsUnsaved => _lastOptimizationResult is not null &&
        (_savedSql is null || !string.Equals(OptimizedSql, _savedSql, StringComparison.Ordinal));

    public bool HasOptimizationResult => _lastOptimizationResult is not null;
    public string SqlToCopy => _lastOptimizationResult?.OptimizedSql ?? string.Empty;
    public bool CanPreviewChanges => RewriteReviews.Count > 0;
    public string RewriteSummary => RewriteTextLocalization.FormatSummary(
        _language,
        RewriteReviews.Count,
        RewriteReviews.Count(item => item.IsAccepted),
        RewriteReviews.Count(item => !item.IsAccepted));

    public bool IsPreviewDiffMode
    {
        get => _isPreviewDiffMode;
        private set
        {
            if (!SetProperty(ref _isPreviewDiffMode, value)) return;
            OnPropertyChanged(nameof(PreviewButtonText));
            OnPropertyChanged(nameof(PreviewButtonTooltip));
            OnPropertyChanged(nameof(IsOptimizedEditorVisible));
            OnPropertyChanged(nameof(IsInputEditorVisible));
            OnPropertyChanged(nameof(IsDiffVisible));
            OnPropertyChanged(nameof(IsPerformanceNotesVisible));
            OnPropertyChanged(nameof(AreWorkspaceActionsEnabled));
            OnPropertyChanged(nameof(CanUseOutputActions));
        }
    }

    public bool IsOptimizedEditorVisible => !IsPreviewDiffMode;
    public bool IsInputEditorVisible => !IsPreviewDiffMode;
    public bool IsDiffVisible => IsPreviewDiffMode;
    public bool IsPerformanceNotesVisible => !IsPreviewDiffMode;
    public bool AreWorkspaceActionsEnabled => !IsPreviewDiffMode;
    public bool CanUseOutputActions => HasOptimizationResult && AreWorkspaceActionsEnabled;
    public string PreviewButtonText => T(IsPreviewDiffMode ? "ShowOptimized" : "PreviewChanges");
    public string PreviewButtonTooltip => T(IsPreviewDiffMode ? "ShowOptimizedTooltip" : "PreviewTooltip");

    public bool IsDarkTheme => _isDarkTheme;
    public string ThemeButtonText => T(IsDarkTheme ? "LightMode" : "DarkMode");
    public IReadOnlyList<LanguageOption> Languages => Localization.Languages
        .Select(option => option with { IsSelected = option.Code == _language })
        .ToArray();
    public string this[string key] => T(key);

    public LanguageOption SelectedLanguage
    {
        get => Languages.First(option => option.Code == _language);
        set
        {
            if (value is null || value.Code == _language) return;
            _language = Languages.Any(option => option.Code == value.Code) ? value.Code : "en";
            LanguagePreferenceStore.SaveLanguage(_language);
            // Force every binding on the window to reevaluate against the new language,
            // including indexer bindings used by localized XAML properties.
            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(Languages));
            OnPropertyChanged("Item[]");
            OnPropertyChanged(nameof(PreviewButtonText));
            OnPropertyChanged(nameof(ThemeButtonText));
            OnPropertyChanged(nameof(EditActionName));
            if (_optimizationBaseline is { } baseline)
            {
                var accepted = RewriteReviews.ToDictionary(item => item.Sequence, item => item.IsAccepted);
                PopulateRewriteReviews(baseline, accepted);
            }
            if (_lastOptimizationResult is { } result)
            {
                Findings = string.Join(Environment.NewLine + Environment.NewLine,
                    result.PerformanceNotes.Select(note => Localization.TranslateFinding(_language, note)));
                Status = _language == "hu"
                    ? $"{result.AppliedRewrites.Count} biztonságos módosítás alkalmazva; {result.PerformanceNotes.Count} teljesítmény-megjegyzés"
                    : result.SummaryStatus;
                RebuildDiffPreview(result);
            }
            else
            {
                Status = T("Ready");
                Findings = T("InitialFindings");
            }
        }
    }

    private string T(string key) => Localization.Get(_language, key);

    public bool IsOutputReadOnly
    {
        get => _isOutputReadOnly;
        private set
        {
            if (!SetProperty(ref _isOutputReadOnly, value)) return;
            OnPropertyChanged(nameof(IsEditSlashVisible));
            OnPropertyChanged(nameof(EditActionName));
        }
    }

    public bool IsEditSlashVisible => IsOutputReadOnly;
    public string EditActionName => T(IsOutputReadOnly ? "EnableEditing" : "DisableEditing");

    public ObservableCollection<DiffLineViewModel> DiffLines { get; } = new();
    public ObservableCollection<DiffSideLineViewModel> OriginalDiffLines { get; } = new();
    public ObservableCollection<DiffSideLineViewModel> OptimizedDiffLines { get; } = new();
    public ObservableCollection<RewriteReviewViewModel> RewriteReviews { get; } = new();
    public ICommand OptimizeCommand { get; }
    public ICommand FormatCommand { get; }
    public ICommand TogglePreviewCommand { get; }
    public ICommand ToggleOutputEditingCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public void LoadInput(string sql, string fileName)
    {
        InputSql = sql;
        InputFileName = fileName;
        Status = _language == "hu" ? $"Betöltve: {fileName}" : $"Loaded {fileName}";
    }

    public void ClearWorkspace()
    {
        InputSql = string.Empty;
        OptimizedSql = string.Empty;
        InputFileName = "QUERY.SQL";
        OptimizedFileName = "OPTIMIZED.SQL";
        _lastOptimizationResult = null;
        _optimizationBaseline = null;
        _savedSql = null;
        Findings = T("InitialFindings");
        Status = T("Ready");
        IsPreviewDiffMode = false;
        IsOutputReadOnly = true;

        DiffLines.Clear();
        OriginalDiffLines.Clear();
        OptimizedDiffLines.Clear();
        RewriteReviews.Clear();

        OnPropertyChanged(nameof(IsUnsaved));
        OnPropertyChanged(nameof(HasOptimizationResult));
        OnPropertyChanged(nameof(CanUseOutputActions));
        OnPropertyChanged(nameof(SqlToCopy));
        OnPropertyChanged(nameof(CanPreviewChanges));
        OnPropertyChanged(nameof(RewriteSummary));
    }

    public void SetStatus(string status) => Status = status;

    private void FormatInput()
    {
        if (string.IsNullOrWhiteSpace(InputSql)) return;

        // Formatting only changes whitespace. Protected SQL strings, quoted names, and comments
        // are left untouched by SqlTextProcessor.
        var processor = new SqlTextProcessor();
        var formatted = processor.RewriteCode(InputSql, code => Regex.Replace(
            code,
            @"\s+(?=(?:SELECT|FROM|WHERE|GROUP\s+BY|HAVING|ORDER\s+BY|UNION(?:\s+ALL)?|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|INNER\s+JOIN|CROSS\s+JOIN|JOIN|ON|AND|OR)\b)",
            match => match.Value.Contains('\n') ? match.Value : Environment.NewLine,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

        InputSql = formatted;
        Status = T("FormattedQuery");
    }

    public void MarkSaved(string fileName, string sql)
    {
        _savedSql = sql;
        OptimizedFileName = fileName;
        OnPropertyChanged(nameof(IsUnsaved));
        Status = _language == "hu" ? $"Mentve: {fileName}" : $"Saved {fileName}";
    }

    private void Optimize()
    {
        if (string.IsNullOrWhiteSpace(InputSql))
        {
            Status = T("EnterQuery");
            return;
        }

        _optimizationBaseline = _optimizer.OptimizeWithDetails(InputSql);
        _lastOptimizationResult = _optimizationBaseline;
        PopulateRewriteReviews(_optimizationBaseline);
        RebuildReviewedResult();
        OptimizationCompleted?.Invoke(this, EventArgs.Empty);
        _savedSql = null;
        OptimizedFileName = "OPTIMIZED.SQL";
        OptimizedSql = _lastOptimizationResult.OptimizedSql;
        Findings = string.Join(Environment.NewLine + Environment.NewLine,
            _lastOptimizationResult.PerformanceNotes.Select(note => Localization.TranslateFinding(_language, note)));

        RebuildDiffPreview(_lastOptimizationResult);

        IsPreviewDiffMode = false;
        IsOutputReadOnly = true;
        OnPropertyChanged(nameof(IsUnsaved));
        OnPropertyChanged(nameof(HasOptimizationResult));
        OnPropertyChanged(nameof(CanUseOutputActions));
        OnPropertyChanged(nameof(SqlToCopy));
        OnPropertyChanged(nameof(CanPreviewChanges));
        OnPropertyChanged(nameof(RewriteSummary));
        Status = _language == "hu"
            ? $"{_lastOptimizationResult.AppliedRewrites.Count} biztonságos módosítás alkalmazva; {_lastOptimizationResult.PerformanceNotes.Count} teljesítmény-megjegyzés"
            : _lastOptimizationResult.SummaryStatus;
    }

    private void TogglePreview()
    {
        if (!CanPreviewChanges) return;
        IsPreviewDiffMode = !IsPreviewDiffMode;
    }

    private void ToggleOutputEditing()
    {
        if (!HasOptimizationResult) return;
        IsOutputReadOnly = !IsOutputReadOnly;
        Status = T(IsOutputReadOnly ? "EditingDisabled" : "EditingEnabled");
    }

    private void ToggleTheme()
    {
        _isDarkTheme = !IsDarkTheme;
        if (Application.Current is { } application)
            application.RequestedThemeVariant = _isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        ThemePreferenceStore.SaveDarkMode(_isDarkTheme);

        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeButtonText));

        if (_lastOptimizationResult is null) return;
        RebuildDiffPreview(_lastOptimizationResult);
    }

    private void RebuildDiffPreview(OptimizationResult result)
    {
        DiffLines.Clear();
        foreach (var line in _diffPreviewMapper.Map(result, _language))
            DiffLines.Add(line);

        var sideBySide = _diffPreviewMapper.MapSideBySide(result, _language);
        OriginalDiffLines.Clear();
        OptimizedDiffLines.Clear();
        foreach (var line in sideBySide.Original) OriginalDiffLines.Add(line);
        foreach (var line in sideBySide.Optimized) OptimizedDiffLines.Add(line);
    }

    private void RebuildReviewedResult()
    {
        if (_optimizationBaseline is not { } baseline) return;

        var sql = baseline.OriginalSql;
        foreach (var review in RewriteReviews.OrderBy(item => item.Sequence))
        {
            if (!review.IsAccepted || string.IsNullOrEmpty(review.Rewrite.BeforeText)) continue;
            var index = sql.IndexOf(review.Rewrite.BeforeText, StringComparison.Ordinal);
            if (index < 0) index = sql.IndexOf(review.Rewrite.BeforeText, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;
            sql = sql[..index] + review.Rewrite.AfterText + sql[(index + review.Rewrite.BeforeText.Length)..];
        }

        var activeRewrites = RewriteReviews.Where(review => review.IsAccepted).OrderBy(item => item.Sequence)
            .Select(review => review.Rewrite).ToArray();
        var notes = _optimizer.Analyze(sql);
        _lastOptimizationResult = baseline with
        {
            OptimizedSql = sql,
            AppliedRewrites = activeRewrites,
            PerformanceNotes = notes,
            DiffLines = _optimizer.BuildLineDiff(baseline.OriginalSql, sql)
        };
        OptimizedSql = sql;
        Findings = string.Join(Environment.NewLine + Environment.NewLine,
            notes.Select(note => Localization.TranslateFinding(_language, note)));
        RebuildDiffPreview(_lastOptimizationResult);
        OnPropertyChanged(nameof(SqlToCopy));
        OnPropertyChanged(nameof(IsUnsaved));
        OnPropertyChanged(nameof(RewriteSummary));
        Status = RewriteSummary;
    }

    private void PopulateRewriteReviews(OptimizationResult baseline, IReadOnlyDictionary<int, bool>? acceptedBySequence = null)
    {
        RewriteReviews.Clear();
        var usedPositions = new Dictionary<string, int>(StringComparer.Ordinal);
        var ordered = new List<RewriteReviewViewModel>(baseline.AppliedRewrites.Count);
        for (var sequence = 0; sequence < baseline.AppliedRewrites.Count; sequence++)
        {
            var rewrite = baseline.AppliedRewrites[sequence];
            var position = FindRewritePosition(baseline, rewrite, usedPositions);
            var isAccepted = acceptedBySequence is not null && acceptedBySequence.TryGetValue(sequence, out var accepted)
                ? accepted
                : !rewrite.RequiresReview;
            ordered.Add(new RewriteReviewViewModel(rewrite, sequence, position, baseline.OriginalSql, RebuildReviewedResult, _language, isAccepted));
        }

        foreach (var review in ordered.OrderBy(item => item.SqlOrder).ThenBy(item => item.Sequence))
            RewriteReviews.Add(review);
        OnPropertyChanged(nameof(CanPreviewChanges));
        OnPropertyChanged(nameof(RewriteSummary));
    }

    private static int FindRewritePosition(OptimizationResult baseline, AppliedRewrite rewrite, IDictionary<string, int> usedPositions)
    {
        var text = rewrite.BeforeText;
        var searchSql = baseline.OriginalSql;
        if (string.IsNullOrEmpty(text) || searchSql.IndexOf(text, StringComparison.Ordinal) < 0)
        {
            text = rewrite.AfterText;
            searchSql = baseline.OptimizedSql;
        }
        if (string.IsNullOrEmpty(text)) return int.MaxValue;

        var occurrence = usedPositions.TryGetValue(text, out var count) ? count : 0;
        var position = -1;
        var start = 0;
        for (var current = 0; current <= occurrence; current++)
        {
            position = searchSql.IndexOf(text, start, StringComparison.OrdinalIgnoreCase);
            if (position < 0)
            {
                position = -1;
                break;
            }
            start = position + Math.Max(1, text.Length);
        }
        usedPositions[text] = occurrence + 1;
        return position < 0 ? int.MaxValue : position;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
