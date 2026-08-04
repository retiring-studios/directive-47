using System;
using System.Windows;
using System.Windows.Controls;

namespace D47.Render;

/// <summary>
/// Renders one capability's answer. It knows display models and nothing else —
/// no capability names it as a special case, and adding a capability needs no
/// edit here.
///
/// <para>
/// Every visual surface instantiates this same control against the same answer.
/// WPF cannot put one element in two windows, so "the same render" is one
/// definition rather than one object — and parity holds because there is
/// nowhere else for a surface to get its layout from.
/// </para>
/// </summary>
public partial class CapabilityView : UserControl
{
    /// <summary>
    /// Creates the view. Set <see cref="System.Windows.FrameworkElement.DataContext"/>
    /// to an <see cref="Answer"/> to render it.
    /// </summary>
    public CapabilityView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// A view of an answer, laid out at the size it wants to be and belonging to
    /// nobody. Read <see cref="UIElement.DesiredSize"/> off it for how big that
    /// is.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Here because three surfaces asked the same question and each wrote the
    /// same four steps to answer it: the panel sizing its window, the game
    /// overlay sizing its own, and the headset sizing a texture. Getting the
    /// sequence wrong does not fail loudly — it produces a plausible wrong
    /// number — so three copies of it were three chances to produce one.
    /// </para>
    /// <para>
    /// <b>A throwaway rather than an instance already in a window.</b> One with a
    /// parent answers what it wants <i>inside whatever it is sitting in</i>
    /// rather than what it wants. On the game overlay that came out exactly one
    /// padding narrower than the truth — 64 device-independent pixels — which is
    /// the sort of wrong that looks plausible.
    /// </para>
    /// <para>
    /// <b>Against infinity.</b> The render decides how big it is, and every
    /// surface then presents that at whatever size it has. A constraint here
    /// would make the caller the thing deciding the layout, which is the
    /// direction [#96](https://github.com/retiring-studios/directive-47/issues/96)
    /// establishes runs the other way.
    /// </para>
    /// <para>
    /// <b>Arranged and updated, not merely measured.</b> The answer's template is
    /// chosen by a resource lookup that needs the control connected, so measuring
    /// alone returns an almost-empty box at a default size — the same default
    /// whatever the answer says. Measured, not reasoned about: dropping
    /// <c>UpdateLayout</c> as redundant is exactly what produced that, and it
    /// shipped once as an overlay too narrow for its own title bar.
    /// </para>
    /// <para>
    /// The laid-out control comes back rather than its size, because one caller
    /// needs the control itself. The headset draws it to a bitmap and first
    /// arranges it again at whole pixels — its own concern, not every surface's.
    /// Handing back only a size would have left that caller writing these four
    /// steps again, which is the duplication this exists to remove.
    /// </para>
    /// </remarks>
    /// <param name="answer">What the render would be showing.</param>
    /// <returns>The view, measured and arranged at its natural size.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
    public static CapabilityView LaidOutFor(Answer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        var asking = new CapabilityView { DataContext = answer };

        asking.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        asking.Arrange(new Rect(asking.DesiredSize));
        asking.UpdateLayout();

        return asking;
    }
}
