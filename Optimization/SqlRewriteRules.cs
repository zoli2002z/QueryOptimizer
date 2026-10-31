using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QueryOptimizer.Optimization;

internal static class SqlRewriteRules
{
    private static readonly Regex TrueConjunction = CreateRegex(@"\bAND\s+1\s*=\s*1\b");
    private static readonly Regex FalseDisjunction = CreateRegex(@"\bOR\s+1\s*=\s*0\b");
    private static readonly Regex LeadingTrueCondition = CreateRegex(@"(\bWHERE\s+)(1\s*=\s*1\s+AND)\b");
    private static readonly Regex StandaloneTrueCondition = CreateRegex(
        @"\bWHERE\s+1\s*=\s*1(?=\s*(?:GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|OFFSET|FETCH|FOR|;|$))");
    private static readonly Regex IdentityArithmetic = CreateRegex(
        @"(\b[\w$#]+(?:\.[\w$#]+)?)\s*(?:[+-]\s*0|\*\s*1|/\s*1)\b");
    private static readonly Regex TrimOnPredicateColumn = CreateRegex(
        @"\b[LR]?TRIM\s*\(\s*([\w$#]+(?:\.[\w$#]+)?)\s*\)(?=\s*(?:=|!=|<>|IN\b))");
    private static readonly Regex TruncOnDateRangePredicate = CreateRegex(
        @"\bTRUNC\s*\(\s*([\w$#]+(?:\.[\w$#]+)?)\s*\)(?=\s*(?:>=|<)\s*(?:ADD_MONTHS\s*\(\s*TRUNC\b|TRUNC\b|DATE\b))");
    private static readonly Regex TruncOnDateEqualityPredicate = CreateRegex(
        @"\bTRUNC\s*\(\s*([\w$#]+(?:\.[\w$#]+)?)\s*\)\s*=\s*(TRUNC\s*\([^()\r\n]+\))");
    private static readonly Regex CaseFunctionInList = CreateRegex(
        @"\b(?<fn>LOWER|UPPER)\s*\(\s*(?<col>[\w$#]+(?:\.[\w$#]+)?)\s*\)\s+IN\s*\(\s*(?<list>'(?:''|[^'])*'(?:\s*,\s*'(?:''|[^'])*')*)\s*\)");
    private static readonly Regex CaseFunctionEquality = CreateRegex(
        @"\b(?<fn>LOWER|UPPER)\s*\(\s*(?<col>[\w$#]+(?:\.[\w$#]+)?)\s*\)\s*(?<op>=|!=|<>)\s*(?<lit>'(?:''|[^'])*')");
    private static readonly Regex RedundantDistinctWithKey = CreateRegex(
        @"(\bSELECT(?:\s+/\*\+[\s\S]*?\*/)?)\s+DISTINCT(?=\s+(?:[\w$#]+\.)?(?:order_id|customer_id|product_id|region_id|id)\b)");
    private static readonly Regex InSubqueryToExists = CreateRegex(
        @"\b(?<outer>[\w$#]+\.[\w$#]+)\s+IN\s*\((?<lead>\s*)SELECT\s+(?<inner>[\w$#]+\.[\w$#]+)(?<from>\s+FROM\s+[\s\S]+?\r?\n(?<whereIndent>[ \t]*)WHERE\s+)(?<where>(?:[^()]|\([^()]*\))+?)\)");

