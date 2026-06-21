using System.Drawing;

namespace MonoBooth.Session;

/// <summary>Outcome of one completed photo-booth run.</summary>
/// <param name="FilmstripPath">Where the composed strip was saved on disk.</param>
/// <param name="Filmstrip">A display copy of the strip (owned by the UI; dispose when replaced).</param>
/// <param name="Printed">Whether the strip was successfully sent to a printer.</param>
public sealed record SessionResult(string FilmstripPath, Bitmap Filmstrip, bool Printed);
