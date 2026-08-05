using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using D47.Capabilities;
using D47.Composition;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Capabilities;

/// <summary>
/// There is one place the real registry is built, and everything that needs it
/// reads that one — the application, the shared test fixture, and parity.
///
/// <para>
/// The registry being built in three places was harmless only while help was
/// the only capability and all three agreed by coincidence. Adding a second one
/// meant editing all three, and nothing failed if you missed one: miss the
/// fixture and the tests keep asserting the old answer, miss parity and the one
/// test whose job is to notice an unwired capability carries on checking a
/// registry that is not the application's.
/// </para>
/// </summary>
public class CompositionTests
{
    [Fact]
    public void CapabilityRegistry_HoweverManyCapabilitiesExist_IsComposedInOnePlace()
    {
        IReadOnlyList<CapabilityDescriptor> declared = DeclaredAnywhere();

        // A discovery-based test that finds nothing passes, and a test that
        // passes because it checked nothing is worse than no test.
        declared.ShouldNotBeEmpty();

        IReadOnlyList<string> unregistered =
        [
            .. from descriptor in declared
               where !Composed.Capabilities.Descriptors.Contains(descriptor)
               select descriptor.Id,
        ];

        unregistered.ShouldBeEmpty();
    }

    [Fact]
    public void TheComposedRegistry_RegistersEachCapability_Once()
    {
        // Composition is hand-written, so the way it goes wrong is a line
        // pasted and not edited. Duplicates would otherwise reach help as a
        // capability listed twice.
        IReadOnlyList<string> ids =
            [.. Composed.Capabilities.Descriptors.Select(descriptor => descriptor.Id)];

        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
    }

    /// <summary>
    /// Every capability declaration the product carries, found rather than
    /// listed — because a list here would be the second place to forget.
    ///
    /// <para>
    /// Scanning what was built beside this test rather than what
    /// <see cref="Composed"/> references, so that a capability project
    /// referenced by the tests and not by the composition is red rather than
    /// invisible. The hole that leaves is a capability project referenced by
    /// nothing at all, which no test in this assembly can see; closing it means
    /// reading <c>src/*.csproj</c>, a claim about the shape of the repository
    /// rather than about the code.
    /// </para>
    /// </summary>
    /// <returns>The declared descriptors, in no particular order.</returns>
    private static IReadOnlyList<CapabilityDescriptor> DeclaredAnywhere()
    {
        // Test assemblies are excluded, and that exclusion is the difference
        // between this failing for a real reason and failing for a fixture. A
        // test is free to declare a descriptor to hand to a registry it built
        // itself — CapabilityRegistryTests does exactly that — and none of
        // those are capabilities the application owes anybody.
        IEnumerable<Assembly> ours =
            from file in Directory.EnumerateFiles(AppContext.BaseDirectory, "D47.*.dll")
            let name = Path.GetFileNameWithoutExtension(file)
            where !name.EndsWith(".Tests", StringComparison.Ordinal)
                  && !name.Equals("D47.TestSupport", StringComparison.Ordinal)
            select Assembly.LoadFrom(file);

        // Deliberately unguarded. A D47 assembly that cannot be loaded or
        // reflected over is a fault worth failing on: swallowing it is how this
        // test would come to find nothing and say so approvingly.
        return
        [
            .. from assembly in ours
               from type in assembly.GetTypes()
               from member in type.GetMembers(BindingFlags.Public | BindingFlags.Static)
               let descriptor = Declared(member)
               where descriptor is not null
               select descriptor,
        ];
    }

    /// <summary>
    /// The descriptor a member declares, if it declares one.
    /// </summary>
    /// <param name="member">The public static member to look at.</param>
    /// <returns>The descriptor, or <see langword="null"/>.</returns>
    private static CapabilityDescriptor? Declared(MemberInfo member) => member switch
    {
        PropertyInfo property when property.PropertyType == typeof(CapabilityDescriptor)
            => property.GetValue(null) as CapabilityDescriptor,
        FieldInfo field when field.FieldType == typeof(CapabilityDescriptor)
            => field.GetValue(null) as CapabilityDescriptor,
        _ => null,
    };
}
