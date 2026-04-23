namespace Project_Minutes.Helpers;

public static class MinuteContentFormat
{
    public static (string Title, string Body) SplitTitleBody(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ("", "");

        var t = content.Trim();
        var idx = t.IndexOf("\n\n", StringComparison.Ordinal);
        if (idx <= 0)
            return ("", t);

        return (t[..idx].Trim(), t[(idx + 2)..].Trim());
    }

    public static string CombineTitleBody(string title, string body)
    {
        title = title.Trim();
        body = body.Trim();
        if (title.Length == 0)
            return body;
        if (body.Length == 0)
            return title;
        return title + "\n\n" + body;
    }
}
