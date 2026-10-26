using System.Collections.Generic;
using QueryOptimizer.Optimization;

namespace QueryOptimizer.Presentation.ViewModels;

public interface IDiffPreviewMapper
{
    IReadOnlyList<DiffLineViewModel> Map(OptimizationResult result);
    (IReadOnlyList<DiffSideLineViewModel> Original, IReadOnlyList<DiffSideLineViewModel> Optimized) MapSideBySide(OptimizationResult result);
}
