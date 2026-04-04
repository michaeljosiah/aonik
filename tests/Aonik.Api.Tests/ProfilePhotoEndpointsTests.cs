using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;

namespace Aonik.Api.Tests;

public class ProfilePhotoEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProfilePhotoEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadCustomerPhoto_WithPersonalUserAndPermission_ReturnsOk()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithRoles("PersonalUser")
                .WithPermissions("UserInfo.Update"));

        using var imageContent = new ByteArrayContent(CreateTinyGif());
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/gif");

        using var form = new MultipartFormDataContent();
        form.Add(imageContent, "photo", "avatar.gif");

        // Act
        var response = await client.PostAsync("/profiles/customers/me/photo", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("photoUrl");
    }

    private static byte[] CreateTinyGif()
    {
        return Convert.FromBase64String(
            "R0lGODlhAQABAIABAP///wAAACwAAAAAAQABAAACAkQBADs=");
    }
}
