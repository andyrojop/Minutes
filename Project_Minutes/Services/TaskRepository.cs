using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class TaskRepository
{
    private static HttpClient Http => ApiHttp.Instance;

    public async Task<IReadOnlyList<TaskRecord>> GetByMinuteIdAsync(int minuteId,
        CancellationToken cancellationToken = default)
    {
        var list = await Http.GetFromJsonAsync<List<TaskRecord>>($"api/minutes/{minuteId}/tasks", cancellationToken)
            .ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<int> AddAsync(int minuteId, string title, int? responsibleUserId, DateTime? dueDate,
        CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync("api/tasks",
            new { minuteId, title, responsibleUserId, dueDate }, cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        var node = await res.Content.ReadFromJsonAsync<JsonNode>(cancellationToken).ConfigureAwait(false);
        return node?["taskId"]?.GetValue<int>() ?? throw new InvalidOperationException("Respuesta inválida.");
    }

    public async Task DeleteAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var res = await Http.DeleteAsync($"api/tasks/{taskId}", cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}
