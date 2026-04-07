using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Project_Minutes.Helpers;

public static class SignatureExporter
{
    public static byte[] ToPngBytes(InkCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (canvas.Strokes.Count == 0)
            return [];

        var bounds = canvas.Strokes.GetBounds();
        if (bounds.IsEmpty)
            return [];

        const double pad = 12;
        var w = Math.Max(1, (int)Math.Ceiling(bounds.Width + pad * 2));
        var h = Math.Max(1, (int)Math.Ceiling(bounds.Height + pad * 2));

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            dc.PushTransform(new TranslateTransform(pad - bounds.Left, pad - bounds.Top));
            foreach (var stroke in canvas.Strokes)
                stroke.Draw(dc, stroke.DrawingAttributes);
            dc.Pop();
        }

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
