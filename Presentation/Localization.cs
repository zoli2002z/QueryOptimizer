using System.Collections.Generic;
using QueryOptimizer.Optimization;

namespace QueryOptimizer.Presentation;

public sealed record LanguageOption(string Code, string NativeName, bool IsSelected = false)
{
    public string DisplayCode => Code.ToUpperInvariant();
}

public static class Localization
{
    public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
    {
        new LanguageOption("en", "English"),
        new LanguageOption("hu", "Magyar")
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["AppSubtitle"] = "A focused workspace for better SQL",
        ["LocalWorkspace"] = "LOCAL WORKSPACE",
        ["Language"] = "Language",
        ["ClearSql"] = "Clear",
        ["ClearSqlTooltip"] = "Clear both SQL panels",
        ["ThemeTooltip"] = "Switch between light and dark appearance",
        ["ToggleTheme"] = "Toggle theme",
        ["LightMode"] = "Light mode",
        ["DarkMode"] = "Dark mode",
        ["EnableEditing"] = "Enable editing",
        ["DisableEditing"] = "Disable editing",
        ["InputTitle"] = "Input query",
        ["InputSubtitle"] = "Write SQL or drop in a .sql file",
        ["OpenSql"] = "Open SQL file (Ctrl+O)",
        ["OpenButton"] = "Open",
        ["Optimize"] = "Optimize query  →",
        ["OptimizeTooltip"] = "Apply the available SQL rewrite rules (Ctrl+Enter)",
        ["FormatSql"] = "Format",
        ["FormatTooltip"] = "Put major SQL clauses on separate lines (whitespace only, Ctrl+Shift+F)",
        ["Find"] = "Find",
        ["FindTooltip"] = "Search in the input query (Ctrl+F)",
        ["FindPlaceholder"] = "Find in query…",
        ["FindPrevious"] = "Find previous (Shift+Enter)",
        ["FindNext"] = "Find next (Enter)",
        ["Close"] = "Close",
        ["FormattedQuery"] = "Query formatted",
        ["InputPlaceholder"] = "SELECT customer_id, order_date\nFROM orders\nWHERE order_status = 'open';",
        ["ResultTitle"] = "Optimized result",
        ["ResultSubtitle"] = "Review the rewrite and analysis",
        ["PreviewChanges"] = "Preview changes",
        ["ShowOptimized"] = "Show optimized SQL",
        ["ShowDiffButton"] = "Diff",
        ["ShowSqlButton"] = "SQL",
        ["PreviewTooltip"] = "Show line-by-line differences from the input query",
        ["ShowOptimizedTooltip"] = "Return to the optimized SQL result",
        ["CopySql"] = "Copy optimized SQL",
        ["CopyButton"] = "Copy",
        ["SaveSql"] = "Save optimized SQL (Ctrl+S)",
        ["SaveButton"] = "Save",
        ["EditButton"] = "Edit",
        ["Unsaved"] = "Unsaved",
        ["OutputPlaceholder"] = "Your optimized query will appear here.",
        ["PerformanceNotes"] = "Performance notes",
        ["CopyNotes"] = "Copy notes",
        ["CopyNotesTooltip"] = "Copy all performance notes",
        ["Footer"] = "Static analysis · No database connection",
        ["Ready"] = "Ready when you are",
        ["InitialFindings"] = "Run an analysis to see query patterns and practical suggestions.",
        ["EnterQuery"] = "Enter a query to optimize",
        ["OptimizeBeforeSave"] = "Optimize a query before saving the result",
        ["OptimizeBeforeCopy"] = "Optimize a query before copying the result",
        ["ClipboardUnavailable"] = "Clipboard is unavailable",
        ["CopiedSql"] = "Optimized SQL copied to clipboard",
        ["CopiedNotes"] = "Performance notes copied to clipboard",
        ["EditingDisabled"] = "Optimized SQL editing disabled",
        ["EditingEnabled"] = "Optimized SQL editing enabled",
        ["SelectSqlFile"] = "Select SQL file",
        ["SqlFiles"] = "SQL files",
        ["CouldNotRead"] = "Could not read {0}",
        ["AccessDenied"] = "Access denied for {0}",
        ["SaveBeforeCopy"] = "Optimize a query before copying the result",
        ["CouldNotSave"] = "Could not save {0}",
        ["LoadedFile"] = "Loaded {0}",
        ["SavedFile"] = "Saved {0}",
        ["SummaryChanged"] = "Applied {0} safe rewrite(s); found {1} performance note(s)",
        ["SummaryUnchanged"] = "No safe rewrite applied; found {1} performance note(s)",
        ["RevertRewrite"] = "Revert",
        ["AcceptRewrite"] = "Accept"
    };

