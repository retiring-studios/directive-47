namespace D47.Render;

/// <summary>
/// Everything a surface shows at once: one capability's answer, and whatever the
/// application needs to say alongside it.
///
/// <para>
/// This is what the render binds to. <see cref="Render.Answer"/> stayed what it
/// was — one capability's result paired with the declaration saying how to show
/// it — because an update notice is not part of anybody's answer and putting it
/// there would make every capability's result carry a field about the updater.
/// </para>
///
/// <para>
/// It exists because the headset is drawn from <see cref="CapabilityView"/>
/// alone. There is no window around that one, so anything a surface could have
/// drawn as chrome reaches the panel and the game overlay and never the surface
/// a Commander is wearing. Putting it in the render is what makes all three say
/// the same thing for the same reason the palette and the typeface do.
/// </para>
/// </summary>
public sealed record Presentation
{
    /// <summary>
    /// What was asked for.
    /// </summary>
    public required Answer Answer { get; init; }

    /// <summary>
    /// The version of Directive 47 waiting to be installed, or
    /// <see langword="null"/> when this copy is current.
    /// </summary>
    ///
    /// <remarks>
    /// The version rather than a sentence. What the notice says is the render's
    /// to word, and a caller handing over finished prose would be a second place
    /// for the panel and the headset to disagree about it.
    /// </remarks>
    public string? UpdateWaiting { get; init; }

    /// <summary>
    /// Whether there is anything to say above the answer.
    /// </summary>
    ///
    /// <remarks>
    /// Here rather than in the XAML because a binding cannot ask whether a string
    /// is null without a converter, and a converter is a class, a resource and a
    /// key to misspell for one question with a one-line answer.
    /// </remarks>
    public bool HasNotice => UpdateWaiting is not null;

    /// <summary>
    /// An answer with nothing to say alongside it, which is every ordinary run.
    /// </summary>
    /// <param name="answer">What was asked for.</param>
    /// <returns>The presentation.</returns>
    public static Presentation Of(Answer answer) => new() { Answer = answer };
}
