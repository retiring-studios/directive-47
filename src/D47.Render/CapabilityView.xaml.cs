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
}
