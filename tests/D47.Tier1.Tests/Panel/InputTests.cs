using System;

using Shouldly;

using Xunit;

using Rect = System.Windows.Rect;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// What <see cref="Input"/> refuses to do.
///
/// <para>
/// Not a desktop test, and that is the point of the behavior being asserted:
/// nothing here reaches the pointer, because the whole claim is that these calls
/// never get as far as sending input. Before the guard existed they did, which is
/// why this file cannot inherit <c>DesktopTest</c> and quietly skip — a
/// regression here moves the real mouse.
/// </para>
/// </summary>
public class InputTests
{
    /// <summary>
    /// An automation element with no presence on screen reports this, and it is
    /// stable — so the code that waits for a rectangle to stop moving accepts it
    /// as an answer.
    /// </summary>
    private static readonly Rect Nowhere = new(0, 0, 0, 0);

    [Fact]
    public void RightClick_AtSomethingNotOnScreen_RefusesRatherThanClickingTheCorner()
    {
        // The middle of a zero rectangle is (0,0), which is a real place: the
        // top-left pixel of the screen, where a window keeps its system-menu
        // icon. Clicking it opened an unrelated application's menu, put that
        // application's thread into a modal loop, and hung the next UI Automation
        // call for as long as anybody was willing to wait.
        //
        // The old guard did not catch it. It asked whether the pointer arrived
        // where it was sent, and the pointer had.
        Should.Throw<InvalidOperationException>(() => Input.RightClick(Nowhere))
            .Message.ShouldContain(
                "not on screen",
                Case.Insensitive,
                "the message has to name the cause, because the symptom is a click somewhere odd");
    }

    [Fact]
    public void LeftClick_AtSomethingNotOnScreen_RefusesRatherThanClickingTheCorner()
    {
        // Both gestures, because both reach the same place and either one can be
        // the one that misses. Activating a tray icon is a left click.
        Should.Throw<InvalidOperationException>(() => Input.LeftClick(Nowhere));
    }

    [Fact]
    public void Click_AtAnEmptyRectangle_RefusesToo()
    {
        // Rect.Empty is not the same value as a zero rectangle — it is positioned
        // at infinity — and arithmetic on it produces a coordinate no assertion
        // would recognise. Both mean "this is not a place".
        Should.Throw<InvalidOperationException>(() => Input.LeftClick(Rect.Empty));
    }
}
