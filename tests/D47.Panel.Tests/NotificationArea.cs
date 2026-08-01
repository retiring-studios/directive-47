using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace D47.Panel.Tests;

/// <summary>
/// The Windows notification area, read from outside. Everything else this
/// project asserts belongs to us — a visual tree we built, or a window our own
/// process presents. This belongs to <c>explorer.exe</c>, and the only thing we
/// contribute to it is one icon.
///
/// <para>
/// Reachable through the same in-box UI Automation client the rest of the
/// harness uses. That was worth establishing rather than assuming: a hosted CI
/// runner might plausibly have no shell at all, which would have put this
/// story's test on the dev PC instead of in <c>ci.slnf</c>. It has one.
/// </para>
///
/// <para>
/// An icon's accessible name is its tooltip text, so an icon with no tooltip is
/// anonymous here and cannot be asserted on at all.
/// </para>
/// </summary>
internal static class NotificationArea
{
    private const string TaskbarClass = "Shell_TrayWnd";
    private const string OverflowClass = "TopLevelWindowForOverflowXamlIsland";

    /// <summary>
    /// What the shell classes its notification-area buttons as, on show and in
    /// the overflow alike — <c>SystemTray.NormalButton</c>,
    /// <c>SystemTray.AccentButton</c>, <c>SystemTray.OmniButton</c>. Start, the
    /// pinned applications and the running-window buttons are not any of these,
    /// which is the point: without this filter a task button could answer for
    /// an icon that was never there.
    /// </summary>
    private const string TrayButtonClassPrefix = "SystemTray.";

    /// <summary>
    /// The overflow chevron announces itself as "Show Hidden Icons" when the
    /// flyout is closed and "Show Hidden Icons Hide" when it is open, so this
    /// is a prefix and not the whole name. Matching the whole name found no
    /// chevron whenever one was already open, and reported that as nothing
    /// being hidden.
    /// </summary>
    private const string ChevronNamePrefix = "Show Hidden Icons";

    private static readonly TimeSpan FlyoutPatience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether an icon with the given tooltip is in the notification area,
    /// whether it is on show or behind the overflow chevron.
    /// </summary>
    /// <param name="tooltip">The tooltip text the icon was given.</param>
    /// <exception cref="InvalidOperationException">
    /// There is no taskbar to look at.
    /// </exception>
    internal static bool Holds(string tooltip)
    {
        AutomationElement taskbar = Taskbar();

        if (Names(taskbar).Contains(tooltip))
        {
            return true;
        }

        AutomationElement? chevron = Chevron(taskbar);

        if (chevron is null)
        {
            // No chevron means nothing is hidden, so the visible icons were the
            // whole notification area and the answer is already in.
            return false;
        }

        return WithOverflowOpen(chevron, overflow => Names(overflow).Contains(tooltip));
    }

    /// <summary>
    /// Waits for an icon to appear or disappear. The shell adds and removes
    /// icons on its own schedule, so both directions need patience rather than
    /// a single look.
    /// </summary>
    /// <param name="tooltip">The tooltip text the icon was given.</param>
    /// <param name="present">What to wait for.</param>
    /// <param name="within">How long to wait.</param>
    /// <returns>Whether it reached that state in time.</returns>
    internal static bool WaitUntil(string tooltip, bool present, TimeSpan within)
    {
        DateTime deadline = DateTime.UtcNow + within;

        while (true)
        {
            if (Holds(tooltip) == present)
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            // A second, not the usual quarter, because every look that does not
            // find the icon opens and closes the overflow flyout to be sure.
            // Polling faster would spend the whole wait animating the shell.
            Thread.Sleep(1000);
        }
    }

    /// <summary>
    /// Every tooltip in the notification area, on show and hidden, as text.
    /// Carried by the failure message of every assertion here, because "the
    /// icon was not there" without the list tells you nothing about why.
    /// </summary>
    internal static string Describe()
    {
        var description = new StringBuilder();

        AutomationElement taskbar;

        try
        {
            taskbar = Taskbar();
        }
        catch (InvalidOperationException problem)
        {
            return problem.Message;
        }

        description.AppendLine("Notification area, on show:");
        AppendAll(description, Names(taskbar));

        AutomationElement? chevron = Chevron(taskbar);

        if (chevron is null)
        {
            description.AppendLine("Nothing is hidden — there is no overflow chevron.");
            return description.ToString();
        }

        description.AppendLine("Notification area, hidden behind the chevron:");

        IReadOnlyList<string> hidden = WithOverflowOpen(chevron, Names);
        AppendAll(description, hidden);

        return description.ToString();
    }

