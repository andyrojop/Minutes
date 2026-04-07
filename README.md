# Minutas y firmas (Project_Minutes)

Aplicación de escritorio en **WPF** y **C#** (.NET) para gestionar reuniones, minutas, compromisos y **firmas digitales** almacenadas en **SQL Server**.

## Requisitos

- **Windows** (la app usa WPF).
- **.NET SDK 10** (o la versión indicada en `Project_Minutes/Project_Minutes.csproj` en `TargetFramework`). Comprueba con:

  ```powershell
  dotnet --list-sdks
  ```

- **SQL Server** (local o remoto) con una base de datos y las tablas del proyecto (`Users`, `Meetings`, `Participants`, `Minutes`, `Tasks`, `Signatures`).

## Configurar la base de datos

1. Crea la base (por ejemplo `Minutes`) en SQL Server.
2. Ejecuta tus scripts `CREATE TABLE` para crear el esquema.
3. (Opcional) La aplicación **crea sola** la tabla `TaskSignatures` y el índice de firmas al conectar, si tienes permisos DDL. Si prefieres hacerlo a mano, usa **`Project_Minutes/Database/SchemaExtensions.sql`**. Si el índice único en `Signatures` falla por datos duplicados, corrige las filas y vuelve a intentarlo o déjalo sin índice.
4. Ajusta la cadena de conexión en **`Project_Minutes/appsettings.json`** (véase la siguiente sección).

La aplicación lee la clave `ConnectionStrings:MeetingMinutes` desde ese archivo (véase `Configuration/AppConfiguration.cs`).

## Configuración (`appsettings.json`)

Edita el archivo **`Project_Minutes/appsettings.json`** en la carpeta del proyecto (se copia al compilar a la carpeta de salida).

Ejemplo con **autenticación SQL** (sustituye servidor, base, usuario y contraseña):

```json
{
  "ConnectionStrings": {
    "MeetingMinutes": "Server=(local);Database=Minutes;User Id=sa;Password=TU_PASSWORD;Encrypt=Mandatory;Trust Server Certificate=True;"
  },
  "Database": {
    "CommandTimeoutSeconds": 30
  }
}
```

Ejemplo con **Windows Authentication** (sin `User Id` / `Password`):

```json
"MeetingMinutes": "Server=(local);Database=Minutes;Integrated Security=True;Trust Server Certificate=True;"
```

- Si usas una **instancia con nombre** (por ejemplo `SQLEXPRESS`): `Server=(local)\\SQLEXPRESS` (en JSON, la barra invertida doble es correcta en la cadena).
- No subas al repositorio contraseñas reales; usa secretos locales o variables de entorno si más adelante lo automatizas.

## Cómo ejecutarla

Desde una terminal, entra en la carpeta del **`.csproj`** y ejecuta:

```powershell
cd "ruta\al\proyecto\Project_Minutes\Project_Minutes"
dotnet restore
dotnet run
```

También puedes abrir esa carpeta en **Visual Studio** y pulsar **F5** (Iniciar depuración).

Al arrancar, el menú **Archivo → Probar conexión a SQL** vuelve a comprobar la base; en la barra inferior verás un resumen tipo `SQL Server · Minutes · (servidor)`.

## Estructura útil

| Ruta | Descripción |
|------|-------------|
| `Project_Minutes/appsettings.json` | Cadena de conexión y timeout |
| `Project_Minutes/Configuration/AppConfiguration.cs` | Carga de configuración |
| `Project_Minutes/MainWindow.xaml` | Ventana principal |
| `Project_Minutes/Dialogs/` | Diálogos (reunión, minuta, captura de firma, etc.) |

## Solución de problemas

- **La ventana se cierra o muestra error de conexión:** revisa el servidor, el nombre de la base, el usuario/contraseña y que SQL Server acepte conexiones remotas/TCP si no es local.
- **`appsettings.json` no encontrado al ejecutar:** asegúrate de compilar desde el proyecto que tenía el `CopyToOutputDirectory` en el `.csproj` y ejecuta el `.exe` o `dotnet run` desde la carpeta correcta.
