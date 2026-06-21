using System.Drawing;
using System.Drawing.Printing;

namespace MonoBooth.Printing;

/// <summary>
/// Sends a finished filmstrip to the default printer, laying out <c>copies</c> strips side by side
/// on the page (so a 2x6 print yields the traditional pair of identical strips to tear apart).
/// </summary>
public static class FilmstripPrinter
{
    /// <summary>
    /// Prints the strip. Returns <c>false</c> if there is no usable printer or printing fails — the
    /// booth keeps running either way (the original code crashed when no printer was attached).
    /// </summary>
    public static bool Print(Bitmap filmstrip, int copies)
    {
        copies = Math.Max(1, copies);

        try
        {
            using var doc = new PrintDocument { DocumentName = "MonoBooth filmstrip" };

            if (string.IsNullOrEmpty(doc.PrinterSettings.PrinterName) ||
                !doc.PrinterSettings.IsValid)
            {
                return false;
            }

            doc.PrintPage += (_, e) => RenderPage(e, filmstrip, copies);
            doc.Print();
            return true;
        }
        catch (Exception ex) when (ex is InvalidPrinterException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void RenderPage(PrintPageEventArgs e, Bitmap filmstrip, int copies)
    {
        var area = e.MarginBounds;
        if (e.Graphics is null)
            return;

        // Scale each strip so all copies fit across the printable width while preserving aspect.
        float gap = 10f;
        float slotWidth = (area.Width - gap * (copies - 1)) / copies;
        float scale = Math.Min(slotWidth / filmstrip.Width, (float)area.Height / filmstrip.Height);

        float drawWidth = filmstrip.Width * scale;
        float drawHeight = filmstrip.Height * scale;

        for (int i = 0; i < copies; i++)
        {
            float x = area.Left + i * (slotWidth + gap) + (slotWidth - drawWidth) / 2f;
            e.Graphics.DrawImage(filmstrip, x, area.Top, drawWidth, drawHeight);
        }
    }
}
