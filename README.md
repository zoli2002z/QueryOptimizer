# Query Optimizer

A small Avalonia desktop application for reviewing SQL rewrites and static performance findings.

## Requirements

- .NET 9 SDK

## Run

```powershell
dotnet run
```

## Project structure

- `Optimization/` contains optimization results, the optimizer service, rewrite rules, analysis, SQL text processing, and diff generation.
- `Presentation/Views/` contains the Avalonia window and its XAML layout.
- `Presentation/ViewModels/` contains screen state and diff presentation mapping.
- `Presentation/Commands/` contains UI command implementations.

The app performs local static analysis and safe text rewrites. It does not connect to a database or inspect execution plans.
