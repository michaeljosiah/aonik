using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.Extensions.Configuration;

namespace Aonik.Platform.Endpoints.Cms;

public record GenerateContentImageRequest(string Prompt, string? Alt, int? Width = null, int? Height = null);

internal class GenerateContentImageEndpoint : Endpoint<GenerateContentImageRequest, ContentBlockMediaResponse>
{
    private readonly IContentImageGenerator _imageGenerator;
    private readonly IContentBlockService _contentBlockService;
    private readonly IConfiguration _configuration;

    public GenerateContentImageEndpoint(
        IContentImageGenerator imageGenerator,
        IContentBlockService contentBlockService,
        IConfiguration configuration)
    {
        _imageGenerator = imageGenerator;
        _contentBlockService = contentBlockService;
        _configuration = configuration;
    }

    public override void Configure()
    {
        Post("/cms/content-blocks/{id}/generate-image");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(GenerateContentImageRequest req, CancellationToken ct)
    {
        if (!_imageGenerator.IsAvailable)
        {
            ThrowError("Image generation is not available. Check AI:OpenAI:ApiKey configuration.");
            return;
        }

        var contentBlockId = Route<Guid>("id");

        // Generate image
        var imageBytes = await _imageGenerator.GenerateImageAsync(req.Prompt, req.Width, req.Height, ct);

        // Store to local filesystem
        var localBasePath = _configuration["BlobStorage:LocalBasePath"] ?? "App_Data";
        var contentMediaPath = _configuration["BlobStorage:ContentMedia:Path"] ?? "content-media";
        var fileName = $"{Guid.NewGuid()}.png";
        var relativePath = Path.Combine(contentBlockId.ToString(), fileName);
        var fullDir = Path.Combine(Directory.GetCurrentDirectory(), localBasePath, contentMediaPath, contentBlockId.ToString());
        Directory.CreateDirectory(fullDir);

        var fullPath = Path.Combine(fullDir, fileName);
        await File.WriteAllBytesAsync(fullPath, imageBytes, ct);

        // Build serving URL
        var servingUrl = $"/storage/content-media/{contentBlockId}/{fileName}";

        // Attach as media to the content block
        var mediaRequest = new AddContentBlockMediaRequest(
            Url: servingUrl,
            Alt: req.Alt,
            Caption: null,
            MimeType: "image/png",
            LinkUrl: null);

        var result = await _contentBlockService.AddMediaAsync(contentBlockId, mediaRequest, ct);
        await Send.CreatedAtAsync<AddContentBlockMediaEndpoint>(
            routeValues: new { id = contentBlockId },
            responseBody: result,
            cancellation: ct);
    }
}
