using System.Security.Cryptography;

namespace Aonik.Publishing.Services;

public static class PublicationBytes
{
    public static async Task CopyAsync(Stream input,Stream output,long expectedBytes,string sha256,CancellationToken cancellationToken=default)
    {
        if(expectedBytes<1)throw Mismatch();
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long count=0;var buffer=new byte[65536];int read;
        while((read=await input.ReadAsync(buffer,cancellationToken))!=0)
        {
            count=checked(count+read);if(count>expectedBytes)throw Mismatch();hash.AppendData(buffer,0,read);
            await output.WriteAsync(buffer.AsMemory(0,read),cancellationToken);
        }
        if(count!=expectedBytes||Convert.ToHexStringLower(hash.GetHashAndReset())!=sha256)throw Mismatch();
    }
    private static PublishingException Mismatch()=>new(422,"publication-bytes-mismatch","Public assets must match the exact reviewed length and digest.");
    public static string Url(PublishingChannelOptions channel,string key,Guid id)
    {
        if(!PlayerCatalogueProjection.Slug(key)||id==Guid.Empty||!Uri.TryCreate(channel.PublicBaseUrl,UriKind.Absolute,out var uri)
            ||uri.Scheme!="https"||uri.Host==""||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!="")throw new PublishingException(503,"public-url-not-configured","A permanent HTTPS publication origin is required.");
        return channel.PublicBaseUrl.TrimEnd('/')+"/publishing/channels/"+key+"/assets/"+id.ToString("D");
    }
}
