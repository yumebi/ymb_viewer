using System.Text.RegularExpressions;

namespace Hamana.Viewer.Services;

// "page2.jpg" < "page10.jpg" のような直感的な並びにするための比較器。
public sealed partial class NaturalStringComparer : IComparer<string?>
{
    [GeneratedRegex(@"\d+|\D+")]
    private static partial Regex TokenRegex();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null) return string.CompareOrdinal(x, y);

        var xTokens = TokenRegex().Matches(x);
        var yTokens = TokenRegex().Matches(y);
        int count = Math.Min(xTokens.Count, yTokens.Count);

        for (int i = 0; i < count; i++)
        {
            var xt = xTokens[i].Value;
            var yt = yTokens[i].Value;

            bool xIsDigit = char.IsDigit(xt[0]);
            bool yIsDigit = char.IsDigit(yt[0]);

            int cmp;
            if (xIsDigit && yIsDigit)
            {
                cmp = xt.TrimStart('0').Length.CompareTo(yt.TrimStart('0').Length);
                if (cmp == 0) cmp = string.CompareOrdinal(xt, yt);
            }
            else
            {
                cmp = string.CompareOrdinal(xt, yt);
            }

            if (cmp != 0) return cmp;
        }

        return xTokens.Count.CompareTo(yTokens.Count);
    }
}
