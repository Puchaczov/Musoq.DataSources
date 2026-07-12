using System.Globalization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class CurrentCultureEntity(CultureInfo culture, CultureInfo uiCulture)
{
    public string CurrentCulture => culture.Name;
    public string CurrentUICulture => uiCulture.Name;
    public string DecimalSeparator => culture.NumberFormat.NumberDecimalSeparator;
    public string NumberGroupSeparator => culture.NumberFormat.NumberGroupSeparator;
    public string ShortDatePattern => culture.DateTimeFormat.ShortDatePattern;
    public string LongDatePattern => culture.DateTimeFormat.LongDatePattern;
    public string ShortTimePattern => culture.DateTimeFormat.ShortTimePattern;
    public string LongTimePattern => culture.DateTimeFormat.LongTimePattern;
}