    private static readonly IReadOnlyDictionary<string, string> Hungarian = new Dictionary<string, string>
    {
        ["AppSubtitle"] = "Átlátható munkafelület a jobb SQL-lekérdezésekhez",
        ["LocalWorkspace"] = "HELYI MUNKATERÜLET",
        ["Language"] = "Nyelv",
        ["ClearSql"] = "Törlés",
        ["ClearSqlTooltip"] = "Mindkét SQL-panel tartalmának törlése",
        ["ThemeTooltip"] = "Váltás a világos és sötét megjelenés között",
        ["ToggleTheme"] = "Témaváltás",
        ["LightMode"] = "Világos mód",
        ["DarkMode"] = "Sötét mód",
        ["EnableEditing"] = "Szerkesztés engedélyezése",
        ["DisableEditing"] = "Szerkesztés letiltása",
        ["InputTitle"] = "Bemeneti lekérdezés",
        ["InputSubtitle"] = "Írj be SQL-t, vagy dobj ide egy .sql fájlt",
        ["OpenSql"] = "SQL-fájl megnyitása (Ctrl+O)",
        ["OpenButton"] = "Megnyitás",
        ["Optimize"] = "Lekérdezés optimalizálása  →",
        ["OptimizeTooltip"] = "Az elérhető SQL-átalakítási szabályok alkalmazása (Ctrl+Enter)",
        ["FormatSql"] = "Formázás",
        ["FormatTooltip"] = "A fontos SQL-részek új sorba helyezése (csak szóközök változnak, Ctrl+Shift+F)",
        ["Find"] = "Keresés",
        ["FindTooltip"] = "Keresés a bemeneti lekérdezésben (Ctrl+F)",
        ["FindPlaceholder"] = "Keresés a lekérdezésben…",
        ["FindPrevious"] = "Előző találat (Shift+Enter)",
        ["FindNext"] = "Következő találat (Enter)",
        ["Close"] = "Bezárás",
        ["FormattedQuery"] = "Lekérdezés formázva",
        ["InputPlaceholder"] = "SELECT customer_id, order_date\nFROM orders\nWHERE order_status = 'open';",
        ["ResultTitle"] = "Optimalizált eredmény",
        ["ResultSubtitle"] = "Az átalakítás és az elemzés áttekintése",
        ["PreviewChanges"] = "Módosítások előnézete",
        ["ShowOptimized"] = "Optimalizált SQL megjelenítése",
        ["ShowDiffButton"] = "Diff",
        ["ShowSqlButton"] = "SQL",
        ["PreviewTooltip"] = "A bemeneti lekérdezéstől való soronkénti eltérések megjelenítése",
        ["ShowOptimizedTooltip"] = "Vissza az optimalizált SQL-eredményhez",
        ["CopySql"] = "Optimalizált SQL másolása",
        ["CopyButton"] = "Másolás",
        ["SaveSql"] = "Optimalizált SQL mentése (Ctrl+S)",
        ["SaveButton"] = "Mentés",
        ["EditButton"] = "Szerkesztés",
        ["Unsaved"] = "Nincs mentve",
        ["OutputPlaceholder"] = "Az optimalizált lekérdezés itt jelenik meg.",
        ["PerformanceNotes"] = "Teljesítményre vonatkozó megjegyzések",
        ["CopyNotes"] = "Megjegyzések másolása",
        ["CopyNotesTooltip"] = "Az összes teljesítményre vonatkozó megjegyzés másolása",
        ["Footer"] = "Statikus elemzés · Nincs adatbázis-kapcsolat",
        ["Ready"] = "Használatra kész",
        ["InitialFindings"] = "Futtass elemzést a lekérdezési mintákért és a gyakorlati javaslatokért.",
        ["EnterQuery"] = "Adj meg egy optimalizálandó lekérdezést",
        ["OptimizeBeforeSave"] = "Az eredmény mentése előtt optimalizálj egy lekérdezést",
        ["OptimizeBeforeCopy"] = "Az eredmény másolása előtt optimalizálj egy lekérdezést",
        ["ClipboardUnavailable"] = "A vágólap nem érhető el",
        ["CopiedSql"] = "Az optimalizált SQL a vágólapra másolva",
        ["CopiedNotes"] = "A teljesítményre vonatkozó megjegyzések a vágólapra másolva",
        ["EditingDisabled"] = "Az optimalizált SQL szerkesztése letiltva",
        ["EditingEnabled"] = "Az optimalizált SQL szerkesztése engedélyezve",
        ["SelectSqlFile"] = "SQL-fájl kiválasztása",
        ["SqlFiles"] = "SQL-fájlok",
        ["CouldNotRead"] = "Nem sikerült beolvasni: {0}",
        ["AccessDenied"] = "Nincs hozzáférés: {0}",
        ["CouldNotSave"] = "Nem sikerült menteni: {0}",
        ["RevertRewrite"] = "Visszavonás",
        ["AcceptRewrite"] = "Elfogadás"
    };

