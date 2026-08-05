namespace D47.TestSupport;

/// <summary>
/// The compiled XAML behind the render, which one thread may read at a time and
/// no more.
/// </summary>
public static class CompiledXaml
{
    /// <summary>
    /// The collection name shared by every test class that builds a
    /// <c>CapabilityView</c>, whether it does so directly, through the panel
    /// window that hosts one, or through the headset render that draws one to a
    /// texture.
    ///
    /// <para>
    /// xUnit runs test classes in parallel, so without this two threads call
    /// <c>Application.LoadComponent</c> for the same compiled XAML at once —
    /// and the part of WPF that reads it keeps one list of open streams per
    /// resource with no lock around it. It fails as an index out of range
    /// inside <c>PackagePart</c>, nowhere near anything this project wrote, and
    /// only sometimes: two classes built views and it stayed hidden, and the
    /// third made it show up about one run in three.
    /// </para>
    ///
    /// <para>
    /// Here rather than beside any one of them because a collection binds
    /// inside an assembly, and consolidating the test projects by tier is what
    /// first put all three sets of callers into one. Before that they were
    /// three processes and could not race each other; the guard each of them
    /// had was correct and had nothing to say about the other two.
    /// </para>
    ///
    /// <para>
    /// Deliberately a different name from the desktop collection rather than a
    /// reuse of it. These classes touch nothing outside their own thread and
    /// have no business waiting behind a test that is driving the real desktop
    /// for half a minute.
    /// </para>
    /// </summary>
    public const string Collection = "The render's compiled XAML";
}
