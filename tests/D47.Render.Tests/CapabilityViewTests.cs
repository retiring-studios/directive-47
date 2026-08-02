using System.Collections.Generic;

using D47.Capabilities;
using D47.Help;

using Shouldly;

using Xunit;

namespace D47.Render.Tests;

/// <summary>
/// The render turns a capability's answer into text on a surface from its
/// descriptor and its result, and knows nothing else about it. This is the
/// first thing to consume the descriptor contract as a surface rather than as
/// a test fixture.
///
/// <para>
/// The facts still say <c>Panel_</c> because they are #71's acceptance
/// criteria, and a criterion that has been ticked off is a record. They moved
/// here with the render and are otherwise unchanged; the panel is still one of
/// the surfaces they are true of.
/// </para>
/// </summary>
public class CapabilityViewTests
{
    [Fact]
    public void Panel_ForACapabilityAnswer_RendersItFromTheDescriptorAlone()
    {
        var answer = new Answer
        {
            Descriptor = Descriptor("fuel"),
            Result = new ListResult
            {
                CapabilityId = "fuel",
                Items = ["Main tank: 24 tonnes.", "Reserve: 0.6 tonnes."],
            },
        };

        Rendering.LinesFor(answer).ShouldBe(
        [
            "Main tank: 24 tonnes.",
            "Reserve: 0.6 tonnes.",
        ]);
    }

    [Fact]
    public void Panel_ForAListDisplay_RendersTheItemsInTheOrderTheyWouldBeSpoken()
    {
        // Order is part of the contract: it is the order a spoken answer reads
        // in, and a surface that reorders it is answering a different question.
        var answer = new Answer
        {
            Descriptor = Descriptor("groups"),
            Result = new ListResult
            {
                CapabilityId = "groups",
                Items = ["Getting around", "Your ship", "Where you are"],
            },
        };

        Rendering.LinesFor(answer).ShouldBe(
        [
            "Getting around",
            "Your ship",
            "Where you are",
        ]);
    }

    [Fact]
    public void Panel_ForANewlyRegisteredCapability_RendersItWithNoEditToThePanel()
    {
        // Nothing in the panel names a capability. This one did not exist when
        // the view was written, and it needs no edit to appear.
        var answer = new Answer
        {
            Descriptor = new CapabilityDescriptor
            {
                Id = "neutron",
                Group = "Getting around",
                HelpText = "Plots a neutron-boosted route.",
                Display = new ListDisplay(),
                Examples = ["plot me a neutron route"],
            },
            Result = new ListResult
            {
                CapabilityId = "neutron",
                Items = ["47 jumps.", "First boost at Jackson's Lighthouse."],
            },
        };

        Rendering.LinesFor(answer).ShouldBe(
        [
            "47 jumps.",
            "First boost at Jackson's Lighthouse.",
        ]);
    }

    [Fact]
    public void Panel_ForHelp_RendersEveryLevelOfItsHierarchy()
    {
        var registry = new CapabilityRegistry(
            HelpCapability.Descriptor,
            Descriptor("fuel", "Your ship"));
        var help = new HelpCapability(registry);

        IReadOnlyList<string> groups = Rendering.LinesFor(Of(help.Answer()));
        IReadOnlyList<string> inGroup = Rendering.LinesFor(Of(help.AnswerForGroup("Your ship")));
        IReadOnlyList<string> detail = Rendering.LinesFor(Of(help.AnswerForCapability("fuel")));

        groups.ShouldBe(["Getting around", "Your ship"]);
        inGroup.ShouldBe(["Does the fuel thing."]);
        detail.ShouldBe(["Does the fuel thing.", "Try saying:", "do the fuel thing"]);
    }

    // There is deliberately no test for a display model the panel has no
    // template for. DisplayModel's constructor is private protected, so the
    // hierarchy is closed outside D47.Capabilities and the case cannot be
    // constructed. What that does not close is a display model being added
    // there and no template being added here — see the notes on the pull
    // request.

    private static CapabilityDescriptor Descriptor(string id, string group = "Getting around") => new()
    {
        Id = id,
        Group = group,
        HelpText = $"Does the {id} thing.",
        Display = new ListDisplay(),
        Examples = [$"do the {id} thing"],
    };

    private static Answer Of(CapabilityResult result) => new()
    {
        Descriptor = HelpCapability.Descriptor,
        Result = result,
    };
}
