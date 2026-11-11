using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

public sealed class GitHubLegacyCatalogueMirror(IHttpClientFactory clients,IConfiguration configuration) : ILegacyCatalogueMirror
{
    public async Task<LegacyMirrorHead> ReadAsync(PublishingChannelOptions channel,CancellationToken cancellationToken=default)
    {
        if(channel.LegacyMirrorMode == "release")
            return await ReadReleaseAsync(channel, cancellationToken);
        using var request=Request(channel,HttpMethod.Get);using var response=await clients.CreateClient("Publishing.GitHubMirror").SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return new(null,null);
        response.EnsureSuccessStatusCode();await using var input=await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer=new MemoryStream();var block=new byte[65536];int n;
        while((n=await input.ReadAsync(block,cancellationToken))!=0){if(buffer.Length+n>2*1024*1024)throw new IOException("Mirror response exceeds its bound.");await buffer.WriteAsync(block.AsMemory(0,n),cancellationToken);}
        using var json=JsonDocument.Parse(buffer.ToArray());var root=json.RootElement;
        var sha=root.GetProperty("sha").GetString();var bytes=Convert.FromBase64String(root.GetProperty("content").GetString()??"");
        if(!Regex.IsMatch(sha??"","^[a-f0-9]{40,64}$")||bytes.Length>1048576)throw new IOException("Mirror contents identity is invalid.");
        return new(sha,Encoding.UTF8.GetString(bytes));
    }
    public async Task CompareAndSwapAsync(PublishingChannelOptions channel,string? expectedRevision,string json,CancellationToken cancellationToken=default)
    {
        // Release assets have no conditional replacement API. Only the serialized bridge
        // may promote them; this Worker observes its actual anonymous readback.
        if(channel.LegacyMirrorMode == "release")
            throw new PublishingException(409,"mirror-release-pending","The release bridge has not yet confirmed the latest canonical catalogue.");
        if(Encoding.UTF8.GetByteCount(json)>1048576)throw new IOException("Mirror catalogue exceeds its bound.");
        using var request=Request(channel,HttpMethod.Put);
        request.Content=JsonContent.Create(new{message="Update approved Arke catalogue",content=Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),sha=expectedRevision,branch=channel.LegacyMirrorBranch});
        using var response=await clients.CreateClient("Publishing.GitHubMirror").SendAsync(request,cancellationToken);
        if(response.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.UnprocessableEntity)
            throw new PublishingException(409,"mirror-head-conflict","The mirror changed; reconcile the latest canonical head before retrying.");
        response.EnsureSuccessStatusCode();
    }
    private HttpRequestMessage Request(PublishingChannelOptions channel,HttpMethod method)
    {
        var token=configuration["Publishing:LegacyMirrorToken"];
        if(channel.LegacyMirrorMode != "contents"||!channel.LegacyMirrorEnabled||string.IsNullOrWhiteSpace(token)||!Regex.IsMatch(channel.LegacyMirrorRepository,"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
            ||string.IsNullOrWhiteSpace(channel.LegacyMirrorPath)||channel.LegacyMirrorPath.Split('/').Any(s=>s.Length==0||s is "." or "..")
            ||string.IsNullOrWhiteSpace(channel.LegacyMirrorBranch))throw new PublishingException(503,"mirror-not-configured","Qualify the optional mirror repository, branch, path and credential.");
        var path=string.Join('/',channel.LegacyMirrorPath.Split('/').Select(Uri.EscapeDataString));
        var uri="https://api.github.com/repos/"+channel.LegacyMirrorRepository+"/contents/"+path;
        if(method==HttpMethod.Get)uri+="?ref="+Uri.EscapeDataString(channel.LegacyMirrorBranch);
        var request=new HttpRequestMessage(method,uri);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        request.Headers.UserAgent.ParseAdd("Aonik-Publishing/1.0");request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version","2022-11-28");return request;
    }

    private async Task<LegacyMirrorHead> ReadReleaseAsync(PublishingChannelOptions channel, CancellationToken cancellationToken)
    {
        if(!channel.LegacyMirrorEnabled || !Regex.IsMatch(channel.LegacyMirrorRepository,"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
            || !Regex.IsMatch(channel.LegacyMirrorReleaseTag,"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
            || channel.LegacyMirrorPath != "catalogue.json")
            throw new PublishingException(503,"mirror-not-configured","Qualify the public release mirror repository, tag and catalogue.json asset.");

        // Match Player's explicit refresh: GitHub can cache an old filename
        // redirect after promotion even when the new asset is already readable.
        var uri = new Uri($"https://github.com/{channel.LegacyMirrorRepository}/releases/download/{channel.LegacyMirrorReleaseTag}/catalogue.json?_arke_check={Guid.NewGuid():N}");
        for(var redirects = 0; redirects <= 3; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Aonik-Publishing/1.0");
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using var response = await clients.CreateClient("Publishing.GitHubMirror").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                if(location is null) throw new IOException("Mirror redirect is missing its location.");
                var next = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if(next.Scheme != "https" || next.UserInfo.Length != 0 || !next.IsDefaultPort
                    || next.Fragment.Length != 0 || next.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                    throw new IOException("Mirror redirect is outside GitHub release storage.");
                uri = next;
                continue;
            }
            // A missing release asset can be the bridge's interrupted promotion. Preserve
            // pending status, not a successful empty mirror or a request to create a file.
            if(response.StatusCode == System.Net.HttpStatusCode.NotFound) return new(null, null);
            if(response.StatusCode != System.Net.HttpStatusCode.OK || response.Content.Headers.ContentLength > 1048576)
                throw new IOException("Mirror release readback is unavailable or exceeds its bound.");
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var bytes = new MemoryStream();
            var block = new byte[65536];
            int read;
            while((read = await input.ReadAsync(block, cancellationToken)) != 0)
            {
                if(bytes.Length + read > 1048576) throw new IOException("Mirror catalogue exceeds its bound.");
                await bytes.WriteAsync(block.AsMemory(0, read), cancellationToken);
            }
            var content = bytes.ToArray();
            var json = new UTF8Encoding(false, true).GetString(content);
            return new(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)), json);
        }
        throw new IOException("Mirror redirect limit exceeded.");
    }
}
