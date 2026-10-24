using Avalonia.Media;

namespace QueryOptimizer.Presentation.ViewModels;

public sealed record DiffLineViewModel(
    string LineNumber,
    string Content,
    IBrush Background,
    IBrush Foreground,
    string? Tooltip);
