namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;

public static class StringExtensions
{
    public static string RemovePostfixes(this string value, params string[] postfixes)
    {
        string result = value;
        foreach (var postfix in postfixes)
        {
            if (postfix.Equals(string.Empty)) continue;
            while (result.Length >= postfix.Length && result[^postfix.Length..] == postfix)
            {
                result = result[0..^postfix.Length];
            }
        }

        return result;
    }

    public static string RemovePrefixes(this string value, params string[] prefixes)
    {
        string result = value;
        foreach (var prefix in prefixes)
        {
            if (prefix.Equals(string.Empty)) continue;
            while (result.Length >= prefix.Length && result[0..prefix.Length] == prefix)
            {
                result = result[prefix.Length..];
            }
        }

        return result;
    }
}