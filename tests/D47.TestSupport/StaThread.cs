using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace D47.TestSupport;

/// <summary>
/// Runs something on a thread WPF will talk to.
///
/// <para>
/// WPF will not build a visual tree on a thread that is not single-threaded
/// apartment, and the test runner's threads are not. Every test that inspects a
/// real control needs this first, which is why it was written twice before it
/// was written here.
/// </para>
/// </summary>
public static class StaThread
{
    /// <summary>
    /// Runs something on an STA thread and hands back its answer.
    /// </summary>
    ///
    /// <remarks>
    /// The failure is captured and rethrown on the calling thread rather than
    /// allowed to escape the new one, because an exception leaving a thread
    /// nobody is joined to tears the process down instead of failing a test.
    /// </remarks>
    /// <typeparam name="T">What it works out.</typeparam>
    /// <param name="what">What to run.</param>
    /// <returns>Whatever it worked out.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="what"/> is null.</exception>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "Marshalling a failure back to the calling thread is the whole job. "
            + "Rethrowing inside the catch would tear down the process instead of "
            + "failing the test, and narrowing the type would swallow whatever it "
            + "did not name.")]
    public static T Run<T>(Func<T> what)
    {
        ArgumentNullException.ThrowIfNull(what);

        T answer = default!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                answer = what();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return answer;
    }
}
