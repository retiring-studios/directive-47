namespace D47.Panel.Tests;

/// <summary>
/// The name shared by every test class that drives the real desktop.
///
/// <para>
/// There is one desktop, one notification area, and one overflow flyout, and
/// xUnit runs test classes in parallel. Two classes launching panels at once
/// meant a test could close the other one's window, and a test asking whether
/// the tray icon had gone could be answered by an icon belonging to a panel
/// that was still running. Sharing a collection name is what makes them take
/// turns.
/// </para>
///
/// <para>
/// The in-process tests — the ones that build a visual tree and inspect it —
/// are deliberately not in here. They touch nothing outside their own thread,
/// and slowing them down would buy nothing.
/// </para>
/// </summary>
internal static class Desktop
{
    internal const string Collection = "The desktop";
}
