using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace QueryOptimizer;

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
    string WhyItMatters);

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

public static class SqlOptimizer
{
    // boolean tautology rewrite rules
    private static readonly Regex TrueConjunction = new(
        @"\bAND\s+1\s*=\s*1\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FalseDisjunction = new(
        @"\bOR\s+1\s*=\s*0\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingTrueCondition = new(
        @"(\bWHERE\s+)(1\s*=\s*1\s+AND)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StandaloneTrueCondition = new(
        @"\bWHERE\s+1\s*=\s*1(?=\s*(?:GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|OFFSET|FETCH|FOR|;|$))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // sargability and syntactic rewrite rules
    private static readonly Regex IdentityArithmetic = new(
        @"(\b[\w$#]+(?:\.[\w$#]+)?)\s*(?:[+-]\s*0|\*\s*1|/\s*1)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TrimOnPredicateColumn = new(
        @"\b[LR]?TRIM\s*\(\s*([\w$#]+(?:\.[\w$#]+)?)\s*\)(?=\s*(?:=|!=|<>|IN\b))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TruncOnDateRangePredicate = new(
        @"\bTRUNC\s*\(\s*([\w$#]+(?:\.[\w$#]+)?)\s*\)(?=\s*(?:>=|<)\s*(?:ADD_MONTHS\s*\(\s*TRUNC\b|TRUNC\b|DATE\b))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TruncOnDateEqualityPredicate = new(
        @"\bTRUNC\s*\(\s*([\w$#]+(?:\.[\w$#]+)?)\s*\)\s*=\s*(TRUNC\s*\([^()\r\n]+\))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CaseFunctionInList = new(
        @"\b(?<fn>LOWER|UPPER)\s*\(\s*(?<col>[\w$#]+(?:\.[\w$#]+)?)\s*\)\s+IN\s*\(\s*(?<list>'(?:''|[^'])*'(?:\s*,\s*'(?:''|[^'])*')*)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CaseFunctionEquality = new(
        @"\b(?<fn>LOWER|UPPER)\s*\(\s*(?<col>[\w$#]+(?:\.[\w$#]+)?)\s*\)\s*(?<op>=|!=|<>)\s*(?<lit>'(?:''|[^'])*')",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RedundantDistinctWithKey = new(
        @"(\bSELECT(?:\s+/\*\+[\s\S]*?\*/)?)\s+DISTINCT(?=\s+(?:[\w$#]+\.)?(?:order_id|customer_id|product_id|region_id|id)\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InSubqueryToExists = new(
        @"\b(?<outer>[\w$#]+\.[\w$#]+)\s+IN\s*\((?<lead>\s*)SELECT\s+(?<inner>[\w$#]+\.[\w$#]+)(?<from>\s+FROM\s+[\s\S]+?\r?\n(?<whereIndent>[ \t]*)WHERE\s+)(?<where>(?:[^()]|\([^()]*\))+?)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // static analysis detection rules
    private static readonly Regex SelectStar = new(
        @"\bSELECT\s+(?:DISTINCT\s+)?(?:[\w$#]+\s*\.\s*)?\*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingWildcardLike = new(
        @"\bLIKE\s+(?:N\s*)?'[%_]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FunctionOnPredicateColumn = new(
        @"\b(?:WHERE|ON)\b[^;]*?\b(?:YEAR|MONTH|DAY|DATEPART|LOWER|UPPER|TRIM|LTRIM|RTRIM|TRUNC|NVL|ISNULL|COALESCE|CONVERT|CAST|SUBSTRING|SUBSTR|TO_CHAR|TO_DATE)\s*\(\s*[\w$#]+(?:\.[\w$#]+)?\s*(?:,|\))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ArithmeticOnPredicateColumn = new(
        @"\b(?:WHERE|ON)\b[^;]*?\b[\w$#]+(?:\.[\w$#]+)?\s*[+\-*/]\s*\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RowByRowProcessing = new(
        @"\b(?:WHILE\b|DECLARE\s+\w+\s+CURSOR\b|OPEN\s+\w+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Subquery = new(
        @"\b(?:EXISTS\s*\(|(?:IN|=)\s*\(\s*SELECT\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ScalarSubqueryTable = new(
        @"\(\s*SELECT\s+(?:SUM|COUNT|AVG|MIN|MAX)\s*\([^()]+\)\s+FROM\s+([\w$#]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CustomUdfInSelect = new(
        @"\bSELECT\b[\s\S]*?\b(?:fn_|ufn_|f_)[\w$#]*\s*\([\s\S]*?\bFROM\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ImplicitCommaJoin = new(
        @"\bFROM\s+[\w$#]+(?:\s+[\w$#]+)?\s*,\s*[\w$#]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // the legacy method keeps existing callers compatible
    public static string Optimize(string sql) => OptimizeWithDetails(sql).OptimizedSql;

    // the detailed method returns rewrites notes and line differences
    public static OptimizationResult OptimizeWithDetails(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var rewrites = new List<AppliedRewrite>();
        var result = sql;
        string previous;

        do
        {
            previous = result;

            result = RewriteCode(result, chunk => RemoveLeadingTrueCondition(chunk, rewrites));
            result = RewriteCode(result, chunk => RemoveStandaloneTrueCondition(chunk, rewrites));
            result = RewriteCode(result, chunk => RemoveAlwaysTrueConjuncts(chunk, rewrites));
            result = RewriteCode(result, chunk => RemoveAlwaysFalseDisjuncts(chunk, rewrites));
            result = RewriteCode(result, chunk => RemoveIdentityArithmetic(chunk, rewrites));
            result = RewriteCode(result, chunk => UnwrapTrimPredicate(chunk, rewrites));
            result = RewriteCode(result, chunk => UnwrapTruncDateRangePredicate(chunk, rewrites));
            result = RewriteCode(result, chunk => RewriteTruncDateEqualityPredicate(chunk, rewrites));

            result = RewriteCodePreservingLiteralsAndHints(result, chunk => RemoveRedundantDistinct(chunk, rewrites));
            result = RewriteCodePreservingLiteralsAndHints(result, chunk => UnwrapCaseFunctionOnLiterals(chunk, rewrites));
            result = RewriteCodePreservingLiteralsAndHints(result, chunk => ConvertInSubqueryToExists(chunk, rewrites));
        } while (!string.Equals(previous, result, StringComparison.Ordinal));

        var remainingNotes = Analyze(result, includeFallbackMessage: rewrites.Count == 0);
        var diffLines = BuildLineDiff(sql, result);

        return new OptimizationResult(sql, result, rewrites, remainingNotes, diffLines);
    }

    public static IReadOnlyList<string> Analyze(string sql) =>
        Analyze(sql, includeFallbackMessage: true);

    private static IReadOnlyList<string> Analyze(string sql, bool includeFallbackMessage)
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
            findings.Add("Leading wildcard in LIKE: a pattern starting with % or _ prevents a B-tree index range scan.");
        }

        if (FunctionOnPredicateColumn.IsMatch(analysisText))
        {
            findings.Add("Function in a WHERE or JOIN predicate: applying a function to a column suppresses standard B-tree and bitmap indexes.");
        }

        if (ArithmeticOnPredicateColumn.IsMatch(analysisText))
        {
            findings.Add("Arithmetic on a predicate column: expressions on columns prevent index seeks.");
        }

        if (HasDuplicateScalarSubqueries(analysisText, out var repeatedTable))
        {
            findings.Add($"Multiple scalar subqueries on '{repeatedTable}': coalesce repeated SELECT subqueries into a single CROSS APPLY / LATERAL or LEFT JOIN aggregation to avoid scanning '{repeatedTable}' multiple times.");
        }

        if (CustomUdfInSelect.IsMatch(analysisText))
        {
            findings.Add("User-defined function in SELECT list: calling a PL/SQL function per row causes SQL-to-PL/SQL context switching. Inline the query logic as an OUTER APPLY or LEFT JOIN.");
        }

        if (ImplicitCommaJoin.IsMatch(analysisText))
        {
            findings.Add("Legacy comma join in FROM clause: prefer explicit ANSI JOIN ... ON syntax for clarity and safer join ordering.");
        }

        if (RowByRowProcessing.IsMatch(analysisText))
        {
            findings.Add("Cursor or WHILE loop found: consider whether the work can be expressed as a set-based statement or JOIN.");
        }

        if (Subquery.IsMatch(analysisText))
        {
            findings.Add("Correlated subquery present: verify whether indexing the correlation column or coalescing into a derived table improves the execution plan.");
        }

        if (findings.Count == 0 && includeFallbackMessage)
        {
            findings.Add("No remaining performance anti-patterns were detected.");
        }

        return findings;
    }

    // tracked rewrite implementations

    private static string RemoveLeadingTrueCondition(string sql, List<AppliedRewrite> log) =>
        LeadingTrueCondition.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_LEADING_TRUE", "Simplification", "Removed redundant '1 = 1 AND'",
                match.Value.Trim(), "WHERE", "Strips unnecessary boolean tautologies from the WHERE clause."));
            return match.Groups[1].Value;
        });

    private static string RemoveStandaloneTrueCondition(string sql, List<AppliedRewrite> log) =>
        StandaloneTrueCondition.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_WHERE_TRUE", "Simplification", "Removed standalone 'WHERE 1 = 1'",
                match.Value.Trim(), "(Removed)", "Eliminates a filter clause that evaluates to true for every row."));
            return string.Empty;
        });

    private static string RemoveAlwaysTrueConjuncts(string sql, List<AppliedRewrite> log) =>
        TrueConjunction.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_AND_TRUE", "Simplification", "Removed always-true 'AND 1 = 1'",
                match.Value.Trim(), "(Removed)", "Simplifies predicate evaluation by removing redundant conjunctions."));
            return string.Empty;
        });

    private static string RemoveAlwaysFalseDisjuncts(string sql, List<AppliedRewrite> log) =>
        FalseDisjunction.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_OR_FALSE", "Simplification", "Removed always-false 'OR 1 = 0'",
                match.Value.Trim(), "(Removed)", "Simplifies predicate evaluation by removing dead disjunction branches."));
            return string.Empty;
        });

    private static string RemoveIdentityArithmetic(string sql, List<AppliedRewrite> log) =>
        IdentityArithmetic.Replace(sql, match =>
        {
            var col = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "SARG_IDENTITY_MATH", "SARGability", $"Removed identity math on '{col}'",
                match.Value.Trim(), col, $"Arithmetic expressions like '{match.Value.Trim()}' disable B-Tree index usage on '{col}'."));
            return col;
        });

    private static string UnwrapTrimPredicate(string sql, List<AppliedRewrite> log) =>
        TrimOnPredicateColumn.Replace(sql, match =>
        {
            var col = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "SARG_UNWRAP_TRIM", "SARGability", $"Unwrapped TRIM() on '{col}'",
                match.Value.Trim(), col, $"Wrapping '{col}' in TRIM() prevents the optimizer from using its index and column histogram."));
            return col;
        });

    private static string UnwrapTruncDateRangePredicate(string sql, List<AppliedRewrite> log) =>
        TruncOnDateRangePredicate.Replace(sql, match =>
        {
            var col = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "SARG_UNWRAP_TRUNC_RANGE", "SARGability", $"Removed redundant TRUNC() on '{col}'",
                match.Value.Trim(), col, $"Since the right-hand boundary is already truncated to midnight, comparing '{col}' directly enables an Index Range Scan."));
            return col;
        });

    private static string RewriteTruncDateEqualityPredicate(string sql, List<AppliedRewrite> log) =>
        TruncOnDateEqualityPredicate.Replace(sql, match =>
        {
            var col = match.Groups[1].Value;
            var rhs = match.Groups[2].Value;
            var rewritten = $"{col} >= {rhs} AND {col} < {rhs} + 1";
            log.Add(new AppliedRewrite(
                "SARG_TRUNC_EQUALITY_TO_RANGE", "SARGability", $"Converted TRUNC({col}) equality to half-open date range",
                match.Value.Trim(), rewritten, $"Replaces function-wrapped date equality with an index-friendly range scan on '{col}'."));
            return rewritten;
        });

    private static string RemoveRedundantDistinct(string sql, List<AppliedRewrite> log) =>
        RedundantDistinctWithKey.Replace(sql, match =>
        {
            var selectPrefix = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "REDUNDANT_DISTINCT", "Sort Elimination", "Removed redundant DISTINCT keyword",
                "SELECT ... DISTINCT", "SELECT", "A selected key already makes these rows unique, so DISTINCT adds an unnecessary sort."));
            return selectPrefix;
        });

    private static string UnwrapCaseFunctionOnLiterals(string sql, List<AppliedRewrite> log)
    {
        var result = CaseFunctionInList.Replace(sql, match =>
        {
            var fn = match.Groups["fn"].Value.ToUpperInvariant();
            var col = match.Groups["col"].Value;
            var list = match.Groups["list"].Value.ToUpperInvariant();
            var rewritten = $"{col} IN ({list})";

            log.Add(new AppliedRewrite(
                "SARG_CASE_IN_LIST", "SARGability", $"Normalized {fn}({col}) IN (...) to uppercase literals",
                match.Value.Trim(), rewritten, $"Removes the {fn}() wrapper from '{col}' so the optimizer can use bitmap/B-Tree indexes and histograms."));
            return rewritten;
        });

        result = CaseFunctionEquality.Replace(result, match =>
        {
            var fn = match.Groups["fn"].Value.ToUpperInvariant();
            var col = match.Groups["col"].Value;
            var op = match.Groups["op"].Value;
            var literal = match.Groups["lit"].Value;
            if (literal.Contains('@', StringComparison.Ordinal)) return match.Value;

            var rewritten = $"{col} {op} {literal.ToUpperInvariant()}";
            log.Add(new AppliedRewrite(
                "SARG_CASE_EQUALITY", "SARGability", $"Normalized {fn}({col}) comparison",
                match.Value.Trim(), rewritten, $"Removes {fn}() from '{col}' to restore direct index access."));
            return rewritten;
        });

        return result;
    }

    private static string ConvertInSubqueryToExists(string sql, List<AppliedRewrite> log) =>
        InSubqueryToExists.Replace(sql, match =>
        {
            var outer = match.Groups["outer"].Value;
            var lead = match.Groups["lead"].Value;
            var inner = match.Groups["inner"].Value;
            var fromClause = match.Groups["from"].Value;
            var whereIndent = match.Groups["whereIndent"].Value;
            var whereClause = match.Groups["where"].Value;

            var rewritten = $"EXISTS ({lead}SELECT 1{fromClause}{inner} = {outer}\r\n{whereIndent}  AND {whereClause})";
            log.Add(new AppliedRewrite(
                "SUBQUERY_IN_TO_EXISTS", "Subquery Tuning", $"Converted '{outer} IN (SELECT ...)' to correlated EXISTS",
                $"{outer} IN (SELECT {inner} ...)", $"EXISTS (SELECT 1 ... WHERE {inner} = {outer})",
                $"Allows the optimizer to use a semi-join / composite primary key probe on '{inner}' and stop at the first matching row."));
            return rewritten;
        });

    public static IReadOnlyList<SqlDiffLine> BuildLineDiff(string originalSql, string optimizedSql)
    {
        var oldLines = SplitLines(originalSql);
        var newLines = SplitLines(optimizedSql);

        var m = oldLines.Length;
        var n = newLines.Length;
        var lcs = new int[m + 1, n + 1];

        for (var i = m - 1; i >= 0; i--)
        {
            for (var j = n - 1; j >= 0; j--)
            {
                if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                    lcs[i, j] = 1 + lcs[i + 1, j + 1];
                else
                    lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var diff = new List<SqlDiffLine>(m + n);
        int x = 0, y = 0;

        while (x < m && y < n)
        {
            if (string.Equals(oldLines[x], newLines[y], StringComparison.Ordinal))
            {
                diff.Add(new SqlDiffLine(DiffType.Unchanged, x + 1, y + 1, oldLines[x]));
                x++; y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                diff.Add(new SqlDiffLine(DiffType.Removed, x + 1, null, oldLines[x]));
                x++;
            }
            else
            {
                diff.Add(new SqlDiffLine(DiffType.Added, null, y + 1, newLines[y]));
                y++;
            }
        }

        while (x < m) { diff.Add(new SqlDiffLine(DiffType.Removed, x + 1, null, oldLines[x])); x++; }
        while (y < n) { diff.Add(new SqlDiffLine(DiffType.Added, null, y + 1, newLines[y])); y++; }

        return diff;
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static bool HasDuplicateScalarSubqueries(string sql, out string tableName)
    {
        var seenTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ScalarSubqueryTable.Matches(sql))
        {
            var table = match.Groups[1].Value;
            if (!seenTables.Add(table)) { tableName = table; return true; }
        }
        tableName = string.Empty;
        return false;
    }

    private static string MaskCommentsAndIdentifiers(string sql, bool preserveStringLiterals = false)
    {
        var result = new StringBuilder(sql);
        var index = 0;

        while (index < sql.Length)
        {
            if (sql[index] == '\'')
            {
                var stringEnd = GetProtectedRegionEnd(sql, index);
                if (!preserveStringLiterals) MaskRegion(result, sql, index, stringEnd);
                index = stringEnd;
                continue;
            }

            var commentStart = (sql[index] == '-' && index + 1 < sql.Length && sql[index + 1] == '-') ||
                               (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*');
            var identifierQuote = sql[index] is '"' or '`' or '[';

            if (!commentStart && !identifierQuote) { index++; continue; }

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
            if (source[index] is not '\r' and not '\n') result[index] = ' ';
        }
    }

    private static string RewriteCode(string sql, Func<string, string> rewrite)
    {
        var result = new StringBuilder(sql.Length);
        var codeStart = 0;
        var index = 0;

        while (index < sql.Length)
        {
            var protectedEnd = GetProtectedRegionEnd(sql, index);
            if (protectedEnd == index) { index++; continue; }

            result.Append(rewrite(sql[codeStart..index]));
            result.Append(sql[index..protectedEnd]);
            index = protectedEnd;
            codeStart = index;
        }

        result.Append(rewrite(sql[codeStart..]));
        return result.ToString();
    }

    private static string RewriteCodePreservingLiteralsAndHints(string sql, Func<string, string> rewrite)
    {
        var result = new StringBuilder(sql.Length);
        var codeStart = 0;
        var index = 0;

        while (index < sql.Length)
        {
            var isStringLiteral = sql[index] == '\'';
            var isOptimizerHint = sql[index] == '/' && index + 2 < sql.Length && sql[index + 1] == '*' && sql[index + 2] == '+';

            if (isStringLiteral || isOptimizerHint) { index = GetProtectedRegionEnd(sql, index); continue; }

            var protectedEnd = GetProtectedRegionEnd(sql, index);
            if (protectedEnd == index) { index++; continue; }

            result.Append(rewrite(sql[codeStart..index]));
            result.Append(sql[index..protectedEnd]);
            index = protectedEnd;
            codeStart = index;
        }

        result.Append(rewrite(sql[codeStart..]));
        return result.ToString();
    }

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

        var closingQuote = current switch { '\'' => '\'', '"' => '"', '`' => '`', '[' => ']', _ => '\0' };
        if (closingQuote == '\0') return start;

        for (var index = start + 1; index < sql.Length; index++)
        {
            if (sql[index] != closingQuote) continue;
            if (index + 1 < sql.Length && sql[index + 1] == closingQuote) { index++; continue; }
            return index + 1;
        }

        return sql.Length;
    }
}
