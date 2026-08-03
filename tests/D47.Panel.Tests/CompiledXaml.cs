namespace D47.Panel.Tests;

/// <summary>
/// The compiled XAML behind the render, which one thread may read at a time and
/// no more.
/// </summary>
internal static class CompiledXaml
{
    /// <summary>
    /// The collection name shared by every test class that builds a
    /// <c>CapabilityView</c>.
    ///
    /// <para>
    /// xUnit runs test classes in parallel, so without this two threads call
    /// <c>Application.LoadComponent</c> for the same compiled XAML at once — and
    /// the part of WPF that reads it keeps one list of open streams per resource
    /// with no lock around it. It fails as an index out of range inside
    /// <c>PackagePart</c>, nowhere near anything this project wrote. It is the
    /// same hazard <c>D47.Render.Tests</c> holds a lock against, found here the
    /// moment a second class in this assembly started building a render; before
    /// that there was only one, and one thread never raced itself.
    /// </para>
    ///
    /// <para>
    /// Deliberately a different name from <c>Desktop.Collection</c> rather than
    /// a reuse of it. These classes touch nothing outside their own thread and
    /// have no business waiting behind a test that is driving the real desktop
    /// for half a minute.
    /// </para>
    /// </summary>
    internal const string Collection = "The render's compiled XAML";
}
