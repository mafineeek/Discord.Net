namespace Discord.Net.Hanz;

public static class StringUtils
{
    public static string WithNewlinePadding(this string str, int padding)
        => str.Replace(Environment.NewLine, $"{Environment.NewLine}{string.Empty.PadLeft(padding)}");

    public static string Prefix(this string str, int total, char prefix = ' ')
        => $"{string.Empty.PadLeft(total, prefix)}{str}";
    
    public static string PrefixNewLine(this string str)
    {
        if (str == string.Empty) return string.Empty;

        return $"{Environment.NewLine}{str}";
    }

    public static string CenterAlign(this string str, int width)
    {
        if (str.Length >= width) return str;

        return str.Prefix((width / 2) - (str.Length / 2)).PadRight(width);

    }
}