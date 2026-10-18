using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace QueryOptimizer;

// applies simple SQL rewrites
public static class SqlOptimizer
{
    private static readonly Regex TrueConjunction = new(
        @"\bAND\s+1\s*=\s*1\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FalseDisjunction = new(
        @"\bOR\s+1\s*=\s*0\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingTrueCondition = new(
        @"(\bWHERE\s+)1\s*=\s*1\s+AND\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StandaloneTrueCondition = new(
        @"\bWHERE\s+1\s*=\s*1(?=\s*(?:GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|OFFSET|FETCH|FOR|;|$))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SelectStar = new(
        @"\bSELECT\s+(?:DISTINCT\s+)?(?:[\w$#]+\s*\.\s*)?\*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingWildcardLike = new(
        @"\bLIKE\s+(?:N\s*)?'[%_]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FunctionOnPredicateColumn = new(
        @"\b(?:WHERE|ON)\b[^;]*?\b(?:YEAR|MONTH|DAY|DATEPART|LOWER|UPPER|ISNULL|COALESCE|CONVERT|CAST|SUBSTRING)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RowByRowProcessing = new(
        @"\b(?:WHILE\b|DECLARE\s+\w+\s+CURSOR\b|OPEN\s+\w+)" ,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Subquery = new(
        @"\b(?:EXISTS\s*\(|(?:IN|=)\s*\(\s*SELECT\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // runs the rewrite rules in order
    public static string Optimize(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var result = sql;
        string previous;

        // repeat the rules until no more changes are made
        do
        {
            previous = result;
            result = RewriteCode(result, RemoveLeadingTrueCondition);
            result = RewriteCode(result, RemoveStandaloneTrueCondition);
            result = RewriteCode(result, RemoveAlwaysTrueConjuncts);
            result = RewriteCode(result, RemoveAlwaysFalseDisjuncts);
        } while (!string.Equals(previous, result, StringComparison.Ordinal));

        return result;
    }

    // finds common query patterns that may affect performance
    public static IReadOnlyList<string> Analyze(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var analysisText = MaskCommentsAndIdentifiers(sql);
        var likeText = MaskCommentsAndIdentifiers(sql, preserveStringLiterals: true);
        var findings = new List<string>();

        if (SelectStar.IsMatch(analysisText))
        {
            findings.Add("SELECT *: list only the columns the query needs. This reduces data reads and can allow a covering index.");
        }

        if (LeadingWildcardLike.IsMatch(likeText))
        {
            findings.Add("Leading wildcard in LIKE: a pattern starting with % or _ usually prevents a B-tree index seek. Consider full-text search for substring searches.");
        }

        if (FunctionOnPredicateColumn.IsMatch(analysisText))
        {
            findings.Add("Function in a WHERE or JOIN predicate: applying a function to a column can prevent an index seek. Compare the column directly where possible.");
        }

        if (RowByRowProcessing.IsMatch(analysisText))
        {
            findings.Add("Cursor or WHILE loop found: consider whether the work can be expressed as a set-based statement or JOIN.");
        }

        if (Subquery.IsMatch(analysisText))
        {
            findings.Add("Subquery found: check whether it is correlated and runs once per outer row. A JOIN or set-based rewrite may help, depending on query semantics.");
        }

        if (findings.Count == 0)
        {
            findings.Add("No supported performance patterns were detected. This static check does not estimate execution cost or inspect indexes.");
        }

        return findings;
    }

    // a condition AND TRUE is the same as the condition
    private static string RemoveAlwaysTrueConjuncts(string sql) =>
        TrueConjunction.Replace(sql, string.Empty);

    // a condition OR FALSE is the same as the condition
    private static string RemoveAlwaysFalseDisjuncts(string sql) =>
        FalseDisjunction.Replace(sql, string.Empty);

    // remove TRUE AND at the start of a WHERE clause
    private static string RemoveLeadingTrueCondition(string sql) =>
        LeadingTrueCondition.Replace(sql, "$1");

    // remove a WHERE clause that keeps every row
    private static string RemoveStandaloneTrueCondition(string sql) =>
        StandaloneTrueCondition.Replace(sql, string.Empty);

    // hide comments and identifiers while keeping strings when needed
    private static string MaskCommentsAndIdentifiers(string sql, bool preserveStringLiterals = false)
    {
        var result = new StringBuilder(sql);
        var index = 0;

        while (index < sql.Length)
        {
            if (sql[index] == '\'')
            {
                var stringEnd = GetProtectedRegionEnd(sql, index);
                if (!preserveStringLiterals)
                {
                    MaskRegion(result, sql, index, stringEnd);
                }

                index = stringEnd;
                continue;
            }

            var commentStart = sql[index] == '-' && index + 1 < sql.Length && sql[index + 1] == '-' ||
                               sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*';
            var identifierQuote = sql[index] is '"' or '`' or '[';

            if (!commentStart && !identifierQuote)
            {
                index++;
                continue;
            }

            var end = GetProtectedRegionEnd(sql, index);
            MaskRegion(result, sql, index, end);
            index = end;
        }

        return result.ToString();
    }

    private static void MaskRegion(StringBuilder result, string source, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (source[index] is not '\r' and not '\n')
            {
                result[index] = ' ';
            }
        }
    }

    // skip strings, identifiers, and comments when rewriting SQL
    private static string RewriteCode(string sql, Func<string, string> rewrite)
    {
        var result = new StringBuilder(sql.Length);
        var codeStart = 0;
        var index = 0;

        while (index < sql.Length)
        {
            var protectedEnd = GetProtectedRegionEnd(sql, index);
            if (protectedEnd == index)
            {
                index++;
                continue;
            }

            result.Append(rewrite(sql[codeStart..index]));
            result.Append(sql[index..protectedEnd]);
            index = protectedEnd;
            codeStart = index;
        }

        result.Append(rewrite(sql[codeStart..]));
        return result.ToString();
    }

    // find the end of a string, identifier, or comment
    private static int GetProtectedRegionEnd(string sql, int start)
    {
        var current = sql[start];

        if (current == '-' && start + 1 < sql.Length && sql[start + 1] == '-')
        {
            var end = sql.IndexOfAny(new[] { '\r', '\n' }, start + 2);
            return end < 0 ? sql.Length : end;
        }

        if (current == '/' && start + 1 < sql.Length && sql[start + 1] == '*')
        {
            var end = sql.IndexOf("*/", start + 2, StringComparison.Ordinal);
            return end < 0 ? sql.Length : end + 2;
        }

        var closingQuote = current switch
        {
            '\'' => '\'',
            '"' => '"',
            '`' => '`',
            '[' => ']',
            _ => '\0'
        };

        if (closingQuote == '\0')
        {
            return start;
        }

        for (var index = start + 1; index < sql.Length; index++)
        {
            if (sql[index] != closingQuote)
            {
                continue;
            }

            // SQL escapes quotes by doubling them
            if (index + 1 < sql.Length && sql[index + 1] == closingQuote)
            {
                index++;
                continue;
            }

            return index + 1;
        }

        return sql.Length;
    }
}
