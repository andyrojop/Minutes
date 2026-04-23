using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

/// <summary>Acceso a usuarios y autenticación vía API REST.</summary>
public sealed class UserRepository
{
    private static HttpClient Http => ApiHttp.Instance;

    public async Task<IReadOnlyList<UserRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await Http.GetFromJsonAsync<List<UserRecord>>("api/users", cancellationToken).ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<int> AddAsync(string name, string? email, CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync("api/users", new { name, email }, cancellationToken).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        var node = await res.Content.ReadFromJsonAsync<JsonNode>(cancellationToken).ConfigureAwait(false);
        return node?["userId"]?.GetValue<int>() ?? throw new InvalidOperationException("Respuesta inválida del servidor.");
    }

    public async Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default)
    {
        var n = await Http.GetFromJsonAsync<int>("api/auth/admin-count", cancellationToken).ConfigureAwait(false);
        return n;
    }

    public async Task<AdminSessionUser> RegisterAdministratorAsync(string displayName, string? email, string username,
        string password, CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync("api/auth/register",
            new { displayName, email, username, password }, cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(err);
        }

        var user = await res.Content.ReadFromJsonAsync<AdminSessionUser>(cancellationToken).ConfigureAwait(false);
        return user ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }

    public async Task<AdminSessionUser> RegisterFirstAdministratorAsync(string displayName, string? email,
        string username, string password, CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync("api/auth/register-first",
            new { displayName, email, username, password }, cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(err);
        }

        var user = await res.Content.ReadFromJsonAsync<AdminSessionUser>(cancellationToken).ConfigureAwait(false);
        return user ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }

    public async Task<AdminSessionUser?> LoginAdministratorAsync(string username, string password,
        CancellationToken cancellationToken = default)
    {
        var res = await Http.PostAsJsonAsync("api/auth/login", new { username, password }, cancellationToken)
            .ConfigureAwait(false);
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<AdminSessionUser>(cancellationToken).ConfigureAwait(false);
    }
}
