using System;
using System.IO;

namespace QueryOptimizer.Presentation;

public static class ThemePreferenceStore
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QueryOptimizer",
        "theme.txt");

    public static bool LoadDarkMode()
    {
        try
        {
            return string.Equals(File.ReadAllText(PreferencePath).Trim(), "dark", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void SaveDarkMode(bool isDark)
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencePath);
            if (string.IsNullOrEmpty(directory)) return;

            Directory.CreateDirectory(directory);
            File.WriteAllText(PreferencePath, isDark ? "dark" : "light");
        }
        catch (IOException)
        {
            // Keep the theme switch usable if the preference cannot be written.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the theme switch usable if the preference cannot be written.
        }
    }
}
