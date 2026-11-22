using System;
using System.Collections.Generic;

namespace QueryOptimizer.Optimization;

/// <summary>
/// Compatibility facade for the SQL optimization services.
/// </summary>
public static class SqlOptimizer
{
    private static readonly ISqlOptimizationService Engine = new SqlOptimizationEngine();

    public static string Optimize(string sql) => Engine.OptimizeWithDetails(sql).OptimizedSql;

    public static OptimizationResult OptimizeWithDetails(string sql) => Engine.OptimizeWithDetails(sql);

    public static IReadOnlyList<string> Analyze(string sql) => Engine.Analyze(sql);

    public static IReadOnlyList<SqlDiffLine> BuildLineDiff(string originalSql, string optimizedSql) =>
        Engine.BuildLineDiff(originalSql, optimizedSql);

}
