using System;
using System.Globalization;
using System.Text;
using System.Windows.Automation;

namespace D47.Panel.Tests;

/// <summary>
/// The one desktop these tests share — the name that serializes access to it,
/// and the two things worth asking of it as a whole.
/// </summary>
internal static class Desktop
{
    /// <summary>
    /// The collection name shared by every test class that drives the real
    /// desktop.
    ///
    /// <para>
    /// There is one desktop, one notification area, and one overflow flyout,
    /// and xUnit runs test classes in parallel. Two classes launching panels at
    /// once meant a test could close the other one's window, and a test asking
    /// whether the tray icon had gone could be answered by an icon belonging to
    /// a panel that was still running. Sharing a collection name is what makes
    /// them take turns.
    /// </para>
    ///
    /// <para>
    /// The in-process tests — the ones that build a visual tree and inspect it
    /// — are deliberately not in here. They touch nothing outside their own
    /// thread, and slowing them down would buy nothing.
    /// </para>
    /// </summary>
    internal const string Collection = "The desktop";

    /// <summary>
    /// Set this to <c>1</c> to run the pointer-driven tests on a machine you
    /// are sitting at, having decided to leave it alone for a minute.
    /// </summary>
    private const string RunAnyway = "D47_DESKTOP_INPUT_TESTS";

    /// <summary>
    /// Why a pointer-driven test skipped, in the words a reader of the test
    /// output needs.
    /// </summary>
    internal const string NeedsAnIdleMachine =
        "This test moves the real pointer, and the shell's overflow flyout is dismissed by any "
        + "focus change at all — a click, a keystroke, a media key. It needs a machine nobody is "
        + "using, which CI is and a desk is not. Set " + RunAnyway + "=1 to run it here anyway.";

    /// <summary>
    /// Whether this machine can be relied on to leave the pointer alone.
    ///
    /// <para>
    /// True on CI, which sets <c>CI</c> and has nobody at the keyboard. False on
    /// a development machine unless someone has deliberately said otherwise,
    /// because a test that seizes the pointer for half a minute and fails if you
    /// touch anything is not something to run by surprise.
    /// </para>
    /// </summary>
    internal static bool IsUndisturbed =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
        || Environment.GetEnvironmentVariable(RunAnyway) == "1";

    /// <summary>
    /// Looks for an element under every top-level window.
    ///
    /// <para>
    /// A context menu is its own top-level window and is not a descendant of
    /// whatever opened it, so when the thing being looked for is a menu there
    /// is nothing narrower to search than the whole desktop.
    /// </para>
    /// </summary>
    /// <param name="condition">What to look for.</param>
    /// <returns>The first match, or <see langword="null"/>.</returns>
    internal static AutomationElement? Find(Condition condition)
    {
        foreach (AutomationElement top in TopLevelWindows())
        {
            try
            {
                AutomationElement? found = top.FindFirst(TreeScope.Descendants, condition);

                if (found is not null)
                {
                    return found;
                }
            }
            catch (ElementNotAvailableException)
            {
                // A window closed while being searched. Menus do that.
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Every top-level window, with any menu items under it. What a failing
    /// right-click needs is not "no menu appeared" but a list of what did.
    /// </summary>
    internal static string Describe()
    {
        var description = new StringBuilder();
        description.AppendLine("Top-level windows at the time:");

        foreach (AutomationElement top in TopLevelWindows())
        {
            try
            {
                var line = new StringBuilder(
                    $"  class=\"{top.Current.ClassName}\" name=\"{top.Current.Name}\"");

                AutomationElementCollection items = top.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(
                        AutomationElement.ControlTypeProperty, ControlType.MenuItem));

                for (int index = 0; index < items.Count; index++)
                {
                    line.Append(CultureInfo.InvariantCulture, $" [{items[index].Current.Name}]");
                }

                description.AppendLine(line.ToString());
            }
            catch (ElementNotAvailableException)
            {
                // A window closed while being described. Menus do that.
                continue;
            }
        }

        return description.ToString();
    }

    private static AutomationElement[] TopLevelWindows()
    {
        AutomationElement? root = AutomationElement.RootElement;

        if (root is null)
        {
            return [];
        }

        AutomationElementCollection tops = root.FindAll(
            TreeScope.Children, Condition.TrueCondition);

        var windows = new AutomationElement[tops.Count];

        for (int index = 0; index < tops.Count; index++)
        {
            windows[index] = tops[index];
        }

        return windows;
    }
}
