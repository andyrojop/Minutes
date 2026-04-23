using System.Net;
using System.Net.Http.Json;

namespace Project_Minutes.Services;

public sealed class TaskSignatureRepository
{
    private static HttpClient Http => ApiHttp.Instance;

    public async Task UpsertAsync(int taskId, int userId, byte[] signaturePng,
        CancellationToken cancellationToken = default)
    {
        var res = await Http.PutAsJsonAsync($"api/tasks/{taskId}/signature", new { userId, png = signaturePng },
            cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public async Task<byte[]?> GetPngAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var res = await Http.GetAsync($"api/tasks/{taskId}/signature", cancellationToken).ConfigureAwait(false);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteForTaskAsync(int taskId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
