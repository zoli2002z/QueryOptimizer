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
    public bool HasChanges => !string.Equals(OriginalSql, OptimizedSql, StringComparison.Ordinal);

    public string SummaryStatus =>
        HasChanges
            ? $"Applied {AppliedRewrites.Count} safe rewrite(s); found {PerformanceNotes.Count} performance note(s)"
            : $"No safe rewrite applied; found {PerformanceNotes.Count} performance note(s)";
}
