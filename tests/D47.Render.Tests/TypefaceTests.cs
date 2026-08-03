using System.Collections.Generic;
using System.Windows;

using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Render.Tests;

/// <summary>
/// The typeface is a property of what is drawn rather than of any one surface,
/// so it is set once on the shared render and the panel and both overlays get it
/// together.
///
/// <para>
/// Every fact here resolves the face the render actually reached and reads the
/// name out of the font file, rather than comparing the pack URI back to itself.
/// A pack URI naming a font nobody embedded does not throw: WPF falls back to
/// the system default and draws. A test written the other way agrees with the
/// XAML while the panel is showing Segoe UI.
/// </para>
/// </summary>
public class TypefaceTests
{
    [Fact]
    public void Render_ForEveryLineItPutsOnScreen_SetsItInSairaCondensedRegular()
    {
        IReadOnlyList<Face> faces = Rendering.FacesFor(Fixtures.HelpsAnswer());

        // Not empty first, because a render that put nothing on screen satisfies
        // every claim about what it put there.
        faces.ShouldNotBeEmpty();
        faces.ShouldAllBe(
            face => face == new Face("Saira Condensed", 400),
            customMessage:
                "body text is Regular 400, and a font WPF could not resolve is not an "
                + "error — it is a silent fall back to the system default");
    }

    [Fact]
    public void Render_ForAnythingEmphatic_HasSemiBoldToReachFor()
    {
        // Nothing on the render is emphatic yet, so this asks the family the
        // render set for the weight emphasis will want. The weight is what
        // catches a missing file: with only Regular embedded, WPF hands back
        // Regular at 400 and synthesizes the rest.
        Face face = Rendering.FaceAt(Fixtures.HelpsAnswer(), FontWeights.SemiBold);

        face.ShouldBe(
            new Face("Saira Condensed", 600),
            customMessage:
                "600 rather than 400 is the whole assertion: name the family wrong and "
                + "WPF answers every weight with the Regular file and emboldens it itself");
    }
}
