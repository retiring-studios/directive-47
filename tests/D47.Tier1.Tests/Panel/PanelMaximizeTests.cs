using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

using D47.Panel;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Whether the panel can be thrown to full screen.
///
/// <para>
/// It should not be. Decided alongside
/// [#157](https://github.com/retiring-studios/directive-47/issues/157): the
/// panel, the game overlay and the VR overlay are one minimal UI, and an overlay
/// overcomes a larger panel by scaling in its own environment. A panel that goes
/// full screen has stopped being minimal.
/// </para>
///
/// <para>
/// Asked of the window Windows actually has, rather than of a WPF property. The
/// button is not the claim — double-clicking the title bar and Win+Up maximize
/// a window without ever going near it, and all three are governed by the one
/// style bit. Reading the bit off the live handle is the version of the question
/// that covers the gestures nobody would think to drive.
/// </para>
///
/// <para>
/// In-process and off the desktop tier, like <c>PanelSizeTests</c> — the window
/// is opened without taking the foreground and gone before the test returns.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelMaximizeTests
{
    /// <summary>
    /// The window's own styles, as opposed to its extended ones.
    /// </summary>
    private const int ItsOwnStyles = -16;

    /// <summary>
    /// <c>WS_MAXIMIZEBOX</c>. The button, the title bar's double-click and
    /// Win+Up, all at once.
    /// </summary>
    private const long CanBeMaximized = 0x00010000;

    /// <summary>
    /// <c>WS_THICKFRAME</c>. The edge a window is dragged by.
    /// </summary>
    private const long CanBeDragged = 0x00040000;

    [Fact]
    public void Panel_WhateverItsSize_CannotBeMaximized()
    {
        long style = OpenedPanel.Opened((panel, _) => StyleOf(panel));

        // Both halves, because the difficulty is that they come together.
        // ResizeMode has no value meaning "resizable but not maximizable" —
        // CanResize and CanResizeWithGrip give both, NoResize and CanMinimize
        // give neither — so a fix that reaches for one of the four satisfies
        // half of this and breaks the other half.
        (style & CanBeMaximized).ShouldBe(
            0,
            "the panel is one of three surfaces in a minimal UI, and one that can be thrown "
            + "to full screen has stopped being minimal");

        (style & CanBeDragged).ShouldBe(
            CanBeDragged,
            "drag-resize is how the Commander chooses the panel's size, and #96 shipped "
            + "scrolling underneath it");
    }

    /// <summary>
    /// The window styles Windows is holding for a window.
    /// </summary>
    private static long StyleOf(MainWindow panel) =>
        GetWindowLongPtr(new WindowInteropHelper(panel).Handle, ItsOwnStyles).ToInt64();

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the executable being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
}
