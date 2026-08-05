using System.Reflection;

using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Where Directive 47 actually starts.
///
/// <para>
/// WPF writes an entry point of its own out of <c>App.xaml</c>, and by the time
/// anything in <c>App</c> runs the application object exists and its message
/// loop has begun. That is too late for the updater: the installer relaunches
/// this executable to do its own work — first run, shortcuts, uninstall — and
/// what handles those has to run and exit before there is an application at all.
/// <c>vpk</c> says so out loud while packing, and says it as a warning, which
/// nothing fails on.
/// </para>
///
/// <para>
/// So the entry point is ours. This is the fact that arrangement rests on, and
/// the one that quietly stops being true if <c>StartupObject</c> is dropped from
/// the project file — at which point WPF's generated <c>Main</c> takes over
/// again, the build still succeeds, and the only symptom is an install that
/// misbehaves on somebody else's machine.
/// </para>
/// </summary>
public class EntryPointTests
{
    [Fact]
    public void Startup_BeforeWpfHasAnApplication_IsSomewhereWeControl()
    {
        MethodInfo? start = typeof(App).Assembly.EntryPoint;

        start.ShouldNotBeNull();

        start.DeclaringType?.FullName.ShouldBe(
            "D47.Panel.Program",
            "the updater has to have the launch before WPF builds an application, and it can "
            + "only do that from an entry point we wrote");
    }
}
