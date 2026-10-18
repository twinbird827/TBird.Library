using System.Text;

namespace ComicViewer.Logic;

public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    // 区切り単位で比べないと "a/10.jpg" と "a 2/1.jpg" のように、フォルダ名の続きとファイル名が混ざって並ぶ。
    public static readonly IComparer<string> Path = Comparer<string>.Create(ComparePath);

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var a = x.Normalize(NormalizationForm.FormKC);
        var b = y.Normalize(NormalizationForm.FormKC);
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            var ia = i; var jb = j;
            var digitA = IsDigit(a[i]);
            var digitB = IsDigit(b[j]);
            while (i < a.Length && IsDigit(a[i]) == digitA) i++;
            while (j < b.Length && IsDigit(b[j]) == digitB) j++;
            var sa = a.AsSpan(ia, i - ia);
            var sb = b.AsSpan(jb, j - jb);

            int c;
            if (digitA && digitB)
            {
                c = CompareNumber(sa, sb);
            }
            else
            {
                c = string.Compare(sa.ToString(), sb.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            if (c != 0) return c;
        }
        var rest = (a.Length - i).CompareTo(b.Length - j);
        return rest != 0 ? rest : string.CompareOrdinal(x, y);
    }

    private static int CompareNumber(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        var ta = a.TrimStart('0');
        var tb = b.TrimStart('0');
        var c = ta.Length.CompareTo(tb.Length);
        if (c != 0) return c;
        c = ta.SequenceCompareTo(tb);
        return c != 0 ? Math.Sign(c) : a.Length.CompareTo(b.Length);
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static int ComparePath(string? x, string? y)
    {
        var a = (x ?? "").Split('/');
        var b = (y ?? "").Split('/');
        for (var k = 0; k < Math.Min(a.Length, b.Length); k++)
        {
            var c = Instance.Compare(a[k], b[k]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }
}
