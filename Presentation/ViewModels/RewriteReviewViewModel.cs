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

    public RewriteReviewViewModel(AppliedRewrite rewrite, int sequence, int sqlOrder, string sourceSql, Action onChanged, string language, bool isAccepted = true)
    {
        Rewrite = rewrite;
        Sequence = sequence;
        SqlOrder = sqlOrder;
        var formattedRewrite = Localization.FormatRewrite(rewrite, language).Split(Environment.NewLine, 2);
        Title = formattedRewrite[0];
        WhySummary = formattedRewrite.Length > 1 ? formattedRewrite[1] : string.Empty;
        RuleLocation = GetLocation(sourceSql, sqlOrder, language);
        Tooltip = $"{WhySummary}{Environment.NewLine}{RewriteTextLocalization.RuleReferenceLabel(language)}: {rewrite.RuleId}";
        _onChanged = onChanged;
        _language = language;
        _isAccepted = isAccepted;
        ToggleCommand = new RelayCommand(Toggle);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public AppliedRewrite Rewrite { get; }
    public int Sequence { get; }
    public int SqlOrder { get; }
    public string Title { get; }
    public string WhySummary { get; }
    public string RuleLocation { get; }
    public string Tooltip { get; }
    public string ReviewLabel => RewriteTextLocalization.ReviewLabel(_language, Rewrite.RequiresReview, IsAccepted);
    public bool RequiresReview => Rewrite.RequiresReview;
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
            OnPropertyChanged(nameof(ReviewLabel));
            _onChanged();
        }
    }
    public string ActionText => RewriteTextLocalization.ActionLabel(_language, IsAccepted);
    public ICommand ToggleCommand { get; }

    private void Toggle() => IsAccepted = !IsAccepted;

    private static string Compact(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    private static string GetLocation(string sql, int position, string language)
    {
        if (position < 0 || position > sql.Length) return RewriteTextLocalization.UnknownLocation(language);
        var line = 1;
        var column = 1;
        for (var index = 0; index < position; index++)
        {
            if (sql[index] == '\n') { line++; column = 1; }
            else if (sql[index] != '\r') column++;
        }
        return RewriteTextLocalization.FormatLocation(language, line, column);
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
