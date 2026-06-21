using System.Drawing;
using System.Drawing.Printing;

namespace MonoBooth.Printing;

/// <summary>
/// Sends the finished filmstrip to the default printer as a single 2"x6" strip, and asks the printer
/// for <c>copies</c> prints. On a dye-sub photo printer like the Kodak 6850 — loaded with 2x6 media —
/// the printer prints and cuts each strip, so the default of 2 copies yields two ready-to-hand 2x6
/// strips without the app tiling anything itself.
/// </summary>
public static class FilmstripPrinter
{
    // A single strip, in hundredths of an inch (the printer Graphics' native unit).
    private const int StripWidthHundredths = 200;   // 2 inches
    private const int StripHeightHundredths = 600;   // 6 inches

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

            // One 2x6 page; let the printer run off (and cut) the requested number of strips.
            doc.DefaultPageSettings.PaperSize = ResolveStripPaper(doc.PrinterSettings);
            doc.DefaultPageSettings.Landscape = false;
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            doc.OriginAtMargins = false;
            doc.PrinterSettings.Copies = (short)Math.Min(copies, (int)short.MaxValue);

            doc.PrintPage += (_, e) => RenderPage(e, filmstrip);
            doc.Print();
            return true;
        }
        catch (Exception ex) when (ex is InvalidPrinterException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Finds the printer's 2x6 paper (any orientation), or defines a custom 2x6.</summary>
    private static PaperSize ResolveStripPaper(PrinterSettings printer)
    {
        foreach (PaperSize size in printer.PaperSizes)
        {
            bool is2x6 =
                (Near(size.Width, StripWidthHundredths) && Near(size.Height, StripHeightHundredths)) ||
                (Near(size.Width, StripHeightHundredths) && Near(size.Height, StripWidthHundredths));
            if (is2x6)
                return size;
        }

        return new PaperSize("2x6", StripWidthHundredths, StripHeightHundredths);
    }

    private static bool Near(int a, int b) => Math.Abs(a - b) <= 10;

    private static void RenderPage(PrintPageEventArgs e, Bitmap filmstrip)
    {
        if (e.Graphics is null)
            return;

        // PageBounds is in 1/100" (the default printer PageUnit), so this fills the 2x6 strip in
        // real inches, preserving the strip's aspect and centring it.
        var page = e.PageBounds;
        float scale = Math.Min((float)page.Width / filmstrip.Width, (float)page.Height / filmstrip.Height);
        float drawWidth = filmstrip.Width * scale;
        float drawHeight = filmstrip.Height * scale;
        float x = page.Left + (page.Width - drawWidth) / 2f;
        float y = page.Top + (page.Height - drawHeight) / 2f;

        e.Graphics.DrawImage(filmstrip, x, y, drawWidth, drawHeight);
    }
}
