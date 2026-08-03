using System.Linq;
using System.Windows.Media;

namespace D47.Render.Tests;

/// <summary>
/// A face something actually resolved to, reduced to values that can leave the
/// thread that built the visual tree.
/// </summary>
///
/// <remarks>
/// Read out of the font file rather than off the element, because that is the
/// only version of the question a wrong pack URI can fail. WPF resolves a font
/// it cannot find to the system default and draws it without complaint, so an
/// element asked what family it was told to use answers "Saira Condensed" while
/// the screen says Segoe UI.
/// </remarks>
///
/// <param name="Family">The family name the font file itself declares.</param>
/// <param name="Weight">Its OpenType weight: 400 regular, 600 semibold.</param>
internal sealed record Face(string Family, int Weight)
{
    /// <summary>
    /// Works out which font file WPF would actually draw a typeface with.
    /// </summary>
    /// <param name="typeface">The typeface to resolve.</param>
    /// <returns>The face it resolved to.</returns>
    internal static Face Of(Typeface typeface) =>
        typeface.TryGetGlyphTypeface(out GlyphTypeface? glyph)
            ? new Face(glyph.FamilyNames.Values.First(), glyph.Weight.ToOpenTypeWeight())
            : new Face("nothing WPF could resolve", 0);
}
