using System.Windows;
using System.Windows.Controls;

using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What is behind the render in the panel window.
///
/// <para>
/// The window used to name a background of its own, in the same hex value the
/// render named independently, with nothing making the two agree. That is the
/// defect [#100](https://github.com/retiring-studios/directive-47/issues/100)
/// exists to close: there is one render, the background is a property of what is
/// drawn, and every surface presenting it gets that background with it.
/// </para>
///
/// <para>
/// A local value is what the XAML sets, which is why this asks for that and not
/// for the effective one. A window with no background of its own still reports a
/// brush — the theme's — and reading the effective value would call the fixed
/// state and the broken one the same thing.
/// </para>
///
/// <para>
/// The brush stays on the thread that built it and only a description of it
/// comes back. A brush belongs to its dispatcher, so handing one out fails when
/// the assertion tries to name it, for a reason that has nothing to do with the
/// subject.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelBackgroundTests
{
    private const string NoneOfItsOwn = "no background of its own";

    [Fact]
    public void Panel_ForWhatIsBehindTheRender_NamesNoColorOfItsOwn()
    {
        string named = StaThread.Run(() =>
        {
            object background = new MainWindow(Fixtures.HelpsAnswer())
                .ReadLocalValue(Control.BackgroundProperty);

            return background == DependencyProperty.UnsetValue
                ? NoneOfItsOwn
                : $"{background}";
        });

        named.ShouldBe(
            NoneOfItsOwn,
            customMessage:
                "a second place naming the background is a second place for it to be "
                + "wrong, and the window has no way to know it disagrees with the render");
    }
}
