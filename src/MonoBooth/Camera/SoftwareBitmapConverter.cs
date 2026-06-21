using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace MonoBooth.Camera;

/// <summary>
/// Converts a WinRT <see cref="SoftwareBitmap"/> (what MediaCapture hands us) into a GDI+
/// <see cref="Bitmap"/> (what WinForms and the filmstrip compositor understand).
/// </summary>
/// <remarks>
/// Uses only projected WinRT APIs (<see cref="SoftwareBitmap.CopyToBuffer"/> + a
/// <see cref="DataReader"/>) to pull the pixels out. The older UWP trick of QI-casting a
/// <c>BitmapBuffer</c> reference to a custom <c>IMemoryBufferByteAccess</c> COM interface throws an
/// <see cref="InvalidCastException"/> under the modern C#/WinRT projection, so we avoid it entirely.
/// </remarks>
internal static class SoftwareBitmapConverter
{
    /// <summary>Returns a new <see cref="Bitmap"/>; the caller owns and must dispose it.</summary>
    public static Bitmap ToBitmap(SoftwareBitmap source)
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
            int byteCount = checked(4 * width * height);

            // Pull the Bgra8 pixels (tightly packed, stride == 4 * width) into a managed array.
            var pixels = new byte[byteCount];
            var winrtBuffer = new Buffer((uint)byteCount);
            src.CopyToBuffer(winrtBuffer);
            using (var reader = DataReader.FromBuffer(winrtBuffer))
            {
                reader.ReadBytes(pixels);
            }

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int sourceStride = 4 * width;
                if (data.Stride == sourceStride)
                {
                    Marshal.Copy(pixels, 0, data.Scan0, byteCount);
                }
                else
                {
                    // GDI+ may pad rows; copy a row at a time to respect the destination stride.
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(pixels, y * sourceStride,
                            IntPtr.Add(data.Scan0, y * data.Stride), sourceStride);
                    }
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
