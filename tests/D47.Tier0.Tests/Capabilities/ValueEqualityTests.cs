using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using D47.Capabilities;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Capabilities;

/// <summary>
/// That the contract's records are the values they say they are.
///
/// <para>
/// Every type here is a <c>record</c>, which advertises value semantics — and
/// two of them carry an <see cref="IReadOnlyList{T}"/>, whose default comparer
/// is reference equality. Nothing warns about that; the declaration simply
/// stops being true partway down the type.
/// </para>
/// </summary>
public class ValueEqualityTests
{
    private static CapabilityDescriptor Describing(params string[] examples) =>
        new()
        {
            Id = "help",
            Group = "Getting around",
            HelpText = "Lists what I can do, and narrows down from there.",
            Display = new ListDisplay(),
            Examples = examples,
        };

    private static ListResult Listing(params string[] items) =>
        new() { CapabilityId = "help", Items = items };

    [Fact]
    public void TwoDescriptorsWithTheSameExamples_AreEqual()
    {
        // Separately built, identical content. Before this was fixed the two
        // lists were compared by reference, so this was false for every pair
        // that was not literally the same object.
        Describing("what can you do", "help").ShouldBe(Describing("what can you do", "help"));
    }

    [Fact]
    public void TwoDescriptorsWithDifferentExamples_AreNotEqual()
    {
        // The other direction, and the one a fix could break by comparing
        // nothing at all. Equality that is always true is as wrong as equality
        // that is never true, and quieter.
        Describing("what can you do").ShouldNotBe(Describing("help"));
    }

    [Fact]
    public void TwoDescriptorsWithTheSameExamplesInADifferentOrder_AreNotEqual()
    {
        // Order is content here. The examples are shown in the order they are
        // given, so two orderings are two different things to look at.
        Describing("first", "second").ShouldNotBe(Describing("second", "first"));
    }

    [Fact]
    public void TwoDescriptorsWithTheSameExamples_HashTheSame()
    {
        // Without this the fix is half done: equal values that hash differently
        // are two entries in any dictionary, which is the same defect wearing a
        // different coat.
        Describing("what can you do", "help").GetHashCode()
            .ShouldBe(Describing("what can you do", "help").GetHashCode());
    }

    [Fact]
    public void TwoResultsWithTheSameItems_AreEqual()
    {
        Listing("Getting around").ShouldBe(Listing("Getting around"));
    }

    [Fact]
    public void TwoResultsWithDifferentItems_AreNotEqual()
    {
        Listing("Getting around").ShouldNotBe(Listing("Somewhere else"));
    }

    [Fact]
    public void TwoResultsWithTheSameItems_HashTheSame()
    {
        Listing("Getting around").GetHashCode().ShouldBe(Listing("Getting around").GetHashCode());
    }

    [Fact]
    public void CapabilityDescriptor_HasTheMembersThisFileKnowsAbout()
    {
        // The alarm on a hand-written equality. CapabilityDescriptor.Equals
        // names its members one at a time, so a member added and not added
        // there is ignored — silently, which is precisely the fault the
        // hand-written equality exists to fix, moved rather than removed.
        //
        // Add a property and this fails naming the method to update. That is
        // the whole of its job; it asserts nothing about behaviour.
        Declared(typeof(CapabilityDescriptor)).ShouldBe(
            ["Display", "Examples", "Group", "HelpText", "Id"],
            "a member changed — update CapabilityDescriptor.Equals and GetHashCode to match");
    }

    [Fact]
    public void ListResult_HasTheMembersThisFileKnowsAbout()
    {
        // Only what ListResult declares. Anything added to CapabilityResult
        // itself is covered by the generated base equality this calls into, so
        // the base needs no guard.
        Declared(typeof(ListResult)).ShouldBe(
            ["Items"],
            "a member changed — update ListResult.Equals and GetHashCode to match");
    }

    /// <summary>
    /// The public instance properties a type declares itself, in a stable
    /// order — reflection does not promise one.
    /// </summary>
    /// <param name="type">The type to look at.</param>
    /// <returns>Its own property names, sorted.</returns>
    private static IEnumerable<string> Declared(Type type) =>
        type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

    [Fact]
    public void TwoResultsFromDifferentCapabilities_AreNotEqual()
    {
        // The base record's own members still have to count. A fix that reaches
        // for the items first would call two results equal because they happen
        // to list the same lines.
        ListResult help = Listing("Getting around");
        ListResult somethingElse = help with { CapabilityId = "loadout" };

        help.ShouldNotBe(somethingElse);
    }
}
