using Microsoft.Extensions.Configuration;

namespace Project_Minutes.Configuration;

/// <summary>Configuración del cliente WPF: API REST y tableta de firmas (sin cadena SQL).</summary>
public sealed class ClientConfiguration
{
    public required string ApiBaseUrl { get; init; }
    public SignaturePadOptions SignaturePad { get; init; } = new();

    public static ClientConfiguration Load(string? basePath = null)
    {
        basePath ??= AppDomain.CurrentDomain.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var api = config["Api:BaseUrl"]?.Trim()
                  ?? throw new InvalidOperationException("Falta Api:BaseUrl en appsettings.json (URL del backend REST).");

        var sigSection = config.GetSection("SignaturePad");
        var useTopaz = sigSection.GetValue<bool?>("UseTopaz") ?? true;
        var comPort = sigSection.GetValue<string>("ComPort") ?? "COM9";
        var tabletType = sigSection.GetValue<int?>("TabletType") ?? 0;
        var baudRate = sigSection.GetValue<int?>("BaudRate") ?? 19200;
        var modelName = sigSection.GetValue<string>("Model") ?? "";
        var sigPlusPath = sigSection.GetValue<string>("SigPlusAssemblyPath");

        return new ClientConfiguration
        {
            ApiBaseUrl = api,
            SignaturePad = new SignaturePadOptions
            {
                UseTopaz = useTopaz,
                ComPort = comPort,
                TabletType = tabletType,
                BaudRate = baudRate,
                Model = modelName,
                SigPlusAssemblyPath = string.IsNullOrWhiteSpace(sigPlusPath) ? null : sigPlusPath.Trim()
            }
        };
    }
}

public sealed class SignaturePadOptions
{
    public bool UseTopaz { get; init; } = true;
    public string ComPort { get; init; } = "COM9";
    public int TabletType { get; init; }
    public int BaudRate { get; init; } = 19200;
    public string Model { get; init; } = "";
    public string? SigPlusAssemblyPath { get; init; }
}
