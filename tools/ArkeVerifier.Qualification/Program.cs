using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Azure.Identity;

using Aonik.Infrastructure.Publishing;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

if (args.Length != 2) throw new ArgumentException("Provide the dev ACA parameters file and synthetic fixture directory.");
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var settings = config.RootElement.GetProperty("parameters").GetProperty("publishingAppSettings").GetProperty("value");
string Get(string key) => settings.GetProperty("Publishing__Verifier__" + key).GetString()!;
var policy = new PackageVerifierOptions
{
    Provider = Get("Provider"), SessionPoolResourceId = Get("SessionPoolResourceId"), ReaderCommit = Get("ReaderCommit"), Image = Get("Image"),
    MemoryMiB = int.Parse(Get("MemoryMiB")), ScratchMiB = int.Parse(Get("ScratchMiB")), MaxAssetBytes = long.Parse(Get("MaxAssetBytes"))
};
const string devPool = "/subscriptions/336d38e2-bb96-4510-a4c5-5c313139eef9/resourceGroups/rg-aonik-dev-uksouth/providers/Microsoft.App/sessionPools/aonikdevpublishing";
if (policy.SessionPoolResourceId != devPool) throw new InvalidOperationException("This qualification runner only targets the dedicated dev pool.");
using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
var verifier = new SessionPackageVerifier(http, new AzureCliCredential(new AzureCliCredentialOptions { TenantId = "bca913ea-aec0-4528-a999-2f95c9daf398" }));
bool success = true;
foreach (string kind in new[] { "world", "listening", "guide", "music", "story", "comic", "part" })
{
    string path = Path.GetFullPath(Path.Combine(args[1], kind + ".zip"));
    byte[] bytes = await File.ReadAllBytesAsync(path);
    var input = new VerifiedInput(path, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength);
    var result = await verifier.VerifyAsync(input, policy);
    Console.WriteLine(JsonSerializer.Serialize(new { fixture = kind, result.Passed, result.Code, result.ReaderCommit, result.ArchiveSha256, result.Fingerprint }));
    success &= result.Passed;
}
string storyPath = Path.GetFullPath(Path.Combine(args[1], "story.zip"));
byte[] story = await File.ReadAllBytesAsync(storyPath);
var changedIdentity = await verifier.VerifyAsync(new VerifiedInput(storyPath, new string('0', 64), story.Length), policy);
Console.WriteLine(JsonSerializer.Serialize(new { fixture = "changed-archive-identity", changedIdentity.Passed, changedIdentity.Code }));
success &= !changedIdentity.Passed && changedIdentity.Code == "verifier-upload-invalid";
string malformedPath = Path.GetTempFileName();
try
{
    byte[] malformed = Encoding.UTF8.GetBytes("synthetic invalid ZIP; never a creator package");
    await File.WriteAllBytesAsync(malformedPath, malformed);
    var refused = await verifier.VerifyAsync(new VerifiedInput(malformedPath, Convert.ToHexStringLower(SHA256.HashData(malformed)), malformed.Length), policy);
    Console.WriteLine(JsonSerializer.Serialize(new { fixture = "invalid-archive", refused.Passed, refused.Code }));
    success &= !refused.Passed && !refused.Code.StartsWith("verifier-", StringComparison.Ordinal);
}
finally { File.Delete(malformedPath); }
Environment.ExitCode = success ? 0 : 1;
