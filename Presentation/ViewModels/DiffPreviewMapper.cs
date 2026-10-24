using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Media;
using QueryOptimizer.Optimization;

namespace QueryOptimizer.Presentation.ViewModels;

public sealed class DiffPreviewMapper : IDiffPreviewMapper
{
    public IReadOnlyList<DiffLineViewModel> Map(OptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var reasonsByLine = BuildDiffReasonMap(result);
        var rows = new List<DiffLineViewModel>(result.DiffLines.Count);
        for (var index = 0; index < result.DiffLines.Count; index++)
        {
            var line = result.DiffLines[index];
            var (background, foreground, marker, number) = line.Type switch
            {
                DiffType.Added => ("#E8F7EE", "#176B43", "+", line.NewLineNumber?.ToString() ?? string.Empty),
                DiffType.Removed => ("#FDEDEE", "#A9363E", "-", line.OldLineNumber?.ToString() ?? string.Empty),
                _ => ("#F5F7FA", "#53647B", "·", line.NewLineNumber?.ToString() ?? line.OldLineNumber?.ToString() ?? string.Empty)
            };
            var tooltip = reasonsByLine.TryGetValue(index, out var reasons)
                ? string.Join(Environment.NewLine + Environment.NewLine,
                    reasons.Select(rewrite => $"{rewrite.Title}{Environment.NewLine}{rewrite.WhyItMatters}"))
                : null;

            rows.Add(new DiffLineViewModel(number, $"{marker}  {line.Text}",
                Brush.Parse(background), Brush.Parse(foreground), tooltip));
        }

        return rows;
    }

    private static Dictionary<int, IReadOnlyList<AppliedRewrite>> BuildDiffReasonMap(OptimizationResult result)
    {
        var changedGroups = new List<List<int>>();
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
                changedGroups.Add(currentGroup);
            }
            currentGroup.Add(index);
        }

        var reasonsByLine = new Dictionary<int, IReadOnlyList<AppliedRewrite>>();
        var assignedRewriteIndexes = new HashSet<int>();
        foreach (var group in changedGroups)
        {
            var matchingIndexes = Enumerable.Range(0, result.AppliedRewrites.Count)
                .Where(rewriteIndex => !assignedRewriteIndexes.Contains(rewriteIndex) && group.Any(index =>
                    SnippetMatchesLine(result.AppliedRewrites[rewriteIndex].BeforeSnippet, result.DiffLines[index].Text) ||
                    SnippetMatchesLine(result.AppliedRewrites[rewriteIndex].AfterSnippet, result.DiffLines[index].Text)))
                .ToList();

            if (matchingIndexes.Count == 0 && result.AppliedRewrites.Count > assignedRewriteIndexes.Count)
                matchingIndexes.Add(Enumerable.Range(0, result.AppliedRewrites.Count)
                    .First(rewriteIndex => !assignedRewriteIndexes.Contains(rewriteIndex)));

            foreach (var rewriteIndex in matchingIndexes) assignedRewriteIndexes.Add(rewriteIndex);
            var matchingRewrites = matchingIndexes.Select(rewriteIndex => result.AppliedRewrites[rewriteIndex]).ToList();
            foreach (var lineIndex in group) reasonsByLine[lineIndex] = matchingRewrites;
        }

        return reasonsByLine;
    }

    private static bool SnippetMatchesLine(string snippet, string line)
    {
        if (string.IsNullOrWhiteSpace(snippet)) return false;

        var segments = snippet.Split("...", StringSplitOptions.None)
            .Select(segment => string.Join(@"\s+", segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(Regex.Escape)))
            .Where(segment => segment.Length > 0)
            .ToList();
        if (segments.Count == 0 || snippet.Trim() is "(Removed)" or "WHERE" or "SELECT") return false;

        return Regex.IsMatch(line, string.Join(".*?", segments), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
