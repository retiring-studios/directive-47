using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;

using Shouldly;

using Xunit;

using Forms = System.Windows.Forms;

namespace D47.Panel.Tests;

/// <summary>
/// The mark, wherever Windows draws it: the executable in Explorer, the title
/// bar, the taskbar button, alt-tab, and the notification area.
///
/// <para>
/// In-process and fast. These read the icon that ships — the resource compiled
/// into the panel assembly and the icon resource compiled into the executable —
/// rather than a copy of the file sitting in the repository, because the thing
/// that can go wrong is what packaging did to it.
/// </para>
///
/// <para>
/// The reason this file exists at all is an encoding trap. 128 and 256 frames
/// are PNG-compressed to keep the file near its old size, and GDI+ cannot decode
/// PNG frames — <c>Icon.ToBitmap</c> throws
/// <c>ArgumentOutOfRangeException</c> on one rather than degrading to a blank
/// square. The tray does not go through GDI+, which is what makes it safe; that
/// is measured here rather than assumed, because the assumption is the whole
/// reason the small file is affordable.
/// </para>
/// </summary>
public class ApplicationIconTests
{
    /// <summary>
    /// The largest frame stored as an uncompressed DIB. Everything above this is
    /// PNG, which is where GDI+ stops being able to read it.
    /// </summary>
    private const int LargestClassicFrame = 64;

    [Fact]
    public void ApplicationIcon_IsBuiltIntoTheExecutable_SoWindowsHasAMarkToDrawAtAll()
    {
        // ApplicationIcon in the project file is the whole mechanism. Without it
        // a WPF executable carries no icon resource, and Explorer, the title
        // bar, the taskbar button and alt-tab all fall back to the generic one.
        CountIconsIn(Executable(), 32).ShouldBeGreaterThan(
            0,
            customMessage:
                "the executable carries no icon resource at all, which is the default WPF "
                + "icon everywhere Windows shows the application");
    }

    [Fact]
    public void ExecutableIcon_IsTheSameMark_AsTheNotificationAreaShows()
    {
        // Counting icons only proves something is there. The application and the
        // tray carrying different marks would satisfy that and still be the bug
        // this story is about.
        using Icon fromExecutable = ExtractFrom(Executable(), 32);
        using var fromResource = new Icon(ShippedIcon(), 32, 32);
        using Bitmap drawnFromExecutable = Render(fromExecutable);
        using Bitmap drawnFromResource = Render(fromResource);

        // Pixel for pixel, not a count of covered pixels: both come from the
        // same 32 frame of the same file, so anything less than identical means
        // they are not the same mark, and two different marks can easily cover
        // the same number of pixels.
        Differences(drawnFromExecutable, drawnFromResource).ShouldBe(
            0,
            customMessage:
                "the executable's icon and the notification area's icon should be the same "
                + "mark, drawn from the same frame of the same file");
    }

    [Fact]
    public void Icon_CarriesFramesBeyondTheClassicRange_SoLargeViewsAreNotAnUpscale()
    {
        // The reason 128 and 256 were added. Until this story the file stopped
        // at 64, and everything above it was Windows stretching a 64.
        //
        // Asserted against the resource rather than the executable, and
        // deliberately: PrivateExtractIcons upscales silently, so asking the
        // executable for 256 returns a 256 whether or not a 256 frame exists
        // and cannot tell the two apart. Icon's own frame selection can, because
        // it picks the nearest frame that is really in the file. The executable
        // embeds this same file, so proving it here proves it there.
        using var large = new Icon(ShippedIcon(), 256, 256);

        large.Width.ShouldBeGreaterThan(
            LargestClassicFrame,
            customMessage:
                "the file stops at 64, so Explorer's large views are showing a stretched 64");

        Covered(large).ShouldBeGreaterThan(
            0,
            customMessage: "a large frame that renders nothing is worse than no large frame");
    }

    [Fact]
    public void TrayIcon_AtTheNotificationAreaSize_LoadsAPictureRatherThanNothing()
    {
        // The regression the encoding trap would cause, at the size this machine
        // actually asks for. A blank tray icon is not an error — it is a
        // correctly sized square of nothing — so the assertion has to be about
        // pixels and not about null.
        using var icon = new Icon(ShippedIcon(), Forms.SystemInformation.SmallIconSize);

        icon.Width.ShouldBeLessThanOrEqualTo(
            LargestClassicFrame,
            customMessage:
                "the notification area asked for a size that selects a PNG frame; that still "
                + "draws, but it means this machine is past where the classic frames stop");

        Covered(icon).ShouldBeGreaterThan(
            0, customMessage: "the tray icon loaded as a blank square");
    }

