using System;
using System.IO;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// Where Directive 47 keeps what it writes down.
///
/// <para>
/// A decision rather than an implementation detail, and asserted for that
/// reason: the log and the store both need an answer and must not disagree, and
/// the path the log used before this was picked because one line had to go
/// somewhere. A test is what makes changing it a deliberate act rather than an
/// edit nobody notices.
/// </para>
/// </summary>
public class ApplicationDataTests
{
    [Fact]
    public void ApplicationData_LivesUnderTheLocalProfile_InAFolderNamedForTheProduct()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Directive 47");

        ApplicationData.Folder.ShouldBe(
            expected,
            customMessage:
                "local rather than roaming, because everything remembered is machine-shaped "
                + "— a position on a 4K monitor is wrong on a laptop");
    }
}
