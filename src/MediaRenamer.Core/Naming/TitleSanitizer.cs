using System.Globalization;
using System.Text;

namespace MediaRenamer.Core.Naming;

/// <summary>Cleans an episode title so it can be used in a file name on all platforms.</summary>
public static class TitleSanitizer
{
    /// <summary>Replacement for every character that is not allowed.</summary>
    public const char Replacement = '_';

    /// <summary>Punctuation that is kept, in addition to letters, digits and spaces.</summary>
    private const string AllowedPunctuation = "-.,'&()…#%!";

    /// <summary>Quotation marks that are not in the Unicode categories "initial/final quote punctuation".</summary>
    private const string OtherQuotationMarks =
        "\"'" +
        "‚„" + // ‚ „ (low quotation marks)
        "`´" + // ` ´ (grave and acute accent, used as apostrophe)
        "′″" + // ′ ″ (prime, double prime)
        "＂＇" + // ＂ ＇ (fullwidth)
        "〝〞〟" + // 〝 〞 〟
        "❛❜❝❞"; // ❛ ❜ ❝ ❞

    /// <summary>
    /// Cleans <paramref name="title"/>, in this order:
    /// <list type="number">
    /// <item>Invisible format characters (byte order mark U+FEFF, zero width space, direction marks, soft hyphen, ...)
    /// are removed, except the zero width (non-)joiner, which is part of the spelling in some scripts. All kinds of
    /// quotation marks become <c>'</c>.</item>
    /// <item><c>...</c> becomes <c>…</c>, all kinds of dashes (en dash <c>–</c>, em dash <c>—</c>, ...) become
    /// <c>-</c>.</item>
    /// <item><c>:</c> and <c>?</c> are removed.</item>
    /// <item>Letters (including umlauts, accents and <c>ß</c>), digits and the punctuation <c>- . , ' &amp; ( ) … # % !</c>
    /// are kept, every other character becomes <c>_</c>, including the characters forbidden on Windows.</item>
    /// <item>Every whitespace character (tab, no-break space, ...) becomes a space, consecutive spaces become one, and
    /// spaces at the start and end are removed.</item>
    /// </list>
    /// </summary>
    /// <example><c>AC/DC</c> → <c>AC_DC</c>, <c>Say "Hi"</c> → <c>Say 'Hi'</c>, <c>Stop!</c> → <c>Stop!</c>,
    /// <c>Part 1: The Beginning</c> → <c>Part 1 The Beginning</c>, <c>What now?</c> → <c>What now</c>.</example>
    public static string Sanitize(string title)
    {
        // composed form, so a letter with an accent is one character
        var text = title.Normalize(NormalizationForm.FormC);

        var replaced = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsRemovedFormatCharacter(rune))
            {
                // e.g. TMDB returns "O \uFEFF X" for Squid Game S02E06
            }
            else if (IsQuotationMark(rune))
            {
                replaced.Append('\'');
            }
            else if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.DashPunctuation)
            {
                replaced.Append('-');
            }
            else
            {
                replaced.Append(rune.ToString());
            }
        }

        text = replaced.ToString()
            .Replace("...", "…", StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal);

        var result = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                // at most one space in a row, none at the start
                if (result.Length > 0 && result[^1] != ' ')
                {
                    result.Append(' ');
                }
            }
            else if (IsAllowed(rune))
            {
                result.Append(rune.ToString());
            }
            else
            {
                result.Append(Replacement);
            }
        }

        return result.ToString().TrimEnd(' ');
    }

    /// <summary>Zero width non-joiner and zero width joiner, which are kept.</summary>
    private const string KeptFormatCharacters = "\u200C\u200D";

    private static bool IsRemovedFormatCharacter(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format
        && !(rune.IsBmp && KeptFormatCharacters.Contains((char)rune.Value, StringComparison.Ordinal));

    private static bool IsQuotationMark(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
        || (rune.IsBmp && OtherQuotationMarks.Contains((char)rune.Value, StringComparison.Ordinal));

    private static bool IsAllowed(Rune rune) =>
        Rune.IsLetterOrDigit(rune)
        // combining marks belong to the preceding letter (e.g. in scripts without precomposed characters)
        || Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
        || (rune.IsBmp && AllowedPunctuation.Contains((char)rune.Value, StringComparison.Ordinal))
        || (rune.IsBmp && KeptFormatCharacters.Contains((char)rune.Value, StringComparison.Ordinal));
}