    public static IReadOnlyList<ISqlRewriteRule> CreateDefault(SqlTextProcessor? textProcessor = null)
    {
        textProcessor ??= new SqlTextProcessor();
        return new ISqlRewriteRule[]
        {
            Rule(textProcessor, "TAUTOLOGY_LEADING_TRUE", RemoveLeadingTrueCondition),
            Rule(textProcessor, "TAUTOLOGY_WHERE_TRUE", RemoveStandaloneTrueCondition),
            Rule(textProcessor, "TAUTOLOGY_AND_TRUE", RemoveAlwaysTrueConjuncts),
            Rule(textProcessor, "TAUTOLOGY_OR_FALSE", RemoveAlwaysFalseDisjuncts),
            Rule(textProcessor, "SARG_IDENTITY_MATH", RemoveIdentityArithmetic),
            Rule(textProcessor, "SARG_UNWRAP_TRIM", UnwrapTrimPredicate),
            Rule(textProcessor, "SARG_UNWRAP_TRUNC_RANGE", UnwrapTruncDateRangePredicate),
            Rule(textProcessor, "SARG_TRUNC_EQUALITY_TO_RANGE", RewriteTruncDateEqualityPredicate),
            Rule(textProcessor, "REDUNDANT_DISTINCT", RemoveRedundantDistinct, preserveLiteralsAndHints: true),
            Rule(textProcessor, "SARG_CASE_FUNCTION", UnwrapCaseFunctionOnLiterals, preserveLiteralsAndHints: true),
            Rule(textProcessor, "SUBQUERY_IN_TO_EXISTS", ConvertInSubqueryToExists, preserveLiteralsAndHints: true)
        };
    }

    private static ISqlRewriteRule Rule(
        SqlTextProcessor textProcessor,
        string name,
        Func<string, List<AppliedRewrite>, string> rewrite,
        bool preserveLiteralsAndHints = false) =>
        new SqlRewriteRule(textProcessor, name, preserveLiteralsAndHints, rewrite);

