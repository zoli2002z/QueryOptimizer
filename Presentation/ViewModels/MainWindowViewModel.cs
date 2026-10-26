using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QueryOptimizer.Optimization;
using QueryOptimizer.Presentation.Commands;

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
    private string _findings = "Run an analysis to see query patterns and practical suggestions.";
    private string? _savedSql;
    private OptimizationResult? _lastOptimizationResult;
    private bool _isPreviewDiffMode;
    private bool _isOutputReadOnly = true;

    public MainWindowViewModel(ISqlOptimizationService optimizer, IDiffPreviewMapper diffPreviewMapper)
    {
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _diffPreviewMapper = diffPreviewMapper ?? throw new ArgumentNullException(nameof(diffPreviewMapper));

        OptimizeCommand = new RelayCommand(Optimize);
        TogglePreviewCommand = new RelayCommand(TogglePreview);
        ToggleOutputEditingCommand = new RelayCommand(ToggleOutputEditing);
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
    public bool CanPreviewChanges => _lastOptimizationResult?.HasChanges == true;

    public bool IsPreviewDiffMode
    {
        get => _isPreviewDiffMode;
        private set
        {
            if (!SetProperty(ref _isPreviewDiffMode, value)) return;
            OnPropertyChanged(nameof(PreviewButtonText));
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
    public string PreviewButtonText => IsPreviewDiffMode ? "Show optimized SQL" : "Preview changes";

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
    public string EditActionName => IsOutputReadOnly ? "Enable editing" : "Disable editing";

    public ObservableCollection<DiffLineViewModel> DiffLines { get; } = new();
    public ObservableCollection<DiffSideLineViewModel> OriginalDiffLines { get; } = new();
    public ObservableCollection<DiffSideLineViewModel> OptimizedDiffLines { get; } = new();
    public ICommand OptimizeCommand { get; }
    public ICommand TogglePreviewCommand { get; }
    public ICommand ToggleOutputEditingCommand { get; }

    public void LoadInput(string sql, string fileName)
    {
        InputSql = sql;
        InputFileName = fileName;
        Status = $"Loaded {fileName}";
    }

    public void SetStatus(string status) => Status = status;

    public void MarkSaved(string fileName, string sql)
    {
        _savedSql = sql;
        OptimizedFileName = fileName;
        OnPropertyChanged(nameof(IsUnsaved));
        Status = $"Saved {fileName}";
    }

    private void Optimize()
    {
        if (string.IsNullOrWhiteSpace(InputSql))
        {
            Status = "Enter a query to optimize";
            return;
        }

        _lastOptimizationResult = _optimizer.OptimizeWithDetails(InputSql);
        OptimizationCompleted?.Invoke(this, EventArgs.Empty);
        _savedSql = null;
        OptimizedFileName = "OPTIMIZED.SQL";
        OptimizedSql = _lastOptimizationResult.OptimizedSql;
        Findings = string.Join(Environment.NewLine + Environment.NewLine, _lastOptimizationResult.PerformanceNotes);

        DiffLines.Clear();
        foreach (var line in _diffPreviewMapper.Map(_lastOptimizationResult))
            DiffLines.Add(line);
        OriginalDiffLines.Clear();
        OptimizedDiffLines.Clear();
        var sideBySide = _diffPreviewMapper.MapSideBySide(_lastOptimizationResult);
        foreach (var line in sideBySide.Original) OriginalDiffLines.Add(line);
        foreach (var line in sideBySide.Optimized) OptimizedDiffLines.Add(line);

        IsPreviewDiffMode = false;
        IsOutputReadOnly = true;
        OnPropertyChanged(nameof(IsUnsaved));
        OnPropertyChanged(nameof(HasOptimizationResult));
        OnPropertyChanged(nameof(CanUseOutputActions));
        OnPropertyChanged(nameof(SqlToCopy));
        OnPropertyChanged(nameof(CanPreviewChanges));
        Status = _lastOptimizationResult.SummaryStatus;
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
        Status = IsOutputReadOnly ? "Optimized SQL editing disabled" : "Optimized SQL editing enabled";
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
