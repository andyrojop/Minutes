using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Project_Minutes.Helpers;

namespace Project_Minutes.Dialogs;

public partial class CaptureSignatureDialog : Window
{
    public byte[]? SignaturePng { get; private set; }

    public CaptureSignatureDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Ink.Strokes.Clear();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var png = SignatureExporter.ToPngBytes(Ink);
        if (png.Length == 0)
        {
            MessageBox.Show(this, "Dibuja tu firma en el área blanca.", Title, MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SignaturePng = png;
        DialogResult = true;
    }

    private void Ink_StylusDown(object sender, StylusDownEventArgs e)
    {
        DeviceText.Text = $"Dispositivo: lápiz digital ({e.StylusDevice.Name})";
    }

    private void Ink_MouseDown(object sender, MouseButtonEventArgs e)
    {
        DeviceText.Text = e.StylusDevice is { } s
            ? $"Dispositivo: lápiz digital ({s.Name})"
            : "Dispositivo: ratón / trackpad";
    }
}
