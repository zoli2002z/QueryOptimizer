using System;
using System.Collections.Generic;

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

        var appliedRewrites = new List<AppliedRewrite>();
        var optimizedSql = sql;
        string previousSql;

        do
        {
            previousSql = optimizedSql;
            foreach (var rule in _rewriteRules)
                optimizedSql = rule.Apply(optimizedSql, appliedRewrites);
        } while (!string.Equals(previousSql, optimizedSql, StringComparison.Ordinal));

        var notes = _analyzer.Analyze(optimizedSql, includeFallbackMessage: appliedRewrites.Count == 0);
        var diff = _diffBuilder.Build(sql, optimizedSql);
        return new OptimizationResult(sql, optimizedSql, appliedRewrites, notes, diff);
    }

    public IReadOnlyList<string> Analyze(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return _analyzer.Analyze(sql, includeFallbackMessage: true);
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
