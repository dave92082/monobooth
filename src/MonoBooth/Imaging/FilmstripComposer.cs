using System.Drawing;
using System.Drawing.Drawing2D;

namespace MonoBooth.Imaging;

/// <summary>
/// Stacks the captured frames into a single vertical filmstrip with a coloured border around each
/// shot — the classic photo-booth look. Replaces the original copy/pasted <c>AppendBorder</c> +
/// <c>saveFilmStrip</c> code with one allocation-safe routine.
/// </summary>
public static class FilmstripComposer
{
    /// <summary>
    /// Builds the filmstrip. All source frames are normalised to the first frame's size so an
    /// odd-sized capture can't skew the layout. The caller owns the returned bitmap.
    /// </summary>
    public static Bitmap Compose(IReadOnlyList<Bitmap> frames, int borderWidth, Color borderColor)
    {
        if (frames is null || frames.Count == 0)
            throw new ArgumentException("At least one frame is required.", nameof(frames));

        int cellWidth = frames[0].Width;
        int cellHeight = frames[0].Height;

        int stripWidth = cellWidth + borderWidth * 2;
        int stripHeight = (cellHeight + borderWidth * 2) * frames.Count;

        var strip = new Bitmap(stripWidth, stripHeight);
        using (var canvas = Graphics.FromImage(strip))
        {
            canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
            canvas.Clear(borderColor);

            for (int i = 0; i < frames.Count; i++)
            {
                int y = i * (cellHeight + borderWidth * 2) + borderWidth;
                canvas.DrawImage(frames[i], new Rectangle(borderWidth, y, cellWidth, cellHeight));
            }
        }

        return strip;
    }
}
