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
        ["ThemeTooltip"] = "Switch between light and dark appearance",
        ["ToggleTheme"] = "Toggle theme",
        ["LightMode"] = "Light mode",
        ["DarkMode"] = "Dark mode",
        ["EnableEditing"] = "Enable editing",
        ["DisableEditing"] = "Disable editing",
        ["InputTitle"] = "Input query",
        ["InputSubtitle"] = "Write SQL or drop in a .sql file",
        ["OpenSql"] = "Open SQL file",
        ["Optimize"] = "Optimize query  →",
        ["OptimizeTooltip"] = "Apply the available SQL rewrite rules",
        ["InputPlaceholder"] = "SELECT customer_id, order_date\nFROM orders\nWHERE order_status = 'open';",
        ["ResultTitle"] = "Optimized result",
        ["ResultSubtitle"] = "Review the rewrite and analysis",
        ["PreviewChanges"] = "Preview changes",
        ["ShowOptimized"] = "Show optimized SQL",
        ["PreviewTooltip"] = "Show line-by-line differences from the input query",
        ["CopySql"] = "Copy optimized SQL",
        ["SaveSql"] = "Save optimized SQL",
        ["Unsaved"] = "Unsaved",
        ["OutputPlaceholder"] = "Your optimized query will appear here.",
        ["PerformanceNotes"] = "Performance notes",
        ["CopyNotes"] = "Copy notes",
        ["CopyNotesTooltip"] = "Copy all performance notes",
        ["SplitterTooltip"] = "Drag to resize the SQL panes",
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
        ["SummaryUnchanged"] = "No safe rewrite applied; found {1} performance note(s)"
    };

    private static readonly IReadOnlyDictionary<string, string> Hungarian = new Dictionary<string, string>
    {
        ["AppSubtitle"] = "Átlátható munkafelület a jobb SQL-lekérdezésekhez",
        ["LocalWorkspace"] = "HELYI MUNKATERÜLET",
        ["Language"] = "Nyelv",
        ["ThemeTooltip"] = "Váltás a világos és sötét megjelenés között",
        ["ToggleTheme"] = "Témaváltás",
        ["LightMode"] = "Világos mód",
        ["DarkMode"] = "Sötét mód",
        ["EnableEditing"] = "Szerkesztés engedélyezése",
        ["DisableEditing"] = "Szerkesztés letiltása",
        ["InputTitle"] = "Bemeneti lekérdezés",
        ["InputSubtitle"] = "Írj be SQL-t, vagy dobj ide egy .sql fájlt",
        ["OpenSql"] = "SQL-fájl megnyitása",
        ["Optimize"] = "Lekérdezés optimalizálása  →",
        ["OptimizeTooltip"] = "Az elérhető SQL-átalakítási szabályok alkalmazása",
        ["InputPlaceholder"] = "SELECT customer_id, order_date\nFROM orders\nWHERE order_status = 'open';",
        ["ResultTitle"] = "Optimalizált eredmény",
        ["ResultSubtitle"] = "Az átalakítás és az elemzés áttekintése",
        ["PreviewChanges"] = "Módosítások előnézete",
        ["ShowOptimized"] = "Optimalizált SQL megjelenítése",
        ["PreviewTooltip"] = "A bemeneti lekérdezéstől való soronkénti eltérések megjelenítése",
        ["CopySql"] = "Optimalizált SQL másolása",
        ["SaveSql"] = "Optimalizált SQL mentése",
        ["Unsaved"] = "Nincs mentve",
        ["OutputPlaceholder"] = "Az optimalizált lekérdezés itt jelenik meg.",
        ["PerformanceNotes"] = "Teljesítményre vonatkozó megjegyzések",
        ["CopyNotes"] = "Megjegyzések másolása",
        ["CopyNotesTooltip"] = "Az összes teljesítményre vonatkozó megjegyzés másolása",
        ["SplitterTooltip"] = "Húzd az SQL-panelek átméretezéséhez",
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
        ["CouldNotSave"] = "Nem sikerült menteni: {0}"
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
        if (language != "hu")
            return $"{rewrite.Title}{System.Environment.NewLine}{rewrite.WhyItMatters}";

        var column = rewrite.AfterSnippet;
        var (title, reason) = rewrite.RuleId switch
        {
            "TAUTOLOGY_LEADING_TRUE" => ("A felesleges '1 = 1 AND' eltávolítva", "Eltávolítja a szükségtelen logikai azonosságot a WHERE részből."),
            "TAUTOLOGY_WHERE_TRUE" => ("Az önálló 'WHERE 1 = 1' eltávolítva", "Eltávolítja azt a szűrési feltételt, amely minden sorra igaz."),
            "TAUTOLOGY_AND_TRUE" => ("A mindig igaz 'AND 1 = 1' eltávolítva", "Egyszerűsíti a feltétel kiértékelését a felesleges logikai kapcsolat eltávolításával."),
            "TAUTOLOGY_OR_FALSE" => ("A mindig hamis 'OR 1 = 0' eltávolítva", "Egyszerűsíti a feltétel kiértékelését a hatástalan logikai ág eltávolításával."),
            "SARG_IDENTITY_MATH" => ($"Az azonosságművelet eltávolítva erről: '{column}'", $"A(z) '{rewrite.BeforeSnippet}' aritmetikai kifejezés letilthatja a B-fa index használatát ezen az oszlopon: '{column}'."),
            "SARG_UNWRAP_TRIM" => ($"A TRIM() eltávolítva erről: '{column}'", $"A TRIM() megakadályozhatja az index és az oszlophisztogram használatát ezen az oszlopon: '{column}'."),
            "SARG_UNWRAP_TRUNC_RANGE" => ($"A felesleges TRUNC() eltávolítva erről: '{column}'", $"A közvetlen összehasonlítás lehetővé teszi az index tartománykeresését ezen az oszlopon: '{column}'."),
            "SARG_TRUNC_EQUALITY_TO_RANGE" => ("A TRUNC() egyenlőség félig nyitott dátumtartománnyá alakítva", $"A függvénnyel burkolt dátum-egyenlőség indexbarát tartománykereséssé alakul ezen az oszlopon: '{column}'."),
            "REDUNDANT_DISTINCT" => ("A felesleges DISTINCT kulcsszó eltávolítva", "A kiválasztott kulcs már egyedivé teszi a sorokat, ezért a DISTINCT szükségtelen rendezést okozna."),
            "SARG_CASE_IN_LIST" => ("A CASE kifejezés IN feltétellé egyszerűsítve", "A függvényburkolat eltávolítása segíti a bitmap- vagy B-fa indexek és az oszlophisztogram használatát."),
            "SARG_CASE_EQUALITY" => ("A CASE kifejezés közvetlen összehasonlítássá egyszerűsítve", "A függvény eltávolítása visszaállítja az oszlop közvetlen indexelérését."),
            "SUBQUERY_IN_TO_EXISTS" => ("Az IN al-lekérdezés korrelált EXISTS feltétellé alakítva", "Az optimalizáló félig összekapcsolást vagy összetett elsődleges kulcsú keresést használhat, és az első találatnál megállhat."),
            _ => (rewrite.Title, rewrite.WhyItMatters)
        };

        return $"{title}{System.Environment.NewLine}{reason}";
    }
}
