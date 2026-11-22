using System;
using System.IO;

namespace QueryOptimizer.Presentation;

public static class LanguagePreferenceStore
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QueryOptimizer",
        "language.txt");

    public static string LoadLanguage()
    {
        try
        {
            var language = File.ReadAllText(PreferencePath).Trim();
            return string.Equals(language, "hu", StringComparison.OrdinalIgnoreCase) ? "hu" : "en";
        }
        catch (IOException)
        {
            return "en";
        }
        catch (UnauthorizedAccessException)
        {
            return "en";
        }
    }

    public static void SaveLanguage(string language)
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencePath);
            if (string.IsNullOrEmpty(directory)) return;

            Directory.CreateDirectory(directory);
            File.WriteAllText(PreferencePath, language);
        }
        catch (IOException)
        {
            // Keep language switching usable when the preference cannot be written.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep language switching usable when the preference cannot be written.
        }
    }
}
