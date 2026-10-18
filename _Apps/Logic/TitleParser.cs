namespace ComicViewer.Logic;

public static class TitleParser
{
    private const string Opens = "[(［（【";
    private const string Closes = "])］）】";

    public static (string Name, string Author) Parse(string name)
    {
        if (EntryRules.IsZip(name) || EntryRules.IsPdf(name)) name = name[..^Path.GetExtension(name).Length];
        var authors = new List<string>();
        var end = 0;
        while (Bracket(name, end) is (var inner, var close))
        {
            authors.Add(inner.Trim());
            end = close;
            while (end < name.Length && char.IsWhiteSpace(name[end])) end++;
        }
        var rest = name[end..].Trim();
        return authors.Count == 0 || rest.Length == 0 ? (name, "") : (rest, string.Join("×", authors));
    }

    private static (string Inner, int End)? Bracket(string name, int start)
    {
        var kind = start < name.Length ? Opens.IndexOf(name[start]) : -1;
        if (kind < 0) return null;
        var close = name.IndexOf(Closes[kind], start + 1);
        return close < 0 ? null : (name[(start + 1)..close], close + 1);
    }
}
