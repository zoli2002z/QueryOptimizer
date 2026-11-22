namespace QueryOptimizer.Presentation;

internal static class RewriteTextLocalization
{
    public static string ReviewLabel(string language, bool requiresReview, bool isAccepted) => language == "hu"
        ? requiresReview
            ? isAccepted ? "Ellen\u0151rz\u00E9s ut\u00E1n elfogadva" : "Javaslat \u00B7 alkalmaz\u00E1s el\u0151tt ellen\u0151rizd"
            : isAccepted ? "Automatikusan alkalmazva" : "Visszavonva"
        : requiresReview
            ? isAccepted ? "Accepted after review" : "Suggestion \u00B7 review before applying"
            : isAccepted ? "Applied automatically" : "Reverted";

    public static string ActionLabel(string language, bool isAccepted) => language == "hu"
        ? isAccepted ? "Visszavon\u00E1s" : "Elfogad\u00E1s"
        : isAccepted ? "Revert" : "Accept";

    public static string FormatLocation(string language, int line, int column) => language == "hu"
        ? $"{line}. sor \u00B7 {column}. oszlop"
        : $"Line {line} \u00B7 column {column}";

    public static string UnknownLocation(string language) => language == "hu"
        ? "A hely nem \u00E1ll rendelkez\u00E9sre"
        : "Location unavailable";

    public static string RuleReferenceLabel(string language) => language == "hu"
        ? "Szab\u00E1lyazonos\u00EDt\u00F3"
        : "Rule ID";

    public static string FormatSummary(string language, int total, int accepted, int reverted) => language == "hu"
        ? $"{total} m\u00F3dos\u00EDt\u00E1s \u00B7 {accepted} elfogadva \u00B7 {reverted} visszavonva"
        : $"{total} changes \u00B7 {accepted} accepted \u00B7 {reverted} reverted";
}
