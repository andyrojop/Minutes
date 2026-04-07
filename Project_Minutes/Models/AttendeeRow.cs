using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Project_Minutes.Models;

/// <summary>Fila de asistente con estado de firma para la UI de minuta.</summary>
public sealed class AttendeeRow : INotifyPropertyChanged
{
    private byte[]? _pendingPng;
    private bool _pendingRemove;
    private ImageSource? _preview;

    public int UserId { get; init; }
    public string Name { get; init; } = "";

    public bool HasDbSignature { get; set; }

    public byte[]? DbSignatureBytes { get; set; }

    public byte[]? PendingPng
    {
        get => _pendingPng;
        set
        {
            if (ReferenceEquals(_pendingPng, value))
                return;
            _pendingPng = value;
            if (value is { Length: > 0 })
                _pendingRemove = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            RebuildPreview();
        }
    }

    public bool PendingRemove
    {
        get => _pendingRemove;
        set
        {
            if (_pendingRemove == value)
                return;
            _pendingRemove = value;
            if (value)
                _pendingPng = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            RebuildPreview();
        }
    }

    public string StatusText
    {
        get
        {
            if (PendingRemove && HasDbSignature)
                return "Firma se eliminará al guardar";
            if (PendingPng is { Length: > 0 })
                return "Nueva firma lista para guardar";
            if (HasDbSignature)
                return "Firmado";
            return "Pendiente de firma";
        }
    }

    public ImageSource? Preview
    {
        get => _preview;
        private set
        {
            if (ReferenceEquals(_preview, value))
                return;
            _preview = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RebuildPreview()
    {
        if (PendingRemove)
        {
            Preview = null;
            return;
        }

        var bytes = PendingPng is { Length: > 0 } ? PendingPng : DbSignatureBytes;
        if (bytes is not { Length: > 0 })
        {
            Preview = null;
            return;
        }

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new MemoryStream(bytes);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        Preview = bmp;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void ApplyDbSignature(byte[]? png)
    {
        DbSignatureBytes = png;
        HasDbSignature = png is { Length: > 0 };
        OnPropertyChanged(nameof(StatusText));
        RebuildPreview();
    }

    public void RefreshStatusAndPreview()
    {
        OnPropertyChanged(nameof(StatusText));
        RebuildPreview();
    }
}
