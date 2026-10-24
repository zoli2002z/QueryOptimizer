using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QueryOptimizer.Optimization;
using QueryOptimizer.Presentation.ViewModels;
using QueryOptimizer.Presentation.Views;

namespace QueryOptimizer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel(new SqlOptimizationEngine(), new DiffPreviewMapper());
            desktop.MainWindow = new MainWindow(viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
