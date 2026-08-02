using System;
using System.Windows;

using D47.Render;

namespace D47.GameOverlay;

/// <summary>
/// The overlay drawn over Elite: a transparent, always-on-top window whose
/// whole content is the same render the panel shows.
///
/// <para>
/// It instantiates <see cref="CapabilityView"/> rather than sharing the panel's
/// instance, because WPF cannot put one element in two windows. "The same
/// render" is one definition and one answer, not one object.
/// </para>
/// </summary>
public partial class GameOverlayWindow : Window
{
    /// <summary>
    /// Creates the overlay around an answer to show.
    /// </summary>
    /// <param name="answer">What to render.</param>
    public GameOverlayWindow(Answer answer)
    {
        InitializeComponent();
        View.DataContext = answer;
    }

    /// <summary>
    /// Shows the overlay above the game.
    /// </summary>
    /// <param name="game">The running game to draw over.</param>
    public void ShowOver(EliteWindow game) => throw new NotImplementedException();
}