    [Theory]
    [InlineData(16)]   // 100% scaling
    [InlineData(32)]   // 200%
    [InlineData(48)]   // 300%
    [InlineData(64)]   // 400%, and the last classic frame
    [InlineData(128)]  // past it, and PNG
    [InlineData(256)]
    public void TrayIcon_AtEveryScalingWindowsCanAskFor_StillDrawsThroughTheHandleTheTrayUses(
        int requested)
    {
        // SystemInformation.SmallIconSize grows with display scaling, so a
        // machine past 400% asks for more than 64 and selects a PNG frame.
        //
        // That is safe, and this is the measurement saying so. NotifyIcon hands
        // Shell_NotifyIcon the HICON, and the HICON is built by Windows, which
        // decodes PNG frames perfectly well. Only GDI+ cannot — so the way to
        // ask "would the tray draw this" is to render the handle, which is the
        // one thing the tray actually uses.
        using var icon = new Icon(ShippedIcon(), requested, requested);

        Covered(icon).ShouldBeGreaterThan(
            0,
            customMessage:
                $"asking for {requested} produced a handle that draws nothing, which in the "
                + "notification area is an icon that silently is not there");
    }

    /// <summary>
    /// Renders through the icon's handle, which is what the notification area
    /// draws, rather than through <c>ToBitmap</c> on the icon itself, which is
    /// GDI+ and throws on a PNG frame. Rendering the handle is the only way to
    /// ask the question the tray cares about.
    ///
    /// <para>
    /// The caller owns the bitmap that comes back.
    /// </para>
    /// </summary>
    private static Bitmap Render(Icon icon)
    {
        // FromHandle does not own the handle it is given, so disposing this
        // wrapper leaves the caller's icon — which does own it — intact.
        using var fromHandle = Icon.FromHandle(icon.Handle);

        return fromHandle.ToBitmap();
    }

    /// <summary>
    /// How many pixels the mark actually covers. A blank icon and a drawn one
    /// are the same size and the same type; this is what tells them apart.
    /// </summary>
    private static int Covered(Icon icon)
    {
        using Bitmap drawn = Render(icon);

        int covered = 0;

        for (int y = 0; y < drawn.Height; y++)
        {
            for (int x = 0; x < drawn.Width; x++)
            {
                if (drawn.GetPixel(x, y).A > 0)
                {
                    covered++;
                }
            }
        }

        return covered;
    }

    /// <summary>
    /// How many pixels two renderings disagree on. Zero is the same picture;
    /// a differing size is every pixel, since there is nothing to compare.
    /// </summary>
    private static int Differences(Bitmap left, Bitmap right)
    {
        if (left.Width != right.Width || left.Height != right.Height)
        {
            return left.Width * left.Height;
        }

        int differing = 0;

        for (int y = 0; y < left.Height; y++)
        {
            for (int x = 0; x < left.Width; x++)
            {
                if (left.GetPixel(x, y) != right.GetPixel(x, y))
                {
                    differing++;
                }
            }
        }

        return differing;
    }

    /// <summary>
    /// The icon as it ships inside the panel assembly, which is where the
    /// application reads it from. Copied out, so the stream can be read as many
    /// times as there are tests.
    /// </summary>
    private static MemoryStream ShippedIcon()
    {
        const string Key = "assets/directive-47.ico";

        using Stream resources =
            typeof(App).Assembly.GetManifestResourceStream("D47.Panel.g.resources")
            ?? throw new InvalidOperationException(
                "D47.Panel carries no compiled WPF resources at all.");

        using var reader = new ResourceReader(resources);

        foreach (DictionaryEntry entry in reader)
        {
            if (!Key.Equals(entry.Key as string, StringComparison.Ordinal))
            {
                continue;
            }

            using var shipped = (Stream)entry.Value!;
            var copy = new MemoryStream();
            shipped.CopyTo(copy);
            copy.Position = 0;

            return copy;
        }

        throw new InvalidOperationException($"{Key} is missing from D47.Panel's resources.");
    }

    /// <summary>
    /// The panel executable, beside the test binaries because the test project
    /// references it.
    /// </summary>
    private static string Executable()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "D47.Panel.exe");

        File.Exists(path).ShouldBeTrue(
            customMessage: $"{path} should have been copied next to the tests");

        return path;
    }

    /// <summary>
    /// Reads an icon straight out of a file's resources at a given size — the
    /// same way the shell does, and unlike <c>ExtractAssociatedIcon</c> it
    /// reports nothing rather than substituting a default when there is no icon.
    /// </summary>
    private static int CountIconsIn(string path, int size) =>
        (int)PrivateExtractIcons(path, 0, size, size, null, null, 0, 0);

    private static Icon ExtractFrom(string path, int size)
    {
        IntPtr[] handles = new IntPtr[1];
        int[] identifiers = new int[1];

        uint found = PrivateExtractIcons(path, 0, size, size, handles, identifiers, 1, 0);

        if (found == 0 || handles[0] == IntPtr.Zero)
        {
            throw new ShouldAssertException(
                $"{Path.GetFileName(path)} carries no icon at {size}x{size}. "
                + "ApplicationIcon in the project file is what puts one there.");
        }

        try
        {
            // Cloned, so the icon owns memory of its own and the handle below can
            // be destroyed rather than leaked.
            using var borrowed = Icon.FromHandle(handles[0]);

            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handles[0]);
        }
    }

    // System32 rather than the default probing order, matching Desktop.cs:
    // user32 is an operating system library, and naming where it comes from is
    // what stops a DLL of the same name beside the test binaries being loaded.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr[]? phicon,
        int[]? piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
