using System.Windows.Controls;

namespace D47.Panel;

/// <summary>
/// Renders one capability's answer. It knows display models and nothing else —
/// no capability names it as a special case, and adding a capability needs no
/// edit here.
/// </summary>
internal partial class CapabilityView : UserControl
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