    public static string Get(string language, string key) =>
        (language == "hu" ? Hungarian : English).TryGetValue(key, out var value) ? value : key;

    public static string TranslateFinding(string language, string finding)
    {
        if (language != "hu") return finding;

        if (finding.StartsWith("SELECT *:", System.StringComparison.Ordinal))
            return "SELECT *: csak a lekérdezéshez szükséges oszlopokat sorold fel. Ez csökkenti az adatok beolvasását, és lehetővé teheti a lefedő index használatát.";
        if (finding.StartsWith("Leading wildcard in LIKE:", System.StringComparison.Ordinal))
            return "Kezdő helyettesítő karakter a LIKE feltételben: a % vagy _ karakterrel kezdődő minta megakadályozza a B-fa index tartománykeresését.";
        if (finding.StartsWith("Function in a WHERE or JOIN predicate:", System.StringComparison.Ordinal))
            return "Függvény a WHERE vagy JOIN feltételben: az oszlopra alkalmazott függvény korlátozza a hagyományos B-fa és bitmap indexek használatát.";
        if (finding.StartsWith("Arithmetic on a predicate column:", System.StringComparison.Ordinal))
            return "Aritmetikai művelet a feltétel oszlopán: az oszlopon végzett műveletek megakadályozhatják az indexkeresést.";
        if (finding.StartsWith("Multiple scalar subqueries on '", System.StringComparison.Ordinal))
        {
            var table = finding.Split('\'')[1];
            return $"Több skalár al-lekérdezés a(z) '{table}' táblán: vond össze az ismétlődő SELECT-eket egy CROSS APPLY / LATERAL vagy LEFT JOIN aggregációba, hogy elkerüld a(z) '{table}' többszöri beolvasását.";
        }
        if (finding.StartsWith("User-defined function in SELECT list:", System.StringComparison.Ordinal))
            return "Felhasználói függvény a SELECT listában: a PL/SQL-függvény soronkénti hívása SQL és PL/SQL közötti kontextusváltást okoz. A lekérdezés logikáját építsd be OUTER APPLY vagy LEFT JOIN használatával.";
        if (finding.StartsWith("Legacy comma join in FROM clause:", System.StringComparison.Ordinal))
            return "Elavult, vesszővel írt összekapcsolás a FROM részben: az egyértelműség és a biztonságosabb összekapcsolási sorrend érdekében használj ANSI JOIN ... ON szintaxist.";
        if (finding.StartsWith("Cursor or WHILE loop found:", System.StringComparison.Ordinal))
            return "Kurzor vagy WHILE ciklus található: vizsgáld meg, kifejezhető-e a művelet halmazalapú utasítással vagy JOIN-nal.";
        if (finding.StartsWith("Correlated subquery present:", System.StringComparison.Ordinal))
            return "Korrelált al-lekérdezés található: ellenőrizd, hogy a korrelációs oszlop indexelése vagy egy származtatott tábla javítja-e a végrehajtási tervet.";
        if (finding == "No remaining performance anti-patterns were detected.")
            return "Nem található további teljesítményt rontó minta.";

        return finding;
    }

