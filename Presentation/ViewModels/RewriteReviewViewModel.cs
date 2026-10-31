using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QueryOptimizer.Optimization;
using QueryOptimizer.Presentation;
using QueryOptimizer.Presentation.Commands;

namespace QueryOptimizer.Presentation.ViewModels;

public sealed class RewriteReviewViewModel : INotifyPropertyChanged
{
    private readonly Action _onChanged;
    private bool _isAccepted = true;
    private readonly string _language;

    public RewriteReviewViewModel(AppliedRewrite rewrite, int sequence, int sqlOrder, Action onChanged, string language, bool isAccepted = true)
    {
        Rewrite = rewrite;
        Sequence = sequence;
        SqlOrder = sqlOrder;
        _onChanged = onChanged;
        _language = language;
        _isAccepted = isAccepted;
        ToggleCommand = new RelayCommand(Toggle);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public AppliedRewrite Rewrite { get; }
    public int Sequence { get; }
    public int SqlOrder { get; }
    public string Title => Rewrite.Title;
    public string BeforeSummary => Compact(Rewrite.BeforeSnippet);
    public string AfterSummary => Compact(Rewrite.AfterSnippet);
    public bool IsAccepted
    {
        get => _isAccepted;
        private set
        {
            if (_isAccepted == value) return;
            _isAccepted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActionText));
            _onChanged();
        }
    }
    public string ActionText => Localization.Get(_language, IsAccepted ? "RevertRewrite" : "AcceptRewrite");
    public ICommand ToggleCommand { get; }

    private void Toggle() => IsAccepted = !IsAccepted;

    private static string Compact(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