    private static string RemoveLeadingTrueCondition(string sql, List<AppliedRewrite> log) =>
        LeadingTrueCondition.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_LEADING_TRUE", "Simplification", "Removed redundant '1 = 1 AND'",
                match.Value.Trim(), "WHERE", "Strips unnecessary boolean tautologies from the WHERE clause.")
                { BeforeText = match.Value, AfterText = match.Groups[1].Value });
            return match.Groups[1].Value;
        });

    private static string RemoveStandaloneTrueCondition(string sql, List<AppliedRewrite> log) =>
        StandaloneTrueCondition.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_WHERE_TRUE", "Simplification", "Removed standalone 'WHERE 1 = 1'",
                match.Value.Trim(), "(Removed)", "Eliminates a filter clause that evaluates to true for every row.")
                { BeforeText = match.Value, AfterText = string.Empty });
            return string.Empty;
        });

    private static string RemoveAlwaysTrueConjuncts(string sql, List<AppliedRewrite> log) =>
        TrueConjunction.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_AND_TRUE", "Simplification", "Removed always-true 'AND 1 = 1'",
                match.Value.Trim(), "(Removed)", "Simplifies predicate evaluation by removing redundant conjunctions.")
                { BeforeText = match.Value, AfterText = string.Empty });
            return string.Empty;
        });

    private static string RemoveAlwaysFalseDisjuncts(string sql, List<AppliedRewrite> log) =>
        FalseDisjunction.Replace(sql, match =>
        {
            log.Add(new AppliedRewrite(
                "TAUTOLOGY_OR_FALSE", "Simplification", "Removed always-false 'OR 1 = 0'",
                match.Value.Trim(), "(Removed)", "Simplifies predicate evaluation by removing dead disjunction branches.")
                { BeforeText = match.Value, AfterText = string.Empty });
            return string.Empty;
        });

    private static string RemoveIdentityArithmetic(string sql, List<AppliedRewrite> log) =>
        IdentityArithmetic.Replace(sql, match =>
        {
            var column = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "SARG_IDENTITY_MATH", "SARGability", $"Removed identity math on '{column}'",
                match.Value.Trim(), column, $"Arithmetic expressions like '{match.Value.Trim()}' disable B-Tree index usage on '{column}'.")
                { BeforeText = match.Value, AfterText = column });
            return column;
        });

    private static string UnwrapTrimPredicate(string sql, List<AppliedRewrite> log) =>
        TrimOnPredicateColumn.Replace(sql, match =>
        {
            var column = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "SARG_UNWRAP_TRIM", "SARGability", $"Unwrapped TRIM() on '{column}'",
                match.Value.Trim(), column, $"Wrapping '{column}' in TRIM() prevents the optimizer from using its index and column histogram.")
                { BeforeText = match.Value, AfterText = column });
            return column;
        });

    private static string UnwrapTruncDateRangePredicate(string sql, List<AppliedRewrite> log) =>
        TruncOnDateRangePredicate.Replace(sql, match =>
        {
            var column = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "SARG_UNWRAP_TRUNC_RANGE", "SARGability", $"Removed redundant TRUNC() on '{column}'",
                match.Value.Trim(), column, $"Since the right-hand boundary is already truncated to midnight, comparing '{column}' directly enables an Index Range Scan.")
                { BeforeText = match.Value, AfterText = column });
            return column;
        });

    private static string RewriteTruncDateEqualityPredicate(string sql, List<AppliedRewrite> log) =>
        TruncOnDateEqualityPredicate.Replace(sql, match =>
        {
            var column = match.Groups[1].Value;
            var rhs = match.Groups[2].Value;
            var rewritten = $"{column} >= {rhs} AND {column} < {rhs} + 1";
            log.Add(new AppliedRewrite(
                "SARG_TRUNC_EQUALITY_TO_RANGE", "SARGability", $"Converted TRUNC({column}) equality to half-open date range",
                match.Value.Trim(), rewritten, $"Replaces function-wrapped date equality with an index-friendly range scan on '{column}'.")
                { BeforeText = match.Value, AfterText = rewritten });
            return rewritten;
        });

    private static string RemoveRedundantDistinct(string sql, List<AppliedRewrite> log) =>
        RedundantDistinctWithKey.Replace(sql, match =>
        {
            var selectPrefix = match.Groups[1].Value;
            log.Add(new AppliedRewrite(
                "REDUNDANT_DISTINCT", "Sort Elimination", "Removed redundant DISTINCT keyword",
                "SELECT ... DISTINCT", "SELECT", "A selected key already makes these rows unique, so DISTINCT adds an unnecessary sort.")
                { BeforeText = match.Value, AfterText = selectPrefix });
            return selectPrefix;
        });

    private static string UnwrapCaseFunctionOnLiterals(string sql, List<AppliedRewrite> log)
    {
        var result = CaseFunctionInList.Replace(sql, match =>
        {
            var function = match.Groups["fn"].Value.ToUpperInvariant();
            var column = match.Groups["col"].Value;
            var list = match.Groups["list"].Value.ToUpperInvariant();
            var rewritten = $"{column} IN ({list})";
            log.Add(new AppliedRewrite(
                "SARG_CASE_IN_LIST", "SARGability", $"Normalized {function}({column}) IN (...) to uppercase literals",
                match.Value.Trim(), rewritten, $"Removes the {function}() wrapper from '{column}' so the optimizer can use bitmap/B-Tree indexes and histograms.")
                { BeforeText = match.Value, AfterText = rewritten });
            return rewritten;
        });

        return CaseFunctionEquality.Replace(result, match =>
        {
            var function = match.Groups["fn"].Value.ToUpperInvariant();
            var column = match.Groups["col"].Value;
            var operation = match.Groups["op"].Value;
            var literal = match.Groups["lit"].Value;
            if (literal.Contains('@', StringComparison.Ordinal)) return match.Value;

            var rewritten = $"{column} {operation} {literal.ToUpperInvariant()}";
            log.Add(new AppliedRewrite(
                "SARG_CASE_EQUALITY", "SARGability", $"Normalized {function}({column}) comparison",
                match.Value.Trim(), rewritten, $"Removes {function}() from '{column}' to restore direct index access.")
                { BeforeText = match.Value, AfterText = rewritten });
            return rewritten;
        });
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
                $"Allows the optimizer to use a semi-join / composite primary key probe on '{inner}' and stop at the first matching row.")
                { BeforeText = match.Value, AfterText = rewritten });
            return rewritten;
        });

    private static Regex CreateRegex(string pattern) => new(
        pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
