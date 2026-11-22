using System;
using System.Collections.Generic;

namespace QueryOptimizer.Optimization;

internal sealed class SqlLineDiffBuilder
{
    public IReadOnlyList<SqlDiffLine> Build(string originalSql, string optimizedSql)
    {
        var oldLines = SplitLines(originalSql);
        var newLines = SplitLines(optimizedSql);
        var oldCount = oldLines.Length;
        var newCount = newLines.Length;
        var longestCommonSubsequence = new int[oldCount + 1, newCount + 1];

        for (var oldIndex = oldCount - 1; oldIndex >= 0; oldIndex--)
        {
            for (var newIndex = newCount - 1; newIndex >= 0; newIndex--)
            {
                longestCommonSubsequence[oldIndex, newIndex] =
                    string.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal)
                        ? 1 + longestCommonSubsequence[oldIndex + 1, newIndex + 1]
                        : Math.Max(longestCommonSubsequence[oldIndex + 1, newIndex],
                            longestCommonSubsequence[oldIndex, newIndex + 1]);
            }
        }

        var diff = new List<SqlDiffLine>(oldCount + newCount);
        var oldPosition = 0;
        var newPosition = 0;
        while (oldPosition < oldCount && newPosition < newCount)
        {
            if (string.Equals(oldLines[oldPosition], newLines[newPosition], StringComparison.Ordinal))
            {
                diff.Add(new SqlDiffLine(DiffType.Unchanged, oldPosition + 1, newPosition + 1, oldLines[oldPosition]));
                oldPosition++;
                newPosition++;
            }
            else if (longestCommonSubsequence[oldPosition + 1, newPosition] >=
                     longestCommonSubsequence[oldPosition, newPosition + 1])
            {
                diff.Add(new SqlDiffLine(DiffType.Removed, oldPosition + 1, null, oldLines[oldPosition]));
                oldPosition++;
            }
            else
            {
                diff.Add(new SqlDiffLine(DiffType.Added, null, newPosition + 1, newLines[newPosition]));
                newPosition++;
            }
        }

        while (oldPosition < oldCount)
        {
            diff.Add(new SqlDiffLine(DiffType.Removed, oldPosition + 1, null, oldLines[oldPosition]));
            oldPosition++;
        }

        while (newPosition < newCount)
        {
            diff.Add(new SqlDiffLine(DiffType.Added, null, newPosition + 1, newLines[newPosition]));
            newPosition++;
        }

        return diff;
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
