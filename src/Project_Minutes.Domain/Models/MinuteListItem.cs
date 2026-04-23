namespace Project_Minutes.Models;

/// <summary>Minuta para lista principal con conteo de firmas vs asistentes.</summary>
public sealed class MinuteListItem
{
    public int MinuteId { get; init; }
    public int MeetingId { get; init; }
    public string? MeetingTitle { get; init; }
    public string Content { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public int ParticipantCount { get; init; }
    public int SignatureCount { get; init; }

    public string DisplayLine
    {
        get
        {
            var title = ExtractTitlePreview(Content);
            string part;
            if (ParticipantCount == 0)
                part = "Sin asistentes en la reunión · añade participantes";
            else if (SignatureCount >= ParticipantCount)
                part = $"✓ Todas las firmas ({SignatureCount}/{ParticipantCount})";
            else
                part = $"Firmas {SignatureCount}/{ParticipantCount} asistentes";

            return $"{title} · {part}";
        }
    }

    public static string ExtractTitlePreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "(sin texto)";
        var t = content.Trim();
        var idx = t.IndexOf("\n\n", StringComparison.Ordinal);
        if (idx > 0)
            return t[..idx].Trim().Replace('\r', ' ').Replace('\n', ' ');
        var line = t.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (line is { Length: > 80 })
            return line[..77] + "…";
        return line ?? "(sin texto)";
    }
}
