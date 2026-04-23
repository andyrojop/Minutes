using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Project_Minutes.Configuration;
using Project_Minutes.Services;
using WinFormsControl = System.Windows.Forms.Control;

namespace Project_Minutes.Dialogs;

public partial class CaptureSignatureDialog : Window
{
    public byte[]? SignaturePng { get; private set; }

    private readonly SignaturePadOptions _padOptions = ClientConfiguration.Load().SignaturePad;
    private object? _sigPlus;
    private bool _topazListening;
    private byte[]? _topazPng;

    public CaptureSignatureDialog()
    {
        InitializeComponent();
        Loaded += CaptureSignatureDialog_Loaded;
        Closed += (_, _) => TeardownTopaz();
    }

    private void CaptureSignatureDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_padOptions.UseTopaz)
        {
            TopazPanel.Visibility = Visibility.Visible;
            TopazHintText.Text =
                "La firma solo se permite con el pad Topaz. Ponga «SignaturePad:UseTopaz» en true en appsettings.json.";
            TopazActivateButton.IsEnabled = false;
            TopazImportButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            DeviceText.Text = "Firma solo con pad — Topaz desactivado en configuración.";
            return;
        }

        var path = TopazSigPlusInterop.TryFindSigPlusAssembly(_padOptions);
        if (path is null)
        {
            TopazPanel.Visibility = Visibility.Visible;
            TopazHintText.Text =
                "No se encontró SigPlusNET.dll. Descargue el «SigPlusNET Assembly SDK» desde topazsystems.com, " +
                "o indique «SignaturePad:SigPlusAssemblyPath» en appsettings.json.";
            TopazActivateButton.IsEnabled = false;
            TopazImportButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            DeviceText.Text = "Sin SDK SigPlusNET — no se puede firmar.";
            return;
        }

        try
        {
            var asm = TopazSigPlusInterop.LoadSigPlusAssembly(path);
            _sigPlus = TopazSigPlusInterop.CreateSigPlusControl(asm);
            SigPlusHost.Child = (WinFormsControl)_sigPlus;
        }
        catch (Exception ex)
        {
            TopazPanel.Visibility = Visibility.Visible;
            TopazHintText.Text = $"No se pudo cargar SigPlusNET: {ex.Message}";
            TopazActivateButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            DeviceText.Text = "Error al cargar el control del pad.";
            return;
        }

        TopazPanel.Visibility = Visibility.Visible;
        var model = string.IsNullOrWhiteSpace(_padOptions.Model) ? "Topaz" : _padOptions.Model;
        TopazHintText.Text =
            $"{model} · tipo de tableta {_padOptions.TabletType}. " +
            "Pulse «Activar pad Topaz», firme en el dispositivo y luego «Importar firma del pad».";
        DeviceText.Text = $"Solo firma en el pad {model}.";
    }

    private void TeardownTopaz()
    {
        if (_sigPlus is null)
            return;

        try
        {
            TopazSigPlusInterop.SetTabletState(_sigPlus, 0);
        }
        catch
        {
            // ignorar al cerrar
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _topazPng = null;
        TopazPreviewBorder.Visibility = Visibility.Collapsed;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        SaveButton.IsEnabled = false;
        TopazImportButton.IsEnabled = false;
        _topazListening = false;

        if (_sigPlus is not null)
        {
            try
            {
                TopazSigPlusInterop.SetTabletState(_sigPlus, 0);
                TopazSigPlusInterop.ClearTablet(_sigPlus);
            }
            catch
            {
                // ignorar
            }
        }

        DeviceText.Text = "Firma borrada. Active el pad de nuevo si desea otra firma.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TopazActivate_Click(object sender, RoutedEventArgs e)
    {
        if (_sigPlus is null)
            return;

        try
        {
            TopazSigPlusInterop.SetTabletState(_sigPlus, 0);
            TopazSigPlusInterop.ApplyTabletSettings(_sigPlus, _padOptions);
            TopazSigPlusInterop.ClearTablet(_sigPlus);
            TopazSigPlusInterop.SetTabletState(_sigPlus, 1);
            _topazListening = true;
            TopazImportButton.IsEnabled = true;
            DeviceText.Text = "Pad activo: firme y pulse «Importar firma del pad».";
        }
        catch (Exception ex)
        {
            var detail = GetInnermostException(ex);
            MessageBox.Show(this,
                "No se pudo activar el pad.\n\n" +
                "• Cierre «Topaz SigPlus Demonstration» u otra app que use el pad.\n" +
                "• Con T-S460-HSB-R suele usarse «TabletType»: 6 en appsettings.\n\n" +
                "Detalle: " + detail.Message,
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void TopazImport_Click(object sender, RoutedEventArgs e)
    {
        if (_sigPlus is null || !_topazListening)
            return;

        try
        {
            if (TopazSigPlusInterop.GetTabletPointCount(_sigPlus) <= 0)
            {
                MessageBox.Show(this, "No se detectó trazo en el pad. Firme y vuelva a intentar.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var png = TopazSigPlusInterop.GetSignaturePng(_sigPlus);
            TopazSigPlusInterop.SetTabletState(_sigPlus, 0);
            _topazListening = false;
            TopazImportButton.IsEnabled = false;
            _topazPng = png;

            BitmapImage bmp;
            using (var ms = new MemoryStream(png))
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
            }

            bmp.Freeze();
            TopazPreviewImage.Source = bmp;
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            TopazPreviewBorder.Visibility = Visibility.Visible;
            SaveButton.IsEnabled = true;
            DeviceText.Text = "Firma lista. Pulse «Guardar firma» o «Limpiar» para repetir.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo leer la firma del pad.\n\n" + ex.Message, Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_topazPng is not { Length: > 0 })
        {
            MessageBox.Show(this,
                "Importe primero la firma desde el pad con «Importar firma del pad».",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SignaturePng = _topazPng;
        DialogResult = true;
    }

    private static Exception GetInnermostException(Exception ex)
    {
        while (ex.InnerException is { } inner)
            ex = inner;
        return ex;
    }
}
