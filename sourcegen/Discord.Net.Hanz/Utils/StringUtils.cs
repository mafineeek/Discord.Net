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
    
    public static string ToParameterName(this string name)
    {
        if (name == string.Empty) return name;

        if (name.StartsWith("_"))
            name = name.Substring(1);

        int lowerCount = 0;
        for (; lowerCount != name.Length; lowerCount++)
        {
            if (char.IsLower(name[lowerCount]))
                break;
        }

        if (lowerCount == name.Length)
            return name.ToLower();

        if (lowerCount > 1)
            lowerCount--;

        var a = name.Substring(0, lowerCount);
        var b = name.Substring(lowerCount);

        return $"{a.ToLower()}{b}";
    }
}