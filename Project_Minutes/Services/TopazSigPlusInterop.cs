using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using Project_Minutes.Configuration;

namespace Project_Minutes.Services;

/// <summary>
/// Carga SigPlusNET.dll (SDK Topaz) por reflexión para no exigir la DLL en tiempo de compilación.
/// </summary>
public static class TopazSigPlusInterop
{
    private const string TypeName = "Topaz.SigPlusNET";

    public static string? TryFindSigPlusAssembly(SignaturePadOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SigPlusAssemblyPath) &&
            File.Exists(options.SigPlusAssemblyPath))
            return options.SigPlusAssemblyPath;

        var baseDir = AppContext.BaseDirectory;
        var nextToExe = Path.Combine(baseDir, "SigPlusNET.dll");
        if (File.Exists(nextToExe))
            return nextToExe;

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Rutas habituales del SDK .NET (la «Demonstration» no incluye esta DLL).
        foreach (var candidate in EnumerateKnownSigPlusPaths(pf, pfx86, local))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (var root in new[] { pf, pfx86 })
        {
            if (string.IsNullOrEmpty(root))
                continue;
            var topaz = Path.Combine(root, "Topaz Systems");
            if (!Directory.Exists(topaz))
                continue;
            try
            {
                var found = Directory.EnumerateFiles(topaz, "SigPlusNET.dll", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (found is not null)
                    return found;
            }
            catch (UnauthorizedAccessException)
            {
                // ignorar carpetas sin permiso
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateKnownSigPlusPaths(string programFiles, string programFilesX86,
        string localAppData)
    {
        foreach (var baseRoot in new[] { programFiles, programFilesX86 })
        {
            if (string.IsNullOrEmpty(baseRoot))
                continue;

            yield return Path.Combine(baseRoot, "Topaz Systems", "SigPlusNET SDK", "SigPlusNET.dll");
            yield return Path.Combine(baseRoot, "Topaz Systems", "SigPlusNET", "SigPlusNET.dll");
            yield return Path.Combine(baseRoot, "Topaz Systems", "SigPlusNET Assembly SDK", "SigPlusNET.dll");
            yield return Path.Combine(baseRoot, "Topaz Systems", "SigPlusNET SDK", "bin", "SigPlusNET.dll");
        }

        if (!string.IsNullOrEmpty(localAppData))
        {
            var t = Path.Combine(localAppData, "Topaz Systems");
            if (Directory.Exists(t))
            {
                foreach (var f in SafeEnumerate(t, "SigPlusNET.dll"))
                    yield return f;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerate(string root, string fileName)
    {
        try
        {
            return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static int ParseComPortNumber(string comPort)
    {
        if (string.IsNullOrWhiteSpace(comPort))
            throw new ArgumentException("ComPort vacío.", nameof(comPort));

        var s = comPort.Trim();
        if (int.TryParse(s, out var direct))
            return direct;

        if (s.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            var n = s[3..].Trim();
            if (int.TryParse(n, out var num))
                return num;
        }

        throw new FormatException($"No se pudo interpretar el puerto COM: «{comPort}».");
    }

    /// <summary>Inicializa tipo de tableta y, si aplica, puerto serie y baudios (tableta apagada).</summary>
    public static void ApplyTabletSettings(object sigPlus, SignaturePadOptions options)
    {
        Invoke(sigPlus, "SetTabletType", options.TabletType);

        // 0 = datos por puerto COM. 2 = USB Topaz. 6 = HSB (HID). Los modos USB/HSB no deben configurarse con COM/baud.
        if (options.TabletType == 0)
        {
            Invoke(sigPlus, "SetTabletComPort", ParseComPortNumber(options.ComPort));
            Invoke(sigPlus, "SetTabletBaudRate", options.BaudRate);
        }
    }

    public static void SetTabletState(object sigPlus, int state) =>
        Invoke(sigPlus, "SetTabletState", state);

    public static void ClearTablet(object sigPlus) =>
        InvokeParameterless(sigPlus, "ClearTablet");

    public static int GetTabletPointCount(object sigPlus)
    {
        var t = sigPlus.GetType();
        var prop = t.GetProperty("NumberOfTabletPoints", BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null)
        {
            var v = prop.GetValue(sigPlus);
            if (v is int i)
                return i;
        }

        var m = t.GetMethod("NumberOfTabletPoints", BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder,
            Type.EmptyTypes, null);
        if (m is not null)
        {
            var v = m.Invoke(sigPlus, null);
            if (v is int i)
                return i;
        }

        return 0;
    }

    public static byte[] GetSignaturePng(object sigPlus, int imageWidth = 500, int imageHeight = 150, int penWidth = 4)
    {
        Invoke(sigPlus, "SetImageXSize", imageWidth);
        Invoke(sigPlus, "SetImageYSize", imageHeight);
        Invoke(sigPlus, "SetImagePenWidth", penWidth);

        var img = InvokeParameterless(sigPlus, "GetSigImage");
        if (img is not Image image)
            throw new InvalidOperationException("GetSigImage no devolvió una imagen válida.");

        try
        {
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        finally
        {
            image.Dispose();
        }
    }

    public static object CreateSigPlusControl(Assembly assembly)
    {
        var t = assembly.GetType(TypeName)
            ?? throw new InvalidOperationException($"No se encontró {TypeName} en SigPlusNET.dll.");

        var inst = Activator.CreateInstance(t)
            ?? throw new InvalidOperationException("No se pudo crear SigPlusNET.");

        return inst;
    }

    public static Assembly LoadSigPlusAssembly(string path) =>
        Assembly.LoadFrom(path);

    private static object? Invoke(object target, string name, params object[] args)
    {
        var t = target.GetType();
        var argTypes = args.Select(a => a.GetType()).ToArray();
        var method = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, argTypes,
            null);
        if (method is null)
            throw new MissingMethodException(t.FullName, name);

        return method.Invoke(target, args);
    }

    private static object? InvokeParameterless(object target, string name)
    {
        var t = target.GetType();
        var method = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes,
            null);
        if (method is null)
            throw new MissingMethodException(t.FullName, name);

        return method.Invoke(target, null);
    }
}