    public static string FormatRewrite(AppliedRewrite rewrite, string language)
    {
        var hu = language == "hu";
        var column = rewrite.AfterSnippet;
        var (title, reason) = rewrite.RuleId switch
        {
            "TAUTOLOGY_LEADING_TRUE" => hu
                ? ("A felesleges kezd\u0151 1 = 1 felt\u00E9tel elt\u00E1vol\u00EDt\u00E1sa", "Az 1 = 1 mindig igaz, ez\u00E9rt nem sz\u0171ri az eredm\u00E9nyt.")
                : ("Remove the leading always-true condition", "1 = 1 is always true, so it does not filter the results."),
            "TAUTOLOGY_WHERE_TRUE" => hu
                ? ("Az \u00FCres WHERE felt\u00E9tel elt\u00E1vol\u00EDt\u00E1sa", "A WHERE 1 = 1 minden sort \u00E1tenged, nem sz\u0171ri az eredm\u00E9nyt.")
                : ("Remove the always-true WHERE clause", "WHERE 1 = 1 allows every row through."),
            "TAUTOLOGY_AND_TRUE" => hu
                ? ("A felesleges AND 1 = 1 elt\u00E1vol\u00EDt\u00E1sa", "Az AND 1 = 1 nem v\u00E1ltoztatja meg a felt\u00E9tel eredm\u00E9ny\u00E9t.")
                : ("Remove the unnecessary AND 1 = 1", "AND 1 = 1 does not change the condition's result."),
            "TAUTOLOGY_OR_FALSE" => hu
                ? ("A felesleges OR 1 = 0 elt\u00E1vol\u00EDt\u00E1sa", "Az OR 1 = 0 nem v\u00E1ltoztatja meg a felt\u00E9tel eredm\u00E9ny\u00E9t.")
                : ("Remove the unnecessary OR 1 = 0", "OR 1 = 0 does not change the condition's result."),
            "SARG_IDENTITY_MATH" => hu
                ? ($"Az azonoss\u00E1gi m\u0171velet elhagy\u00E1sa: {column}", $"Egyszer\u0171s\u00EDthet\u0151: {rewrite.BeforeSnippet} \u2192 {column}. Ellen\u0151rizd az adatt\u00EDpust \u00E9s a sz\u00E1m\u00EDt\u00E1si pontoss\u00E1got.")
                : ($"Remove identity arithmetic from {column}", $"{rewrite.BeforeSnippet} can simplify to {column}. Check that the change does not affect the data type or numeric precision."),
            "SARG_UNWRAP_TRIM" => hu
                ? ($"A TRIM elhagy\u00E1sa err\u0151l az oszlopr\u00F3l: {column}", "Ellen\u0151rizd, hogy a sz\u00F3k\u00F6z\u00F6k lev\u00E1g\u00E1sa nem r\u00E9sze-e a k\u00EDv\u00E1nt \u00F6sszehasonl\u00EDt\u00E1snak.")
                : ($"Remove TRIM from {column}", "Check that trimming spaces is not part of the intended comparison."),
            "SARG_UNWRAP_TRUNC_RANGE" => hu
                ? ($"A d\u00E1tumoszlop k\u00F6zvetlen \u00F6sszehasonl\u00EDt\u00E1sa: {column}", "A TRUNC csak akkor hagyhat\u00F3 el, ha a jobb oldali d\u00E1tumhat\u00E1r \u00E9jf\u00E9lre esik.")
                : ($"Compare the date column directly: {column}", "Remove TRUNC only if the right-hand date boundary is at midnight as expected."),
            "SARG_TRUNC_EQUALITY_TO_RANGE" => hu
                ? ("A TRUNC egyenl\u0151s\u00E9g d\u00E1tumtartom\u00E1nny\u00E1 alak\u00EDt\u00E1sa", "Ellen\u0151rizd az oszlop adatt\u00EDpus\u00E1t \u00E9s az id\u0151z\u00F3na-kezel\u00E9st a tartom\u00E1ny elfogad\u00E1sa el\u0151tt.")
                : ("Replace TRUNC equality with a date range", "Check the column type and time-zone behavior before accepting the range."),
            "REDUNDANT_DISTINCT" => hu
                ? ("A DISTINCT elhagy\u00E1sa felt\u00E9telezett egyedis\u00E9g alapj\u00E1n", "Csak akkor fogadd el, ha az Oracle-s\u00E9ma egyedi kulcsa garant\u00E1lja a sorok egyedis\u00E9g\u00E9t.")
                : ("Remove DISTINCT based on assumed uniqueness", "Accept only if a unique key in the Oracle schema guarantees these rows are unique."),
            "SARG_CASE_IN_LIST" => hu
                ? ($"A kis- \u00E9s nagybet\u0171s lista-\u00F6sszehasonl\u00EDt\u00E1s egyszer\u0171s\u00EDt\u00E9se: {column}", "Az eredm\u00E9ny f\u00FCgghet az Oracle-koll\u00E1ci\u00F3t\u00F3l \u00E9s az adatok bet\u0171z\u00E9s\u00E9t\u0151l. Ellen\u0151rizd ezeket elfogad\u00E1s el\u0151tt.")
                : ($"Simplify the case-insensitive list comparison on {column}", "Results can depend on Oracle collation and stored letter casing. Check both before accepting."),
            "SARG_CASE_EQUALITY" => hu
                ? ($"A kis- \u00E9s nagybet\u0171s \u00F6sszehasonl\u00EDt\u00E1s egyszer\u0171s\u00EDt\u00E9se: {column}", "Az eredm\u00E9ny f\u00FCgghet az Oracle-koll\u00E1ci\u00F3t\u00F3l \u00E9s az adatok bet\u0171z\u00E9s\u00E9t\u0151l. Ellen\u0151rizd ezeket elfogad\u00E1s el\u0151tt.")
                : ($"Simplify the case-insensitive comparison on {column}", "Results can depend on Oracle collation and stored letter casing. Check both before accepting."),
            "SUBQUERY_IN_TO_EXISTS" => hu
                ? ("Az IN allek\u00E9rdez\u00E9s \u00E1talak\u00EDt\u00E1sa EXISTS felt\u00E9tell\u00E9", "Ellen\u0151rizd a NULL \u00E9rt\u00E9kek \u00E9s az allek\u00E9rdez\u00E9s eredm\u00E9nyeinek hat\u00E1s\u00E1t elfogad\u00E1s el\u0151tt.")
                : ("Convert the IN subquery to EXISTS", "Check how NULL values and subquery results affect the intended behavior before accepting."),
            _ => (rewrite.Title, rewrite.WhyItMatters)
        };

        return $"{title}{System.Environment.NewLine}{reason}";
    }
}
