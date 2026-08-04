using D47.Capabilities;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Render.Tests;

/// <summary>
/// That an <see cref="Answer"/> is the value it says it is.
///
/// <para>
/// The fact the whole change was for. <c>Answer</c> is a record holding a
/// descriptor and a result, and both of those held a list — so an answer was a
/// value at the top and a reference two levels down, which is the worst of the
/// two to reason about because the declaration says otherwise.
/// </para>
/// </summary>
public class AnswerTests
{
    [Fact]
    public void TwoAnswersWithTheSameContent_AreEqual()
    {
        // Built separately, from the same capability and the same registry.
        // Before this they were unequal, so anything asking "is this the same
        // answer as last time?" was told no on every single turn.
        Fixtures.HelpsAnswer().ShouldBe(Fixtures.HelpsAnswer());
    }

    [Fact]
    public void TwoAnswersWithTheSameContent_HashTheSame()
    {
        Fixtures.HelpsAnswer().GetHashCode().ShouldBe(Fixtures.HelpsAnswer().GetHashCode());
    }

    [Fact]
    public void TwoAnswersWithDifferentResults_AreNotEqual()
    {
        // The direction that keeps the fix honest. An equality that says yes to
        // everything would satisfy the two above and be worse than what it
        // replaced.
        Answer help = Fixtures.HelpsAnswer();

        Answer altered = help with
        {
            Result = new ListResult
            {
                CapabilityId = help.Descriptor.Id,
                Items = ["something the capability never said"],
            },
        };

        help.ShouldNotBe(altered);
    }
}
