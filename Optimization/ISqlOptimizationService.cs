using System.Collections.Generic;

namespace QueryOptimizer.Optimization;

public interface ISqlOptimizationService
{
    OptimizationResult OptimizeWithDetails(string sql);
    IReadOnlyList<string> Analyze(string sql);
    IReadOnlyList<SqlDiffLine> BuildLineDiff(string originalSql, string optimizedSql);
}
