using System;
using System.Collections.Generic;

namespace QueryOptimizer.Optimization;

public enum DiffType
{
    Unchanged,
    Removed,
    Added
}

public sealed record AppliedRewrite(
    string RuleId,
    string Category,
    string Title,
    string BeforeSnippet,
    string AfterSnippet,
    string WhyItMatters)
{
    // Exact text is kept separately from the shorter, user-facing snippets so a
    // review choice can be applied back to the SQL without guessing.
    public string BeforeText { get; init; } = BeforeSnippet;
    public string AfterText { get; init; } = AfterSnippet;
    public bool RequiresReview { get; init; }
}

public sealed record SqlDiffLine(
    DiffType Type,
    int? OldLineNumber,
    int? NewLineNumber,
    string Text);

public sealed record OptimizationResult(
    string OriginalSql,
    string OptimizedSql,
    IReadOnlyList<AppliedRewrite> AppliedRewrites,
    IReadOnlyList<string> PerformanceNotes,
    IReadOnlyList<SqlDiffLine> DiffLines)
{
    public string? ParseError { get; init; }
    public bool HasChanges => !string.Equals(OriginalSql, OptimizedSql, StringComparison.Ordinal);

    public string SummaryStatus
    {
        get
        {
            var safeCount = 0;
            var reviewCount = 0;
            foreach (var rewrite in AppliedRewrites)
            {
                if (rewrite.RequiresReview) reviewCount++;
                else safeCount++;
            }
            var reviewText = reviewCount == 0 ? string.Empty : $"; {reviewCount} suggestion(s) need review";
            return HasChanges
                ? $"Applied {safeCount} safe rewrite(s){reviewText}; found {PerformanceNotes.Count} performance note(s)"
                : $"No safe rewrite applied{reviewText}; found {PerformanceNotes.Count} performance note(s)";
        }
    }
}
