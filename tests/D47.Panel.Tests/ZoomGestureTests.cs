using System.Windows.Input;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// Which gesture asks the zoom for what.
///
/// <para>
/// A mapping rather than a handler, so it can be asserted without a window and
/// without synthesizing real input. What is left in <c>MainWindow</c> is two
/// event handlers that pass what they were given to this and do as they are
/// told, which is the thinnest an adapter gets.
/// </para>
/// </summary>
public class ZoomGestureTests
{
    [Theory]
    [InlineData(Key.OemPlus)]
    [InlineData(Key.Add)]
    public void Gesture_ForControlAndPlus_AsksForBigger(Key pressed) =>
        ZoomGestures.For(pressed, ModifierKeys.Control).ShouldBe(ZoomGesture.Bigger);

    [Theory]
    [InlineData(Key.OemMinus)]
    [InlineData(Key.Subtract)]
    public void Gesture_ForControlAndMinus_AsksForSmaller(Key pressed) =>
        ZoomGestures.For(pressed, ModifierKeys.Control).ShouldBe(ZoomGesture.Smaller);

    [Theory]
    [InlineData(Key.D0)]
    [InlineData(Key.NumPad0)]
    public void Gesture_ForControlAndZero_AsksForLifeSize(Key pressed) =>
        ZoomGestures.For(pressed, ModifierKeys.Control).ShouldBe(ZoomGesture.LifeSize);

    [Fact]
    public void Gesture_ForThePlusKeyAlone_AsksForNothing()
    {
        // Without Control these are ordinary typing. A panel that zoomed on a
        // bare minus would be a panel nobody could type into the moment there is
        // anything to type into.
        ZoomGestures.For(Key.OemPlus, ModifierKeys.None).ShouldBe(ZoomGesture.Nothing);
        ZoomGestures.For(Key.D0, ModifierKeys.Shift).ShouldBe(ZoomGesture.Nothing);
    }

    [Fact]
    public void Gesture_ForControlAndTheWheel_AsksInTheDirectionItTurned()
    {
        ZoomGestures.For(120, ModifierKeys.Control).ShouldBe(ZoomGesture.Bigger);
        ZoomGestures.For(-120, ModifierKeys.Control).ShouldBe(ZoomGesture.Smaller);
    }

    [Fact]
    public void Gesture_ForTheWheelAlone_AsksForNothing()
    {
        // The wheel on its own scrolls, which is the whole reason there is
        // something to scroll. Zooming on a bare wheel would take that away and
        // surprise everyone who has ever used a window.
        ZoomGestures.For(120, ModifierKeys.None).ShouldBe(ZoomGesture.Nothing);
        ZoomGestures.For(-120, ModifierKeys.Shift).ShouldBe(ZoomGesture.Nothing);
    }

    [Fact]
    public void Gesture_ForAWheelThatDidNotTurn_AsksForNothing() =>
        ZoomGestures.For(0, ModifierKeys.Control).ShouldBe(ZoomGesture.Nothing);

    [Fact]
    public void Gesture_ForControlAndSomethingElse_AsksForNothing() =>
        ZoomGestures.For(Key.P, ModifierKeys.Control).ShouldBe(ZoomGesture.Nothing);
}
