# Query Optimizer

Query Optimizer is an Avalonia desktop application for reviewing Oracle SQL queries. It parses input as Oracle SQL, applies a small set of text-based rewrite rules, and reports heuristic performance observations. You can inspect changes in a side-by-side diff, review optional rewrite proposals, and edit or save the resulting SQL.

## Features

- Open a `.sql` file or drop one into the input pane; format the input by moving major clauses onto separate lines.
- Simplify boolean tautologies and identify certain patterns involving arithmetic on columns, `TRIM` or `TRUNC` predicates, case conversion, `DISTINCT`, and `IN` subqueries.
- Review conditional rewrites individually. These proposals are not included in the optimized SQL until accepted; the application labels them for review because their correctness may depend on schema, data, or Oracle settings.
- Compare the input and output with a line-by-line diff, edit the output, copy it, or save it to a `.sql` file.
- View static performance findings and use the interface in English or Hungarian with light or dark appearance.

## Requirements

- .NET 9 SDK

## Run

From the repository directory:

```powershell
dotnet run
```

## How analysis works

The optimizer first parses the query as Oracle SQL. Rewrites and performance findings are produced by local text-processing rules and pattern checks. The tool does not connect to Oracle or another database, inspect schema metadata, execute queries, or read execution plans. Its output is a set of suggestions for review, not a guarantee that a query will run faster or return equivalent results in every environment.

## Project layout

- `Optimization/` contains Oracle SQL parsing integration, rewrite rules, static analysis, text processing, optimization results, and diff generation.
- `Presentation/` contains the Avalonia desktop UI, view models, localization, and user preference stores.
- `Program.cs`, `App.axaml`, and `App.axaml.cs` configure and start the application.
