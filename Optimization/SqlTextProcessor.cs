using System;
using System.Text;

namespace QueryOptimizer.Optimization;

internal sealed class SqlTextProcessor
{
    public string MaskCommentsAndIdentifiers(string sql, bool preserveStringLiterals = false)
    {
        var result = new StringBuilder(sql);
        var index = 0;

        while (index < sql.Length)
        {
            if (sql[index] == '\'')
            {
                var stringEnd = GetProtectedRegionEnd(sql, index);
                if (!preserveStringLiterals) MaskRegion(result, sql, index, stringEnd);
                index = stringEnd;
                continue;
            }

            var commentStart = (sql[index] == '-' && index + 1 < sql.Length && sql[index + 1] == '-') ||
                               (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*');
            var identifierQuote = sql[index] is '"' or '`' or '[';

            if (!commentStart && !identifierQuote)
            {
                index++;
                continue;
            }

            var end = GetProtectedRegionEnd(sql, index);
            MaskRegion(result, sql, index, end);
            index = end;
        }

        return result.ToString();
    }

    public string RewriteCode(string sql, Func<string, string> rewrite) => Rewrite(sql, rewrite, preserveLiteralsAndHints: false);

    public string RewriteCodePreservingLiteralsAndHints(string sql, Func<string, string> rewrite) =>
        Rewrite(sql, rewrite, preserveLiteralsAndHints: true);

    private static string Rewrite(string sql, Func<string, string> rewrite, bool preserveLiteralsAndHints)
    {
        var result = new StringBuilder(sql.Length);
        var codeStart = 0;
        var index = 0;

        while (index < sql.Length)
        {
            if (preserveLiteralsAndHints)
            {
                var isStringLiteral = sql[index] == '\'';
                var isOptimizerHint = sql[index] == '/' && index + 2 < sql.Length &&
                                      sql[index + 1] == '*' && sql[index + 2] == '+';
                if (isStringLiteral || isOptimizerHint)
                {
                    index = GetProtectedRegionEnd(sql, index);
                    continue;
                }
            }

            var protectedEnd = GetProtectedRegionEnd(sql, index);
            if (protectedEnd == index)
            {
                index++;
                continue;
            }

            result.Append(rewrite(sql[codeStart..index]));
            result.Append(sql[index..protectedEnd]);
            index = protectedEnd;
            codeStart = index;
        }

        result.Append(rewrite(sql[codeStart..]));
        return result.ToString();
    }

    private static void MaskRegion(StringBuilder result, string source, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (source[index] is not '\r' and not '\n') result[index] = ' ';
        }
    }

    private static int GetProtectedRegionEnd(string sql, int start)
    {
        var current = sql[start];
        if (current == '-' && start + 1 < sql.Length && sql[start + 1] == '-')
        {
            var end = sql.IndexOfAny(new[] { '\r', '\n' }, start + 2);
            return end < 0 ? sql.Length : end;
        }

        if (current == '/' && start + 1 < sql.Length && sql[start + 1] == '*')
        {
            var end = sql.IndexOf("*/", start + 2, StringComparison.Ordinal);
            return end < 0 ? sql.Length : end + 2;
        }

        var closingQuote = current switch { '\'' => '\'', '"' => '"', '`' => '`', '[' => ']', _ => '\0' };
        if (closingQuote == '\0') return start;

        for (var index = start + 1; index < sql.Length; index++)
        {
            if (sql[index] != closingQuote) continue;
            if (index + 1 < sql.Length && sql[index + 1] == closingQuote)
            {
                index++;
                continue;
            }
            return index + 1;
        }

        return sql.Length;
    }
}
