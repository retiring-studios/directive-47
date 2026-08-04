using System;

using Velopack;

namespace D47.Panel;

/// <summary>
/// Where Directive 47 starts, before there is an application.
/// </summary>
///
/// <remarks>
/// <para>
/// WPF writes an entry point of its own out of <c>App.xaml</c>, and this
/// replaces it — <c>StartupObject</c> in the project file is what points at this
/// one, and dropping that line silently hands the launch back to the generated
/// one. <c>EntryPointTests</c> is what notices.
/// </para>
/// <para>
/// It exists for one line. The installer relaunches this executable to do its
/// own work — first run, shortcuts, uninstall — passing what it wants on the
/// command line, and <c>VelopackApp.Run</c> is what reads those, does the work
/// and exits the process without ever returning. Done from inside
/// <c>App.OnStartup</c> instead, the application object already exists and its
/// message loop has already begun, which is what <c>vpk</c> warns about while
/// packing. It warns rather than fails, so nothing but this file and its test
/// keeps it right.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Runs the updater's own errands if this launch is one of them, and
    /// otherwise starts the application.
    /// </summary>
    ///
    /// <remarks>
    /// Single-threaded apartment, which WPF's generated entry point also
    /// declared and which is not optional: WPF, the notification-area icon and
    /// every visual tree built here all require it.
    /// </remarks>
    [STAThread]
    internal static void Main()
    {
        // Nothing above this line, ever. Whatever is added to startup, it is
        // added below — an installer's errand that has to get past our code
        // first is an errand that can be broken by our code.
        VelopackApp.Build().Run();

        // Disposed here rather than left to the process ending, because App owns
        // the notification-area icon and a tray icon outliving its application
        // is the ghost that stays in the tray until somebody waves at it. WPF's
        // generated entry point never had to think about this; it never handed
        // the object to anyone.
        using var application = new App();

        application.InitializeComponent();
        application.Run();
    }
}
