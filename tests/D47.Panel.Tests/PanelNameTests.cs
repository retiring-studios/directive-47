using System.Collections.Generic;

using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What the panel calls its parts to anything listening.
///
/// <para>
/// The render names itself, and <c>CapabilityViewTests</c> asserts that. This
/// asks the question again through the window that hosts it, because the render
/// having a name is not the same claim as the panel not announcing something
/// else: the answer's <c>ToString()</c> was reaching the tree from the scrolling
/// region around the render, which no test of the render alone can see.
/// </para>
///
/// <para>
/// In-process and off the desktop, like <c>PanelSizeTests</c>. Peers answer off
/// a tree that has been laid out, so nothing here needs the panel to be the
/// window somebody is looking at.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelNameTests
{
    [Fact]
    public void CapabilityAnswer_InTheAutomationTree_CarriesItsNameRatherThanItsToString()
    {
        Answer answer = Fixtures.HelpsAnswer();

        IReadOnlyList<string> names = OpenedPanel.Opened(
            (panel, _) => AutomationTree.NamesIn(panel));

        // The render's own name has to survive being put in the window, and
        // nothing in that window may read the answer object out loud. Either
        // half passes on its own while the defect stands.
        names.ShouldContain("Capability answer");
        names.ShouldNotContain(answer.ToString());
    }
}
