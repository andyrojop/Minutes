using System.Net.Http.Json;
using System.Text.Json;

namespace Project_Minutes.Services;

public sealed class SignatureRepository
{
    private static HttpClient Http => ApiHttp.Instance;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task UpsertMinuteUserAsync(int minuteId, int userId, byte[] signaturePng,
        CancellationToken cancellationToken = default)
    {
        var res = await Http.PutAsJsonAsync($"api/minutes/{minuteId}/signatures/{userId}", new { png = signaturePng },
            JsonOpts, cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyDictionary<int, byte[]>> GetAllPngByUserForMinuteAsync(int minuteId,
        CancellationToken cancellationToken = default)
    {
        var json = await Http.GetStringAsync($"api/minutes/{minuteId}/signatures", cancellationToken)
            .ConfigureAwait(false);
        var map = JsonSerializer.Deserialize<Dictionary<int, byte[]>>(json, JsonOpts);
        return map ?? new Dictionary<int, byte[]>();
    }

    public async Task DeleteMinuteUserAsync(int minuteId, int userId, CancellationToken cancellationToken = default)
    {
        var res = await Http.DeleteAsync($"api/minutes/{minuteId}/signatures/{userId}", cancellationToken)
            .ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public Task DeleteAllForMinuteAsync(int minuteId, CancellationToken cancellationToken = default)
    {
        // No hay endpoint dedicado; la API elimina firmas al borrar la minuta. No-op en cliente remoto.
        return Task.CompletedTask;
    }
}
