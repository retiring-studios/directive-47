using System.IO;

using D47.Data;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Data;

/// <summary>
/// A key the Commander hands over, and what is on disk afterwards.
///
/// <para>
/// The claim is about the file rather than about the API, so these read the
/// file. A round trip through <c>WriteSecret</c> and <c>ReadSecret</c> would
/// pass just as happily against a store that wrote the key in plain text, which
/// is the failure this exists to catch.
/// </para>
///
/// <para>
/// Tier 1 and runs in CI, because DPAPI needs Windows and nothing else — no
/// microphone, no headset, no game — and CI is Windows.
/// </para>
/// </summary>
public class SecretTests
{
    private const string AKey = "sk-000011112222333344445555666677778888";

    [Fact]
    public void Secret_WhenWritten_IsNowhereInTheFileInTheClear()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().WriteSecret("model", AKey);

        // The whole file, not the value under that name. A store that encrypted
        // the value and then wrote the plain one beside it under some other
        // name would pass a narrower assertion, and the thing being defended
        // against is a file pasted into a bug report.
        File.ReadAllText(temporary.File).ShouldNotContain(
            AKey, customMessage: "the key is in the file in the clear");
    }

    [Fact]
    public void Secret_WhenWritten_ReadsBackForTheSameCommander()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().WriteSecret("model", AKey);

        // A second open is the next run, the same way the store's own tests ask
        // it. Asking the same instance would prove it can hold a string.
        temporary.Open().ReadSecret("model").ShouldBe(AKey);
    }

    [Fact]
    public void Secret_ThatWasNeverWritten_IsNothingAndSaysNothing()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().ReadSecret("model").ShouldBeNull(
            "a Commander who has never been asked for a key has not given one");

        temporary.Recorded.ShouldBeEmpty(
            "a first run is not an event, and recording one would make every "
            + "first run look like a fault");
    }

    [Fact]
    public void Secret_ThatCannotBeDecrypted_IsNothingAndLeavesARecord()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().WriteSecret("model", AKey);

        // What a key looks like after it has travelled — copied to another
        // machine, restored from a backup, or read under another Windows
        // account. Windows will not unscramble it and there is nothing wrong
        // with the file.
        temporary.Corrupt("""{ "model": "bm90IGEgcHJvdGVjdGVkIGJsb2I=" }""");

        DataStore store = temporary.Open();

        // Absent rather than a crash, because the answer the caller needs is
        // "ask for it again" and an exception here would reach a Commander as a
        // dialog about cryptography.
        store.ReadSecret("model").ShouldBeNull(
            "a key that cannot be decrypted is a key we do not have");

        temporary.Recorded.ShouldHaveSingleItem().ShouldContain(
            "model",
            customMessage:
                "the record has to say which key, or it cannot be acted on");
    }
}
