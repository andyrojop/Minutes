using System.Net.Http.Json;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class ParticipantRepository
{
    private static HttpClient Http => ApiHttp.Instance;

    public async Task<IReadOnlyList<ParticipantRecord>> GetByMeetingAsync(int meetingId,
        CancellationToken cancellationToken = default)
    {
        var list =
            await Http.GetFromJsonAsync<List<ParticipantRecord>>($"api/meetings/{meetingId}/participants",
                cancellationToken).ConfigureAwait(false);
        return list ?? [];
    }

    public async Task AddIfNotExistsAsync(int meetingId, int userId, string? position = null,
        CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync($"api/meetings/{meetingId}/participants", new { userId, position },
            cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public async Task RemoveAsync(int meetingId, int userId, CancellationToken cancellationToken = default)
    {
        var res = await Http.DeleteAsync($"api/meetings/{meetingId}/participants/{userId}", cancellationToken)
            .ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}
