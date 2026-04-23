using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class MeetingRepository
{
    private static HttpClient Http => ApiHttp.Instance;

    public async Task<IReadOnlyList<MeetingRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await Http.GetFromJsonAsync<List<MeetingRecord>>("api/meetings", cancellationToken)
            .ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<int> AddAsync(string? title, DateTime meetingDate, TimeSpan meetingTime,
        CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync("api/meetings", new { title, meetingDate, meetingTime }, cancellationToken)
            .ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        var node = await res.Content.ReadFromJsonAsync<JsonNode>(cancellationToken).ConfigureAwait(false);
        return node?["meetingId"]?.GetValue<int>() ?? throw new InvalidOperationException("Respuesta inválida.");
    }

    public async Task UpdateAsync(int meetingId, string? title, DateTime meetingDate, TimeSpan meetingTime,
        CancellationToken cancellationToken = default)
    {
        var res = await Http.PutAsJsonAsync($"api/meetings/{meetingId}", new { title, meetingDate, meetingTime },
            cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(int meetingId, CancellationToken cancellationToken = default)
    {
        var res = await Http.DeleteAsync($"api/meetings/{meetingId}", cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}
