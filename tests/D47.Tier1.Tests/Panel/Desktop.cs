using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace D47.Tier1.Tests.Panel;

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
    /// Set this to <c>1</c> to run the desktop tests on a machine you are
    /// sitting at, having decided to leave it alone for a minute. Developing
    /// these tests is what it is for; running them out of habit is not.
    /// </summary>
    internal const string RunAnyway = "D47_DESKTOP_TESTS";

    /// <summary>
    /// Why a desktop test skipped, in the words a reader of the test output
    /// needs.
    /// </summary>
    internal const string NeedsAMachineNobodyIsUsing =
        "This test drives the real desktop — it launches the application, reads the notification "
        + "area, and sometimes moves the pointer. It needs a machine nobody is using, which CI is "
        + "and a desk is not. Set " + RunAnyway + "=1 to run it here while developing it.";

    /// <summary>
    /// Whether this machine can be relied on to leave the desktop alone.
    ///
    /// <para>
    /// True on CI, which sets <c>CI</c> and has nobody at the keyboard. False on
    /// a development machine unless someone has deliberately said otherwise.
    /// These tests take the whole desktop for as long as they run, and half of
    /// what breaks them is somebody using it — so on a desk they are something
    /// to opt into while working on them, not something a plain
    /// <c>dotnet test</c> springs on you.
    /// </para>
    /// </summary>
    internal static bool IsAvailable =>
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

    /// <summary>
    /// The window Windows currently considers foreground.
    /// </summary>
    internal static IntPtr ForegroundWindow() => GetForegroundWindow();

    /// <summary>
    /// What is in front right now, named. "It did not come forward" says
    /// nothing about why; what took the foreground instead usually does.
    /// </summary>
    internal static string DescribeForeground()
    {
        IntPtr foreground = GetForegroundWindow();

        if (foreground == IntPtr.Zero)
        {
            return "Nothing holds the foreground.";
        }

        foreach (AutomationElement top in TopLevelWindows())
        {
            try
            {
                if (new IntPtr(top.Current.NativeWindowHandle) == foreground)
                {
                    return $"Foreground is class=\"{top.Current.ClassName}\" "
                        + $"name=\"{top.Current.Name}\" pid={top.Current.ProcessId}.";
                }
            }
            catch (ElementNotAvailableException)
            {
                continue;
            }
        }

        return $"Foreground is window {foreground}, which is not a top-level element.";
    }

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the test binaries being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();

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
