using Aonik.SharedKernel.Abstractions;
using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Abstractions;

public class PermissionDeniedExceptionTests
{
    [Fact]
    public void Constructor_Should_SetPermissionKey_And_DefaultMessage_When_OnlyKeyProvided()
    {
        var ex = new PermissionDeniedException("Invoice.Create");

        ex.PermissionKey.Should().Be("Invoice.Create");
        ex.Message.Should().Be("Permission Invoice.Create is required.");
    }

    [Fact]
    public void Constructor_Should_SetPermissionKey_And_CustomMessage_When_BothProvided()
    {
        var ex = new PermissionDeniedException("Invoice.Create", "Authenticated user is required.");

        ex.PermissionKey.Should().Be("Invoice.Create");
        ex.Message.Should().Be("Authenticated user is required.");
    }

    [Fact]
    public void Constructor_Should_PreserveInnerException_When_Provided()
    {
        var inner = new InvalidOperationException("boom");

        var ex = new PermissionDeniedException("Invoice.Create", "wrapping", inner);

        ex.InnerException.Should().BeSameAs(inner);
        ex.PermissionKey.Should().Be("Invoice.Create");
    }

    [Fact]
    public void Constructor_Should_Throw_When_PermissionKeyIsNull()
    {
        var act = () => new PermissionDeniedException(null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("permissionKey");
    }
}
