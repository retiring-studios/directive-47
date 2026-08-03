using Shouldly;

using Valve.VR;

using Xunit;

namespace D47.VrOverlay.Tests;

/// <summary>
/// That the vendored binding is actually wired up: the native library is found
/// and its entry points resolve.
///
/// <para>
/// Nothing here needs a headset or a running SteamVR, which is unusual for this
/// project and is the point — a binding that is not wired up fails the same way
/// on every machine, and finding that out on the one with the headset attached
/// wastes the scarce thing.
/// </para>
/// </summary>
public class BindingTests
{
    [Fact]
    public void Bindings_WithNoSteamVrOnTheMachine_StillResolveTheNativeLibrary()
    {
        // Written because the copy was got wrong once, and nothing said so.
        //
        // A None item preserves its relative path by default, so openvr_api.dll
        // landed in an output vendor\ folder while DllImport probes the
        // application directory. The build was perfectly happy. What it produced
        // would have thrown DllNotFoundException at the first real call, in the
        // headset story, with nothing pointing back at a csproj line.
        //
        // These two are the right pair to ask it with. They are flat C exports
        // rather than anything reached through the interface table, so they need
        // no initialisation, and they are exactly the calls that decide whether
        // to have a headset overlay at all. On a machine with no SteamVR they
        // answer false, which is an answer and not a failure.
        bool installed = OpenVR.IsRuntimeInstalled();
        bool headset = OpenVR.IsHmdPresent();

        // Deliberately no assertion about what they returned. Whether SteamVR is
        // on this machine is not this test's business and asserting either way
        // would make it pass in one place and fail in another. Reaching this
        // line at all is the claim: the library loaded and both entry points
        // resolved.
        _ = installed;
        _ = headset;
    }

    [Fact]
    public void Bindings_ForTheOverlayInterface_AgreeWithTheHeaderTheyWereGeneratedFrom()
    {
        // The interface is a function-pointer table whose layout is positional,
        // so the version string and the struct have to come from the same
        // generation of the file. They do, because both are vendored together
        // and neither is edited - this asserts that nobody has since "helpfully"
        // updated one of them.
        //
        // It is also the thing to look at first when a refresh from upstream
        // goes wrong: a bumped version with a stale struct is silent memory
        // corruption rather than a compile error.
        OpenVR.IVROverlay_Version.ShouldBe("IVROverlay_028");
    }
}
