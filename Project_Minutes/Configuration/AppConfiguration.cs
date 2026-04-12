using Microsoft.Extensions.Configuration;

namespace Project_Minutes.Configuration;

public sealed class AppConfiguration
{
    public required string MeetingMinutesConnectionString { get; init; }
    public int CommandTimeoutSeconds { get; init; } = 30;
    public SignaturePadOptions SignaturePad { get; init; } = new();

    public static AppConfiguration Load(string? basePath = null)
    {
        basePath ??= AppDomain.CurrentDomain.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var cs = config.GetConnectionString("MeetingMinutes")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:MeetingMinutes en appsettings.json.");

        var timeout = config.GetSection("Database").GetValue<int?>("CommandTimeoutSeconds") ?? 30;
        var sigSection = config.GetSection("SignaturePad");
        var useTopaz = sigSection.GetValue<bool?>("UseTopaz") ?? true;
        var comPort = sigSection.GetValue<string>("ComPort") ?? "COM9";
        var tabletType = sigSection.GetValue<int?>("TabletType") ?? 0;
        var baudRate = sigSection.GetValue<int?>("BaudRate") ?? 19200;
        var modelName = sigSection.GetValue<string>("Model") ?? "";
        var sigPlusPath = sigSection.GetValue<string>("SigPlusAssemblyPath");

        return new AppConfiguration
        {
            MeetingMinutesConnectionString = cs,
            CommandTimeoutSeconds = timeout,
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
    /// <summary>Si es false, solo se usa el área de tinta (ratón/lápiz).</summary>
    public bool UseTopaz { get; init; } = true;

    /// <summary>Puerto COM del pad (p. ej. COM9).</summary>
    public string ComPort { get; init; } = "COM9";

    /// <summary>
    /// 0 = puerto COM; 2 = USB (controlador Topaz); 6 = HSB / HID (p. ej. T-S460-HSB-R). Use 0 solo si el pad es serie/COM virtual y no HSB.
    /// </summary>
    public int TabletType { get; init; }

    public int BaudRate { get; init; } = 19200;

    /// <summary>Texto informativo (p. ej. T-S460-HSB-R).</summary>
    public string Model { get; init; } = "";

    /// <summary>Ruta absoluta a SigPlusNET.dll si no está junto al .exe.</summary>
    public string? SigPlusAssemblyPath { get; init; }
}
