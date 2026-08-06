using System.Runtime.InteropServices;
using System.Windows;

namespace D47.GameOverlay;

/// <summary>
/// The desktop, as much of it as anything deciding where a window may go needs
/// to know: how far the screens reach between them.
///
/// <para>
/// Here rather than in <c>D47.Panel</c> for the same reason
/// <see cref="EliteWindow.IsTheGame(nint)"/> is: asking the operating system what the
/// machine looks like is an adapter's job, and what to do with the answer is
/// not. The panel injects this and a test injects a rectangle, which is what
/// lets a monitor disappearing be asserted in CI on a machine whose monitors
/// are all still plugged in.
/// </para>
/// </summary>
public static class Desktop
{
    /// <summary>
    /// The left edge of the virtual screen.
    /// </summary>
    private const int LeftmostPoint = 76;

    /// <summary>
    /// The top edge of the virtual screen.
    /// </summary>
    private const int HighestPoint = 77;

    /// <summary>
    /// How wide every screen together is.
    /// </summary>
    private const int WidthOfThemAll = 78;

    /// <summary>
    /// How tall every screen together is.
    /// </summary>
    private const int HeightOfThemAll = 79;

    /// <summary>
    /// The rectangle every screen on this machine together covers, in physical
    /// pixels.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A bounding box rather than the exact union, so a point in the gap
    /// between two screens that are not aligned counts as on the desktop. That
    /// is the cheaper answer and it is right about the case this is for: a
    /// screen that has been unplugged shrinks the box, so a position remembered
    /// on it falls outside. Getting the gaps right as well means enumerating
    /// monitors, which is a great deal more interop to refine an answer nobody
    /// has been wrong about.
    /// </para>
    /// <para>
    /// Asked each time rather than cached. Monitors are plugged in, unplugged
    /// and rearranged while the machine is running, which is the entire reason
    /// this question exists.
    /// </para>
    /// </remarks>
    /// <returns>Where windows can be seen.</returns>
    public static Rect Everything() =>
        new(
            GetSystemMetrics(LeftmostPoint),
            GetSystemMetrics(HighestPoint),
            GetSystemMetrics(WidthOfThemAll),
            GetSystemMetrics(HeightOfThemAll));

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the executable being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSystemMetrics(int metric);
}
