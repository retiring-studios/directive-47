using System.Windows;
using System.Windows.Controls;

namespace D47.Panel;

/// <summary>
/// Chooses the template for an answer from the display model its descriptor
/// declares, and from nothing else. This is the whole of the panel's knowledge
/// about what it is rendering: a display model type maps to a resource of the
/// same name.
///
/// <para>
/// Deliberately not a switch on capability id. The moment the panel knows the
/// word "help" it owes the same knowledge to thirty-seven more capabilities,
/// and "no capability-specific rendering code" stops being true.
/// </para>
/// </summary>
internal sealed class DisplayModelTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Finds the template declared for this answer's display model.
    /// </summary>
    /// <param name="item">The <see cref="Answer"/> being rendered.</param>
    /// <param name="container">The element whose resources are searched.</param>
    /// <returns>
    /// The matching template, or <see langword="null"/> when the answer is not
    /// an <see cref="Answer"/> or no template is declared for its display model.
    /// </returns>
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not Answer answer || container is not FrameworkElement element)
        {
            return null;
        }

        return element.TryFindResource(answer.Descriptor.Display.GetType().Name) as DataTemplate;
    }
}
