using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;

namespace D47.TestSupport;

/// <summary>
/// Reads back what a real control says about itself to anything listening — a
/// screen reader, or automation looking for something by name.
///
/// <para>
/// A different tree from <see cref="VisualTree"/> and asked for a different
/// reason. That one answers what is on screen; this one answers what the screen
/// is called, and the two disagree far more often than they look like they
/// could. A name set in XAML reads back perfectly from the element carrying it
/// while reaching no peer at all, so only the peers can fail that way — which is
/// what [#167](https://github.com/retiring-studios/directive-47/issues/167) was.
/// </para>
///
/// <para>
/// Here rather than in one test project because the render and the panel both
/// ask it, and the panel's answer is the one that matters: the render's name has
/// to survive being put in the window that hosts it.
/// </para>
/// </summary>
public static class AutomationTree
{
    /// <summary>
    /// What an element and everything under it call themselves, in tree order.
    /// </summary>
    ///
    /// <remarks>
    /// An element with no peer contributes nothing rather than throwing. Not
    /// every element has one — <c>ContentControl</c> is the case that started
    /// this — and an empty list fails an assertion that something is named,
    /// which is the right failure and a readable one.
    /// </remarks>
    /// <param name="root">Where to start looking.</param>
    /// <returns>The names, in tree order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    public static IReadOnlyList<string> NamesIn(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        List<string> names = [];
        Collect(UIElementAutomationPeer.CreatePeerForElement(root), names);

        return names;
    }

    /// <summary>
    /// Walks the peers down, collecting what each is called.
    /// </summary>
    private static void Collect(AutomationPeer? peer, List<string> names)
    {
        if (peer is null)
        {
            return;
        }

        names.Add(peer.GetName());

        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            Collect(child, names);
        }
    }
}
