using System.Globalization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class CultureEntity(CultureInfo culture)
{
    public string Name => culture.Name;
    public string EnglishName => culture.EnglishName;
    public string DisplayName => culture.DisplayName;
    public string NativeName => culture.NativeName;
    public bool IsNeutralCulture => culture.IsNeutralCulture;
    public string ParentName => culture.Parent.Name;
    public int LCID => culture.LCID;
    public string CultureTypes => culture.CultureTypes.ToString();
    public string DecimalSeparator => culture.NumberFormat.NumberDecimalSeparator;
    public string NumberGroupSeparator => culture.NumberFormat.NumberGroupSeparator;
    public string ShortDatePattern => culture.DateTimeFormat.ShortDatePattern;
    public string LongDatePattern => culture.DateTimeFormat.LongDatePattern;
    public string ShortTimePattern => culture.DateTimeFormat.ShortTimePattern;
    public string LongTimePattern => culture.DateTimeFormat.LongTimePattern;
}
