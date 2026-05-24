using FluentAssertions;

namespace Aonik.PersonalFinance.Tests;

/// <summary>
/// Placeholder smoke test — its job is to give the test runner
/// something to discover so CI doesn't treat Aonik.PersonalFinance.Tests
/// as "no tests found". Delete this file when the first real
/// PersonalFinance test lands.
///
/// Note: the project intentionally takes a ProjectReference on
/// Aonik.PersonalFinance so the build catches contract drift the
/// moment a contributor adds a real test.
/// </summary>
public class PersonalFinanceProjectSmokeTests
{
    [Fact]
    public void TestProject_LoadsSuccessfully()
    {
        // If this test fails the test project itself is broken.
        // First real PersonalFinance test should replace this file.
        true.Should().BeTrue();
    }
}
