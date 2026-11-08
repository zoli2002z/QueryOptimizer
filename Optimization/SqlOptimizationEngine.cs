using System;
using System.Collections.Generic;
using SqlParser.Net;

namespace QueryOptimizer.Optimization;

public sealed class SqlOptimizationEngine : ISqlOptimizationService
{
    private readonly IReadOnlyList<ISqlRewriteRule> _rewriteRules;
    private readonly SqlPerformanceAnalyzer _analyzer;
    private readonly SqlLineDiffBuilder _diffBuilder;

    public SqlOptimizationEngine()
    {
        var textProcessor = new SqlTextProcessor();
        _rewriteRules = SqlRewriteRules.CreateDefault(textProcessor);
        _analyzer = new SqlPerformanceAnalyzer(textProcessor);
        _diffBuilder = new SqlLineDiffBuilder();
    }

    internal SqlOptimizationEngine(
        IReadOnlyList<ISqlRewriteRule> rewriteRules,
        SqlPerformanceAnalyzer analyzer,
        SqlLineDiffBuilder diffBuilder)
    {
        _rewriteRules = rewriteRules;
        _analyzer = analyzer;
        _diffBuilder = diffBuilder;
    }

    public OptimizationResult OptimizeWithDetails(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        if (!TryParse(sql, out var parseError))
        {
            var parseNotes = new[] { $"SQL could not be parsed as Oracle SQL. No rewrites were applied. {parseError}" };
            return new OptimizationResult(sql, sql, Array.Empty<AppliedRewrite>(), parseNotes,
                _diffBuilder.Build(sql, sql)) { ParseError = parseError };
        }

        var appliedRewrites = new List<AppliedRewrite>();
        var optimizedSql = sql;
        string previousSql;

        do
        {
            previousSql = optimizedSql;
            foreach (var rule in _rewriteRules)
                optimizedSql = rule.Apply(optimizedSql, appliedRewrites);
        } while (!string.Equals(previousSql, optimizedSql, StringComparison.Ordinal));

        var reviewRequiredRules = new HashSet<string>(StringComparer.Ordinal)
        {
            "SARG_IDENTITY_MATH", "SARG_UNWRAP_TRIM", "SARG_UNWRAP_TRUNC_RANGE",
            "SARG_TRUNC_EQUALITY_TO_RANGE", "REDUNDANT_DISTINCT", "SARG_CASE_IN_LIST",
            "SARG_CASE_EQUALITY", "SUBQUERY_IN_TO_EXISTS"
        };
        for (var index = 0; index < appliedRewrites.Count; index++)
        {
            if (reviewRequiredRules.Contains(appliedRewrites[index].RuleId))
                appliedRewrites[index] = appliedRewrites[index] with { RequiresReview = true };
        }

        // The rewrite pass records proposals too. Roll those proposals back here so
        // library callers, as well as the UI, receive only the non-conditional SQL.
        var safeSql = optimizedSql;
        for (var index = appliedRewrites.Count - 1; index >= 0; index--)
        {
            var rewrite = appliedRewrites[index];
            if (!rewrite.RequiresReview || string.IsNullOrEmpty(rewrite.AfterText)) continue;
            var afterIndex = safeSql.LastIndexOf(rewrite.AfterText, StringComparison.Ordinal);
            if (afterIndex >= 0)
                safeSql = safeSql[..afterIndex] + rewrite.BeforeText + safeSql[(afterIndex + rewrite.AfterText.Length)..];
        }

        if (!TryParse(safeSql, out var rewrittenParseError))
        {
            var validationError = $"A proposed rewrite did not pass Oracle SQL parsing. No rewrites were applied. {rewrittenParseError}";
            return new OptimizationResult(sql, sql, Array.Empty<AppliedRewrite>(), new[] { validationError },
                _diffBuilder.Build(sql, sql)) { ParseError = validationError };
        }

        var notes = _analyzer.Analyze(safeSql, includeFallbackMessage: appliedRewrites.Count == 0);
        var diff = _diffBuilder.Build(sql, safeSql);
        return new OptimizationResult(sql, safeSql, appliedRewrites, notes, diff);
    }

    public IReadOnlyList<string> Analyze(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (!TryParse(sql, out var parseError))
            return new[] { $"SQL could not be parsed as Oracle SQL. Static analysis was skipped. {parseError}" };
        return _analyzer.Analyze(sql, includeFallbackMessage: true);
    }

    private static bool TryParse(string sql, out string error)
    {
        try
        {
            DbUtils.Parse(sql, DbType.Oracle);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            error = exception is SqlParsingErrorException parseException
                ? parseException.Message
                : exception.Message;
            return false;
        }
    }

    public IReadOnlyList<SqlDiffLine> BuildLineDiff(string originalSql, string optimizedSql)
    {
        ArgumentNullException.ThrowIfNull(originalSql);
        ArgumentNullException.ThrowIfNull(optimizedSql);
        return _diffBuilder.Build(originalSql, optimizedSql);
    }
}

internal interface ISqlRewriteRule
{
    string Name { get; }
    string Apply(string sql, List<AppliedRewrite> appliedRewrites);
}

internal sealed class SqlRewriteRule(
    SqlTextProcessor textProcessor,
    string name,
    bool preserveLiteralsAndHints,
    Func<string, List<AppliedRewrite>, string> rewrite) : ISqlRewriteRule
{
    public string Name { get; } = name;

    public string Apply(string sql, List<AppliedRewrite> appliedRewrites)
    {
        var rewriteChunk = new Func<string, string>(chunk => rewrite(chunk, appliedRewrites));
        return preserveLiteralsAndHints
            ? textProcessor.RewriteCodePreservingLiteralsAndHints(sql, rewriteChunk)
            : textProcessor.RewriteCode(sql, rewriteChunk);
    }
}
