using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

public sealed class ImmutablePublicationArtifactStore(IOptions<PublishingOptions> options,IHostEnvironment environment,
    IConfiguration configuration,IHttpClientFactory clients) : IPublicationArtifactStore
{
    private static readonly SemaphoreSlim Writes=new(4);
    public string LocationId
    {
        get
        {
            var root=Root();var marker=Path.Combine(root,".store-id");RejectLinks(marker);
            if(!File.Exists(marker))
            {
                var temp=marker+"."+Guid.NewGuid().ToString("N");
                try
                {
                    using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {file.Write(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")));file.Flush(true);}
                    try{File.Move(temp,marker,false);}catch(IOException)when(File.Exists(marker)){}
                }
                finally{if(File.Exists(temp))File.Delete(temp);}
            }
            var identity=File.ReadAllText(marker);
            if(!System.Text.RegularExpressions.Regex.IsMatch(identity,"^[a-f0-9]{32}$"))throw Unavailable();
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes((OperatingSystem.IsWindows()?root.ToUpperInvariant():root)+"\n"+identity)));
        }
    }
    public async Task WriteAsync(PublicationArtifact artifact,Stream source,CancellationToken cancellationToken=default)
    {
        var destination=ObjectPath(artifact);await Writes.WaitAsync(cancellationToken);
        var temporary=destination+".upload-"+Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);RejectLinks(destination);
            await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.Asynchronous))
            {await PublicationBytes.CopyAsync(source,output,artifact.Bytes,artifact.Sha256,cancellationToken);await output.FlushAsync(cancellationToken);output.Flush(true);}
            try{File.Move(temporary,destination,false);}
            catch(IOException)when(File.Exists(destination))
            {await using var existing=new FileStream(destination,FileMode.Open,FileAccess.Read,FileShare.Read);await PublicationBytes.CopyAsync(existing,Stream.Null,artifact.Bytes,artifact.Sha256,cancellationToken);}
        }
        finally{try{if(File.Exists(temporary))File.Delete(temporary);}finally{Writes.Release();}}
    }
    public Task<Stream> OpenAsync(PublicationArtifact artifact,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();var path=ObjectPath(artifact);RejectLinks(path);
        if(!File.Exists(path)||new FileInfo(path).Length!=artifact.Bytes)throw new IOException("Retained public object unavailable.");
        return Task.FromResult<Stream>(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.Asynchronous));
    }
    public async Task VerifyPublicAsync(PublicationArtifact artifact,CancellationToken cancellationToken=default)
    {
        if(!options.Value.Channels.TryGetValue(artifact.ChannelKey,out var channel)||channel.TenantId!=artifact.TenantId
            ||artifact.Url!=PublicationBytes.Url(channel,artifact.ChannelKey,artifact.Id))throw Unavailable();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromSeconds(90));
        using var response=await clients.CreateClient("Publishing.PublicReadback").GetAsync(artifact.Url,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(response.StatusCode!=System.Net.HttpStatusCode.OK||response.Content.Headers.ContentLength!=artifact.Bytes
            ||response.Content.Headers.ContentType?.MediaType!=artifact.MediaType)throw new IOException("Public asset readback did not match its immutable descriptor.");
        await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
        await PublicationBytes.CopyAsync(stream,Stream.Null,artifact.Bytes,artifact.Sha256,timeout.Token);
    }
    private string ObjectPath(PublicationArtifact artifact)
    {
        if(artifact.TenantId==Guid.Empty||artifact.Id==Guid.Empty||artifact.OperationId==Guid.Empty||artifact.StorageLocationId!=LocationId
            ||artifact.MediaType is not ("application/zip" or "image/png" or "image/jpeg"))throw Unavailable();
        var root=Root();var path=Path.GetFullPath(Path.Combine(root,artifact.TenantId.ToString("N"),artifact.OperationId.ToString("N"),artifact.Id.ToString("N")));
        if(!Inside(path,root))throw Unavailable();RejectLinks(path);return path;
    }
    private string Root()
    {
        var configured=options.Value.Delivery.PublicRoot;
        if(string.IsNullOrWhiteSpace(configured)||!Path.IsPathFullyQualified(configured))throw Unavailable();
        var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        var forbidden=new List<string>{Path.Combine(environment.ContentRootPath,"wwwroot"),Path.Combine(environment.ContentRootPath,"storage"),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath,configuration["BlobStorage:LocalBasePath"]??"storage")),
            Path.GetFullPath(configuration["BlobStorage:LocalBasePath"]??"storage")};
        if(!string.IsNullOrWhiteSpace(options.Value.Intake.PrivateRoot))forbidden.Add(options.Value.Intake.PrivateRoot);
        foreach(var other in forbidden){var path=Path.GetFullPath(other);if(Inside(root,path)||Inside(path,root))throw Unavailable();}
        RejectLinks(root);
        var localPermissions=PublicationStorageAccess.UsesLocalPermissions(root,options.Value.Storage,"delivery");
        Directory.CreateDirectory(root);
        if(localPermissions&&!OperatingSystem.IsWindows())File.SetUnixFileMode(root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        return root;
    }
    private static bool Inside(string path,string root)=>path.Equals(root,StringComparison.OrdinalIgnoreCase)||path.StartsWith(Path.TrimEndingDirectorySeparator(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    private static void RejectLinks(string path)
    {
        for(var current=new DirectoryInfo(path);current is not null;current=current.Parent)if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw Unavailable();
        if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw Unavailable();
    }
    private static PublishingException Unavailable()=>new(503,"public-store-unavailable","Restore the qualified immutable publication store outside private and static-file mounts.");
}
