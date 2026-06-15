using Microsoft.Extensions.FileProviders;

namespace Aonik.Api.Configuration;

/// <summary>
/// Local-disk static-file serving for the development blob-storage
/// fallback. Deployed environments use Azure Blob Storage and skip this
/// entirely.
/// </summary>
/// <remarks>
/// When running locally with <c>BlobStorage:Provider=Local</c>, three
/// kinds of media are served straight from disk so the front-end can
/// render them without an Azure storage round-trip:
/// <list type="bullet">
///   <item><c>/storage/profiles</c> — customer profile photos.</item>
///   <item><c>/storage/attachments</c> — transaction receipts and other
///         user-uploaded attachments.</item>
///   <item><c>/storage/content-media</c> — AI-generated content media
///         (CMS hero images, etc.).</item>
///   <item><c>/storage/documents</c> — generic document files (Spec 035),
///         e.g. CareEntity banner images (Spec 049).</item>
/// </list>
///
/// Each is served with a 1-hour <c>Cache-Control</c> so dev iteration
/// doesn't stall on stale browser caches but also doesn't refetch every
/// frame. Directories are created on demand if missing.
/// </remarks>
public static class DevelopmentStaticFilesConfiguration
{
    private const string LocalBlobProviderKey = "Local";
    private const string DefaultBasePath = "App_Data";
    private const string CacheControlHeader = "public, max-age=3600";

    /// <summary>
    /// Mounts the dev-only local blob mounts when the environment is
    /// Development AND <c>BlobStorage:Provider</c> is set to "Local".
    /// No-op otherwise.
    /// </summary>
    public static IApplicationBuilder UseAonikDevelopmentStaticFiles(
        this IApplicationBuilder app,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var provider = configuration["BlobStorage:Provider"];
        var localBlobsEnabled = environment.IsDevelopment()
            && string.Equals(provider, LocalBlobProviderKey, StringComparison.OrdinalIgnoreCase);

        if (!localBlobsEnabled)
        {
            return app;
        }

        var basePath = configuration["BlobStorage:LocalBasePath"] ?? DefaultBasePath;

        MountStaticFiles(
            app,
            basePath,
            relativePath: configuration["BlobStorage:ProfilePhotos:Path"] ?? "profiles",
            requestPath: "/storage/profiles");

        MountStaticFiles(
            app,
            basePath,
            relativePath: configuration["BlobStorage:Attachments:Path"] ?? "attachments",
            requestPath: "/storage/attachments");

        MountStaticFiles(
            app,
            basePath,
            relativePath: configuration["BlobStorage:ContentMedia:Path"] ?? "content-media",
            requestPath: "/storage/content-media");

        MountStaticFiles(
            app,
            basePath,
            relativePath: configuration["BlobStorage:Documents:Path"] ?? "documents",
            requestPath: "/storage/documents");

        return app;
    }

    private static void MountStaticFiles(
        IApplicationBuilder app,
        string basePath,
        string relativePath,
        string requestPath)
    {
        var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), basePath, relativePath);
        Directory.CreateDirectory(physicalPath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(physicalPath),
            RequestPath = requestPath,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = CacheControlHeader;
            }
        });
    }
}
