using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Project_Minutes.Models;

namespace Project_Minutes.Dialogs;

public partial class TaskSignDialog : Window
{
    private byte[]? _png;

    public int TaskId { get; }
    public int ResponsibleUserId { get; }
    public byte[]? SignaturePng => _png;

    public TaskSignDialog(TaskRecord task)
    {
        InitializeComponent();

        if (task.ResponsibleUserId is null)
            throw new InvalidOperationException("El compromiso no tiene responsable asignado.");

        TaskId = task.TaskId;
        ResponsibleUserId = task.ResponsibleUserId.Value;

        TaskTitleBlock.Text = task.Title;
        ResponsibleBlock.Text = string.IsNullOrWhiteSpace(task.ResponsibleName)
            ? $"Usuario #{ResponsibleUserId}"
            : task.ResponsibleName!;

        if (task.HasResponsibleSignature)
            Title = "Actualizar firma del compromiso";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (AcceptCheck.IsChecked != true)
        {
            MessageBox.Show(this, "Marca la casilla de confirmación antes de capturar la firma.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new CaptureSignatureDialog { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SignaturePng is not { Length: > 0 } png)
            return;

        _png = png;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new MemoryStream(png);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        Preview.Source = bmp;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (AcceptCheck.IsChecked != true)
        {
            MessageBox.Show(this, "Debes confirmar la responsabilidad antes de guardar.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_png is not { Length: > 0 })
        {
            MessageBox.Show(this, "Captura la firma del responsable.", Title, MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
