using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QueryOptimizer.Optimization;

internal sealed class SqlPerformanceAnalyzer(SqlTextProcessor textProcessor)
{
    private static readonly Regex SelectStar = CreateRegex(
        @"\bSELECT\s+(?:DISTINCT\s+)?(?:[\w$#]+\s*\.\s*)?\*");
    private static readonly Regex LeadingWildcardLike = CreateRegex(@"\bLIKE\s+(?:N\s*)?'[%_]");
    private static readonly Regex FunctionOnPredicateColumn = CreateRegex(
        @"\b(?:WHERE|ON)\b[^;]*?\b(?:YEAR|MONTH|DAY|DATEPART|LOWER|UPPER|TRIM|LTRIM|RTRIM|TRUNC|NVL|ISNULL|COALESCE|CONVERT|CAST|SUBSTRING|SUBSTR|TO_CHAR|TO_DATE)\s*\(\s*[\w$#]+(?:\.[\w$#]+)?\s*(?:,|\))");
    private static readonly Regex ArithmeticOnPredicateColumn = CreateRegex(
        @"\b(?:WHERE|ON)\b[^;]*?\b[\w$#]+(?:\.[\w$#]+)?\s*[+\-*/]\s*\d+");
    private static readonly Regex RowByRowProcessing = CreateRegex(
        @"\b(?:WHILE\b|DECLARE\s+\w+\s+CURSOR\b|OPEN\s+\w+)");
    private static readonly Regex Subquery = CreateRegex(@"\b(?:EXISTS\s*\(|(?:IN|=)\s*\(\s*SELECT\b)");
    private static readonly Regex ScalarSubqueryTable = CreateRegex(
        @"\(\s*SELECT\s+(?:SUM|COUNT|AVG|MIN|MAX)\s*\([^()]+\)\s+FROM\s+([\w$#]+)");
    private static readonly Regex CustomUdfInSelect = CreateRegex(
        @"\bSELECT\b[\s\S]*?\b(?:fn_|ufn_|f_)[\w$#]*\s*\([\s\S]*?\bFROM\b");
    private static readonly Regex ImplicitCommaJoin = CreateRegex(
        @"\bFROM\s+[\w$#]+(?:\s+[\w$#]+)?\s*,\s*[\w$#]+");

    public IReadOnlyList<string> Analyze(string sql, bool includeFallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var analysisText = textProcessor.MaskCommentsAndIdentifiers(sql);
        var likeText = textProcessor.MaskCommentsAndIdentifiers(sql, preserveStringLiterals: true);
        var findings = new List<string>();

        if (SelectStar.IsMatch(analysisText))
            findings.Add("SELECT *: list only the columns the query needs. This reduces data reads and can allow a covering index.");

        if (LeadingWildcardLike.IsMatch(likeText))
            findings.Add("Leading wildcard in LIKE: a pattern starting with % or _ prevents a B-tree index range scan.");

        if (FunctionOnPredicateColumn.IsMatch(analysisText))
            findings.Add("Function in a WHERE or JOIN predicate: applying a function to a column suppresses standard B-tree and bitmap indexes.");

        if (ArithmeticOnPredicateColumn.IsMatch(analysisText))
            findings.Add("Arithmetic on a predicate column: expressions on columns prevent index seeks.");

        if (HasDuplicateScalarSubqueries(analysisText, out var repeatedTable))
            findings.Add($"Multiple scalar subqueries on '{repeatedTable}': coalesce repeated SELECT subqueries into a single CROSS APPLY / LATERAL or LEFT JOIN aggregation to avoid scanning '{repeatedTable}' multiple times.");

        if (CustomUdfInSelect.IsMatch(analysisText))
            findings.Add("User-defined function in SELECT list: calling a PL/SQL function per row causes SQL-to-PL/SQL context switching. Inline the query logic as an OUTER APPLY or LEFT JOIN.");

        if (ImplicitCommaJoin.IsMatch(analysisText))
            findings.Add("Legacy comma join in FROM clause: prefer explicit ANSI JOIN ... ON syntax for clarity and safer join ordering.");

        if (RowByRowProcessing.IsMatch(analysisText))
            findings.Add("Cursor or WHILE loop found: consider whether the work can be expressed as a set-based statement or JOIN.");

        if (Subquery.IsMatch(analysisText))
            findings.Add("Correlated subquery present: verify whether indexing the correlation column or coalescing into a derived table improves the execution plan.");

        if (findings.Count == 0 && includeFallbackMessage)
            findings.Add("No remaining performance anti-patterns were detected.");

        return findings;
    }

    private static bool HasDuplicateScalarSubqueries(string sql, out string tableName)
    {
        var seenTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ScalarSubqueryTable.Matches(sql))
        {
            var table = match.Groups[1].Value;
            if (seenTables.Add(table)) continue;
            tableName = table;
            return true;
        }

        tableName = string.Empty;
        return false;
    }

    private static Regex CreateRegex(string pattern) => new(
        pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
