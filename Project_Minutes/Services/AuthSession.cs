using Project_Minutes.Models;

namespace Project_Minutes.Services;

/// <summary>Sesión del administrador autenticado.</summary>
public static class AuthSession
{
    public static AdminSessionUser? Current { get; private set; }

    public static bool IsAdministratorLoggedIn => Current is not null;

    public static void SetUser(AdminSessionUser user) => Current = user;

    public static void Clear() => Current = null;
}