    private static AutomationElement Taskbar()
    {
        AutomationElement root = AutomationElement.RootElement
            ?? throw new InvalidOperationException(
                "UI Automation has no root element. This machine has no interactive desktop.");

        return root.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ClassNameProperty, TaskbarClass))
            ?? throw new InvalidOperationException(
                $"No window of class {TaskbarClass}. This machine has no taskbar, so it has "
                + "no notification area to put an icon in.");
    }

    /// <summary>
    /// Opens the overflow flyout, reads it, and puts it away again. The flyout
    /// is a top-level window that does not exist while it is closed, so there
    /// is nothing to read without opening it first.
    /// </summary>
    private static T WithOverflowOpen<T>(AutomationElement chevron, Func<AutomationElement, T> read)
    {
        AutomationElement? overflow = Overflow();
        bool weOpenedIt = false;

        if (overflow is null)
        {
            Invoke(chevron);
            weOpenedIt = true;

            DateTime deadline = DateTime.UtcNow + FlyoutPatience;

            while (overflow is null && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
                overflow = Overflow();
            }

            if (overflow is null)
            {
                throw new InvalidOperationException(
                    $"The \"{ChevronNamePrefix}\" chevron was invoked but no overflow flyout appeared "
                    + $"within {FlyoutPatience.TotalSeconds:0} seconds.");
            }
        }

        try
        {
            return read(overflow);
        }
        finally
        {
            if (weOpenedIt)
            {
                // Left open, the flyout sits over whatever the next test does.
                Invoke(chevron);
            }
        }
    }

    private static AutomationElement? Chevron(AutomationElement taskbar) =>
        TrayButtons(taskbar).FirstOrDefault(
            button => Name(button)?.StartsWith(ChevronNamePrefix, StringComparison.Ordinal) == true);

    private static AutomationElement? Overflow() =>
        AutomationElement.RootElement?.FindFirst(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ClassNameProperty, OverflowClass));

    private static void Invoke(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
        {
            throw new InvalidOperationException(
                $"\"{element.Current.Name}\" cannot be invoked, so the overflow cannot be opened.");
        }

        ((InvokePattern)pattern).Invoke();
    }

    /// <summary>
    /// The tooltip of every notification-area icon under an element.
    /// </summary>
    private static IReadOnlyList<string> Names(AutomationElement under)
    {
        List<string> names = [];

        foreach (AutomationElement button in TrayButtons(under))
        {
            string? name = Name(button);

            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Every notification-area button under an element, in tree order.
    /// </summary>
    private static IEnumerable<AutomationElement> TrayButtons(AutomationElement under)
    {
        AutomationElementCollection buttons = under.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

        for (int index = 0; index < buttons.Count; index++)
        {
            AutomationElement button = buttons[index];
            string className;

            try
            {
                className = button.Current.ClassName;
            }
            catch (ElementNotAvailableException)
            {
                // The shell removed it between enumerating and reading. Not our
                // icon's business either way.
                continue;
            }

            if (className.StartsWith(TrayButtonClassPrefix, StringComparison.Ordinal))
            {
                yield return button;
            }
        }
    }

    /// <summary>
    /// A button's tooltip, trimmed, or <see langword="null"/> if it went away
    /// while being read. Some icons come back with a leading space —
    /// " Discord", " Ollama" — which is the owning application's doing and not
    /// something a caller should have to know about.
    /// </summary>
    private static string? Name(AutomationElement button)
    {
        try
        {
            return button.Current.Name.Trim();
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    private static void AppendAll(StringBuilder description, IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            description.AppendLine("  (nothing)");
            return;
        }

        foreach (string name in names)
        {
            description.AppendLine(CultureInfo.InvariantCulture, $"  \"{name}\"");
        }
    }
}
