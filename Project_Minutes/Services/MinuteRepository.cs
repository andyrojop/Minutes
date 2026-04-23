using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class MinuteRepository
{
    private static HttpClient Http => ApiHttp.Instance;

    public async Task<IReadOnlyList<MinuteRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await Http.GetFromJsonAsync<List<MinuteRecord>>("api/minutes", cancellationToken).ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<int> AddAsync(int meetingId, string content, CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync("api/minutes", new { meetingId, content }, cancellationToken)
            .ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        var node = await res.Content.ReadFromJsonAsync<JsonNode>(cancellationToken).ConfigureAwait(false);
        return node?["minuteId"]?.GetValue<int>() ?? throw new InvalidOperationException("Respuesta inválida.");
    }

    public async Task UpdateAsync(int minuteId, string content, CancellationToken cancellationToken = default)
    {
        var res = await Http.PutAsJsonAsync($"api/minutes/{minuteId}", new { content }, cancellationToken)
            .ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<MinuteListItem>> GetListItemsAsync(int? filterMeetingId = null,
        CancellationToken cancellationToken = default)
    {
        var url = filterMeetingId is { } id
            ? $"api/minutes/list?meetingId={id}"
            : "api/minutes/list";
        var list = await Http.GetFromJsonAsync<List<MinuteListItem>>(url, cancellationToken).ConfigureAwait(false);
        return list ?? [];
    }

    public async Task DeleteAsync(int minuteId, CancellationToken cancellationToken = default)
    {
        var res = await Http.DeleteAsync($"api/minutes/{minuteId}", cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}
