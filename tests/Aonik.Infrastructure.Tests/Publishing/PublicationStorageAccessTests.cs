using Aonik.Infrastructure.Publishing;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Publishing;

public class PublicationStorageAccessTests
{
    private const string Root = "/mnt/aonik-publishing-private";
    private const string Account = "ankpubtest123";
    private const string Mount = "45 30 0:80 / /mnt/aonik-publishing-private rw,relatime - cifs //ankpubtest123.file.core.windows.net/intake rw,vers=3.1.1";

    [Fact]
    public void AccessControl_Should_DefaultToLocalPermissions()
    {
        PublicationStorageAccess.UsesLocalPermissions(Root, new PublishingStorageOptions(), "intake").Should().BeTrue();
    }

    [Fact]
    public void Mount_Should_RequireTheExactAuthenticatedShare()
    {
        Action verify = () => PublicationStorageAccess.VerifyAzureFilesMount(Root, Account, "intake", [Mount]);
        verify.Should().NotThrow();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("account")]
    [InlineData("share")]
    [InlineData("local")]
    [InlineData("nested")]
    [InlineData("read-only")]
    [InlineData("read-only-superblock")]
    [InlineData("other-path")]
    [InlineData("malformed")]
    public void Mount_Should_RefuseSubstitutionOrUnqualifiedStorage(string fault)
    {
        var mount = fault switch
        {
            "missing" => "",
            "account" => Mount.Replace(Account, "otheraccount"),
            "share" => Mount.Replace(".net/intake", ".net/delivery"),
            "local" => Mount.Replace("- cifs", "- ext4"),
            "nested" => Mount.Replace("0:80 / ", "0:80 /subdir "),
            "read-only" => Mount.Replace("rw,relatime", "ro,relatime"),
            "read-only-superblock" => Mount.Replace("rw,vers", "ro,vers"),
            "other-path" => Mount.Replace(Root, Root + "-other"),
            _ => "45 30 - cifs"
        };
        Action verify = () => PublicationStorageAccess.VerifyAzureFilesMount(Root, Account, "intake", [mount]);
        verify.Should().Throw<PublishingException>().Which.Code.Should().Be("publishing-storage-access-unqualified");
    }

    [Fact]
    public void Mount_Should_DecodeEscapedPathsWithoutAcceptingParentMounts()
    {
        Action verify = () => PublicationStorageAccess.VerifyAzureFilesMount("/private files", Account, "intake", [Mount.Replace(Root, @"/private\040files")]);
        verify.Should().NotThrow();
        Action nested = () => PublicationStorageAccess.VerifyAzureFilesMount(Root + "/nested", Account, "intake", [Mount]);
        nested.Should().Throw<PublishingException>();
    }

    [Fact]
    public void AccessControl_Should_RefuseUnknownModes()
    {
        Action verify = () => PublicationStorageAccess.UsesLocalPermissions(Root, new PublishingStorageOptions { AccessControl = "IgnoreErrors" }, "intake");
        verify.Should().Throw<PublishingException>();
    }
}
