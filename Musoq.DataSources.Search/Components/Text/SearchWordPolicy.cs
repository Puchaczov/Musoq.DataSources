#nullable enable

using System.Globalization;

namespace Musoq.DataSources.Search.Components.Text;

internal static class SearchWordPolicy
{
    public static bool IsWordScalar(char value)
    {
        return !char.IsSurrogate(value) &&
               IsWordCategory(CharUnicodeInfo.GetUnicodeCategory(value));
    }

    public static bool IsWordScalar(char highSurrogate, char lowSurrogate)
    {
        if (!char.IsSurrogatePair(highSurrogate, lowSurrogate))
            return false;

        var scalar = char.ConvertToUtf32(highSurrogate, lowSurrogate);
        return IsWordCategory(CharUnicodeInfo.GetUnicodeCategory(scalar));
    }

    private static bool IsWordCategory(UnicodeCategory category)
    {
        return category is UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark;
    }
}
