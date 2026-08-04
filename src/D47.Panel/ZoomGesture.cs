using System.Windows.Input;

namespace D47.Panel;

/// <summary>
/// What the Commander just asked the zoom for, and which gestures ask it.
///
/// <para>
/// The gestures are a browser's, deliberately and without invention. Nobody
/// should have to learn how to make the text bigger in this panel, and every
/// alternative is a thing to remember in a cockpit where the hands are busy.
/// </para>
///
/// <para>
/// A mapping rather than a pair of event handlers, so which gesture means what
/// is assertable in CI. What is left in <c>MainWindow</c> passes on what it was
/// given and does as it is told.
/// </para>
/// </summary>
internal enum ZoomGesture
{
    /// <summary>
    /// Not a zoom gesture. Whatever was pressed or turned belongs to whoever
    /// would have had it anyway.
    /// </summary>
    Nothing,

    /// <summary>
    /// One level in.
    /// </summary>
    Bigger,

    /// <summary>
    /// One level out.
    /// </summary>
    Smaller,

    /// <summary>
    /// Straight back to 100%.
    /// </summary>
    LifeSize,
}

/// <summary>
/// Reads a gesture out of what the keyboard or the wheel just did.
/// </summary>
internal static class ZoomGestures
{
    /// <summary>
    /// What a keypress asks for.
    /// </summary>
    ///
    /// <remarks>
    /// The number row and the numeric keypad both, because they are the same
    /// key to the person pressing them and different keys to Windows. Without
    /// Control every one of these is ordinary typing and is left alone.
    /// </remarks>
    /// <param name="pressed">Which key.</param>
    /// <param name="held">What was held down with it.</param>
    /// <returns>What it asks the zoom for.</returns>
    internal static ZoomGesture For(Key pressed, ModifierKeys held)
    {
        if (held != ModifierKeys.Control)
        {
            return ZoomGesture.Nothing;
        }

        return pressed switch
        {
            Key.OemPlus or Key.Add => ZoomGesture.Bigger,
            Key.OemMinus or Key.Subtract => ZoomGesture.Smaller,
            Key.D0 or Key.NumPad0 => ZoomGesture.LifeSize,
            _ => ZoomGesture.Nothing,
        };
    }

    /// <summary>
    /// What a turn of the wheel asks for.
    /// </summary>
    ///
    /// <remarks>
    /// Only with Control. The wheel on its own scrolls, which is the whole
    /// reason there is something to scroll — taking that away would surprise
    /// everyone who has ever used a window, to save one modifier key.
    /// </remarks>
    /// <param name="turned">How far it turned, and which way.</param>
    /// <param name="held">What was held down with it.</param>
    /// <returns>What it asks the zoom for.</returns>
    internal static ZoomGesture For(int turned, ModifierKeys held)
    {
        if (held != ModifierKeys.Control || turned == 0)
        {
            return ZoomGesture.Nothing;
        }

        return turned > 0 ? ZoomGesture.Bigger : ZoomGesture.Smaller;
    }
}
