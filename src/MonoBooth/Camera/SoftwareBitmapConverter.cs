using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;

namespace MonoBooth.Camera;

/// <summary>
/// Converts a WinRT <see cref="SoftwareBitmap"/> (what MediaCapture hands us) into a GDI+
/// <see cref="Bitmap"/> (what WinForms and the filmstrip compositor understand).
/// </summary>
internal static class SoftwareBitmapConverter
{
    // COM interface that lets us reach the raw bytes behind a BitmapBuffer reference.
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    /// <summary>Returns a new <see cref="Bitmap"/>; the caller owns and must dispose it.</summary>
    public static unsafe Bitmap ToBitmap(SoftwareBitmap source)
    {
        // GDI+ wants Bgra8 / premultiplied. Convert only when the source isn't already in that shape.
        SoftwareBitmap? converted = null;
        var src = source;
        if (source.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            source.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            converted = SoftwareBitmap.Convert(
                source, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            src = converted;
        }

        try
        {
            int width = src.PixelWidth;
            int height = src.PixelHeight;
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                using var buffer = src.LockBuffer(BitmapBufferAccessMode.Read);
                using var reference = buffer.CreateReference();
                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* srcBytes, out _);

                var plane = buffer.GetPlaneDescription(0);
                var destBase = (byte*)data.Scan0;
                long rowBytes = Math.Min(plane.Stride, Math.Abs(data.Stride));

                for (int y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(
                        srcBytes + plane.StartIndex + (long)plane.Stride * y,
                        destBase + (long)data.Stride * y,
                        data.Stride,
                        rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        finally
        {
            converted?.Dispose();
        }
    }
}
