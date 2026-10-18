namespace ComicViewer.Logic;

public static class TitleParser
{
    private const string Opens = "[(［（【";
    private const string Closes = "])］）】";

    public static (string Name, string Author) Parse(string name)
    {
        if (Bracket(name, 0) is not (var author, var end)) return (name, "");
        // (分類) [作者] タイトル の慣習では先頭の丸括弧は分類・イベント名なので、直後の角括弧を作者にする
        if (name[0] is '(' or '（')
        {
            var next = end;
            while (next < name.Length && char.IsWhiteSpace(name[next])) next++;
            if (next < name.Length && name[next] is '[' or '［' or '【' && Bracket(name, next) is (var square, var squareEnd))
            {
                (author, end) = (square, squareEnd);
            }
        }
        var rest = name[end..].Trim();
        return rest.Length == 0 ? (name, "") : (rest, author.Trim());
    }

    private static (string Inner, int End)? Bracket(string name, int start)
    {
        var kind = start < name.Length ? Opens.IndexOf(name[start]) : -1;
        if (kind < 0) return null;
        var close = name.IndexOf(Closes[kind], start + 1);
        return close < 0 ? null : (name[(start + 1)..close], close + 1);
    }
}
