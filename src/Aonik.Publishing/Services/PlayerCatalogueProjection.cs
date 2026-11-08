using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Services;

internal static class PlayerCatalogueProjection
{
    public const long MaximumSerial=9007199254740991;
    public static string Hash(string json)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    public static JsonObject Read(string json)
    {
        if (Encoding.UTF8.GetByteCount(json)>1048576)throw Invalid("catalogue-size-limit");
        try
        {
            var root=JsonNode.Parse(json) as JsonObject??throw Invalid("catalogue-invalid");
            if (root["version"]?.GetValue<int>()!=2||root["serial"]?.GetValue<long>() is not (>=0 and <=MaximumSerial)
                ||root["worlds"] is not JsonArray worlds)throw Invalid("catalogue-invalid");
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var world in worlds.OfType<JsonObject>())
            {
                var id=world["id"]?.GetValue<string>()??"";
                if(!Slug(id)||!seen.Add(id)||string.IsNullOrWhiteSpace(world["name"]?.GetValue<string>())||world["name"]!.GetValue<string>().Length>200||world["productions"] is not JsonArray productions)
                    throw Invalid("catalogue-identity-invalid");
                ValidatePin(world["package"]);ValidatePin(world["guide"]);ValidatePictures(world);
                var ids=new HashSet<string>(StringComparer.Ordinal);
                foreach(var p in productions)
                {
                    if(p is not JsonObject prod||string.IsNullOrEmpty(prod["title"]?.GetValue<string>())||prod["title"]!.GetValue<string>().Length>200||prod["kind"]?.GetValue<string>() is null)
                        throw Invalid("catalogue-production-invalid");
                    if(prod["id"] is { } pid&&(!Slug(pid.GetValue<string>())||!ids.Add(pid.GetValue<string>())))throw Invalid("catalogue-production-duplicate");
                    foreach(var key in new[]{"package","story","comic"})
                    {
                        ValidatePin(prod[key]);
                        if(prod[key] is JsonObject pin&&(pin["edition"] is null||pin["fingerprint"] is null
                            ||(key is "story" or "comic"&&pin["version"]?.GetValue<int>() is not >0)))throw Invalid("catalogue-capability-invalid");
                    }
                    if((prod["story"] is not null||prod["comic"] is not null)&&prod["package"] is not null)throw Invalid("catalogue-capability-invalid");
                    ValidatePictures(prod);
                }
                if(world["package"]?["parts"] is JsonArray parts)
                    foreach(var part in parts)
                    {
                        ValidatePin(part);
                        if(part is not JsonObject pin||!DateOnly.TryParseExact(pin["released"]?.GetValue<string>(),"yyyy-MM-dd",out _)
                            ||!Slug(pin["production"]?.GetValue<string>()??"")||pin["chapters"] is not JsonArray chapters
                            ||chapters.Any(c=>c?["id"] is null||!Slug(c["id"]!.GetValue<string>())||c["number"]?.GetValue<int>() is not >0||c["title"]?.GetValue<string>() is null))
                            throw Invalid("catalogue-part-invalid");
                    }
            }
            if(seen.Count!=worlds.Count)throw Invalid("catalogue-invalid");
            return root;
        }
        catch(Exception ex) when(ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {throw Invalid("catalogue-invalid");}
    }

    public static JsonObject? World(JsonObject root,string id)=>((JsonArray)root["worlds"]!).OfType<JsonObject>().SingleOrDefault(w=>w["id"]?.GetValue<string>()==id);
    public static string? BaseFingerprint(JsonObject? world)=>world?["package"]?["fingerprint"]?.GetValue<string>();
    public static string? SlotFingerprint(JsonObject? world,string slot)
    {
        var pin=Pin(world,slot);return pin?["fingerprint"]?.GetValue<string>()??pin?["sha256"]?.GetValue<string>();
    }
    public static JsonObject? Pin(JsonObject? world,string slot)
    {
        if(world is null)return null;
        if(slot=="world")return world["package"] as JsonObject;
        if(slot=="guide")return world["guide"] as JsonObject;
        if(slot.StartsWith("part:",StringComparison.Ordinal))
        {var index=int.Parse(slot[5..],System.Globalization.CultureInfo.InvariantCulture)-1;return world["package"]?["parts"] is JsonArray a&&index>=0&&index<a.Count?a[index] as JsonObject:null;}
        var id=slot["production:".Length..];
        var p=(world["productions"] as JsonArray)?.OfType<JsonObject>().SingleOrDefault(p=>p["id"]?.GetValue<string>()==id);
        return (p?["comic"]??p?["story"]??p?["package"]) as JsonObject;
    }
    public static string Slot(PackageVerificationResult package)
    {
        if(package.Kind=="world")return "world";
        if(package.Kind=="guide")return "guide";
        if(package.Kind=="part")return "part:"+package.PartNumber;
        var productions=package.Presentation!.Manifest.GetProperty("productions");
        if(productions.GetArrayLength()!=1)throw Invalid("production-identity-unavailable");
        return "production:"+productions[0].GetProperty("id").GetString();
    }
    public static IReadOnlyList<string> PictureIds(ApprovedPublicationMaterial m)
    {
        var manifest=JsonNode.Parse(m.Package.Presentation!.Manifest.GetRawText())!.AsObject();
        var ids=new HashSet<string>(StringComparer.Ordinal){m.Material.ArtworkAssetId};
        if(manifest["keyArt"]?.GetValue<string>() is { } key)ids.Add(key);
        foreach(var p in manifest["productions"]!.AsArray())foreach(var role in new[]{"picture","tall"})if(p?[role]?.GetValue<string>() is { } id)ids.Add(id);
        return ids.Order(StringComparer.Ordinal).ToArray();
    }

    public static (string Json,string Slot,JsonObject Pin) Apply(JsonObject root,ApprovedPublicationMaterial material,
        IReadOnlyList<PublicationArtifact> artifacts,PublicationOperation operation,long serial)
    {
        var revision=material.Revision;var package=material.Package;var manifest=JsonNode.Parse(package.Presentation!.Manifest.GetRawText())!.AsObject();
        var worlds=root["worlds"]!.AsArray();var existing=World(root,revision.WorldId);
        var world=existing?.DeepClone().AsObject()??new JsonObject{["id"]=revision.WorldId,["name"]=manifest["name"]!.GetValue<string>(),["productions"]=new JsonArray()};
        var archive=artifacts.Single(a=>a.AssetId=="");
        var pin=PinOf(archive);pin["edition"]=package.Edition;pin["fingerprint"]=package.Fingerprint;
        var assetMap=artifacts.Where(a=>a.AssetId!="").ToDictionary(a=>a.AssetId,StringComparer.Ordinal);
        var slot=Slot(package);
        if(package.Kind=="world")
        {
            world["name"]=revision.Title;world["logline"]=revision.Summary;pin["parts"]=new JsonArray();world["package"]=pin.DeepClone();
            var products=new JsonArray();
            foreach(var p in manifest["productions"]!.AsArray().OfType<JsonObject>())products.Add(Production(p,world,assetMap,package,false,pin));
            foreach(var p in world["productions"]!.AsArray().OfType<JsonObject>())
                if((p["package"] is not null||p["story"] is not null||p["comic"] is not null)&&!products.OfType<JsonObject>().Any(n=>n["id"]?.GetValue<string>()==p["id"]?.GetValue<string>()))products.Add(p.DeepClone());
            world["productions"]=products;
        }
        else if(package.Kind=="guide")world["guide"]=pin.DeepClone();
        else if(package.Kind=="part")
        {
            if(world["package"] is not JsonObject basePin||basePin["fingerprint"]?.GetValue<string>()!=package.BaseFingerprint)throw Invalid("publication-lineage-changed");
            var parts=basePin["parts"] as JsonArray??new JsonArray();if(parts.Count+1!=package.PartNumber)throw Invalid("publication-lineage-changed");
            var production=manifest["productions"]![0]!["id"]!.GetValue<string>();
            var details=Details(package,production);
            var chapters=details["chapters"] as JsonArray??new JsonArray();
            pin["released"]=operation.CreatedAt.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            pin["production"]=production;pin["chapters"]=chapters.DeepClone();parts.Add(pin.DeepClone());
            if(basePin["parts"] is null)basePin["parts"]=parts;
            // SPEC-058 R-48/R-53: the verified part declares its production's next release.
            // An omitted optional schedule does not erase the author's previous declaration.
            if(manifest["part"]?["schedule"] is { } schedule)
            {
                var target=world["productions"]!.AsArray().OfType<JsonObject>().Single(p=>p["id"]?.GetValue<string>()==production);
                target["schedule"]=schedule.DeepClone();
            }
        }
        else
        {
            var production=manifest["productions"]![0]!.AsObject();var p=Production(production,world,assetMap,package,true,pin);
            var products=world["productions"]!.AsArray();var old=products.OfType<JsonObject>().SingleOrDefault(x=>x["id"]?.GetValue<string>()==production["id"]?.GetValue<string>());
            if(old is not null)products.Remove(old);products.Add(p);
        }
        var artwork=assetMap[material.Material.ArtworkAssetId];
        var worldPins=world["pins"]?.DeepClone().AsObject()??new JsonObject();worldPins["keyArt"]=PinOf(artwork);world["pins"]=worldPins;
        if(existing is not null)worlds.Remove(existing);worlds.Add(world);
        root["serial"]=serial;
        var json=root.ToJsonString();Read(json);if(pin.ToJsonString().Length>65536)throw Invalid("catalogue-pin-size-limit");return(json,slot,pin);
    }

    private static JsonObject Production(JsonObject source,JsonObject world,Dictionary<string,PublicationArtifact> assets,PackageVerificationResult package,bool alone,JsonObject pin)
    {
        var id=source["id"]!.GetValue<string>();
        var old=world["productions"]!.AsArray().OfType<JsonObject>().SingleOrDefault(p=>p["id"]?.GetValue<string>()==id);
        var result=new JsonObject{["id"]=id,["title"]=source["title"]!.GetValue<string>(),["kind"]=source["comic"] is not null?"Graphic novel":source["story"] is not null?"Interactive story":source["music"] is not null?"Music":source["narration"] is not null||source["listening"] is not null?source["book"] is not null?"Audiobook · Book":"Audiobook":"Book"};
        var pins=new JsonObject();
        foreach(var role in new[]{"picture","tall"})
        {
            if(old?[role] is { } bundled)result[role]=bundled.DeepClone(); // Keep released readers' bundle-path strings.
            if(source[role]?.GetValue<string>() is { } assetId)pins[role]=PinOf(assets[assetId]);
        }
        if(pins.Count>0)result["pins"]=pins;
        if(alone)
        {
            var key=package.Kind is "story" or "comic"?package.Kind:"package";
            var own=pin.DeepClone().AsObject();
            if(key is "story" or "comic")own["version"]=Details(package,id)[key+"Version"]?.GetValue<int>()??throw Invalid("production-capability-unavailable");
            if(source["music"] is not null)result["music"]=Details(package,id)["musicKind"]?.GetValue<string>()??throw Invalid("production-capability-unavailable");
            result[key]=own;
        }
        return result;
    }
    private static JsonObject Details(PackageVerificationResult package,string production)
        =>package.Presentation?.Details is { } details?JsonNode.Parse(details.GetRawText())?[production]?.AsObject()??throw Invalid("production-capability-unavailable"):throw Invalid("production-capability-unavailable");
    public static JsonObject PinOf(PublicationArtifact artifact)=>new(){["url"]=artifact.Url,["byteLength"]=artifact.Bytes,["sha256"]=artifact.Sha256};
    public static string Withdraw(JsonObject root,PublishedEdition edition,long serial)
    {
        var world=World(root,edition.WorldId)??throw Invalid("publication-entry-changed");
        if(SlotFingerprint(world,edition.Slot)!=(edition.Fingerprint??edition.PackageSha256))throw Invalid("publication-entry-changed");
        if(edition.Slot=="world"||edition.Slot.StartsWith("part:",StringComparison.Ordinal))
        {
            world.Remove("package");
            var keep=world["productions"]!.AsArray().OfType<JsonObject>().Where(p=>p["package"] is not null||p["story"] is not null||p["comic"] is not null).Select(p=>(JsonNode?)p.DeepClone()).ToArray();
            world["productions"]=new JsonArray(keep);
        }
        else if(edition.Slot=="guide")world.Remove("guide");
        else
        {
            var id=edition.Slot["production:".Length..];var products=world["productions"]!.AsArray();
            var p=products.OfType<JsonObject>().Single(x=>x["id"]?.GetValue<string>()==id);products.Remove(p);
        }
        if(world["package"] is null&&world["guide"] is null&&world["productions"]!.AsArray().Count==0)
        {
            root["worlds"]!.AsArray().Remove(world);
            if(root["feature"]?["world"]?.GetValue<string>()==edition.WorldId)root.Remove("feature");
            if(root["band"]?.GetValue<string>()==edition.WorldId)root.Remove("band");
        }
        root["serial"]=serial;var json=root.ToJsonString();Read(json);return json;
    }
    private static void ValidatePictures(JsonObject item)
    {
        foreach(var key in new[]{"keyArt","picture","tall"})
        {
            if(item[key] is { } bundled&&bundled.GetValueKind()!=System.Text.Json.JsonValueKind.String)throw Invalid("catalogue-bundled-picture-invalid");
            if(item["pins"]?[key] is { } pin){ValidatePin(pin);if(pin["byteLength"]!.GetValue<long>()>4194304)throw Invalid("catalogue-picture-size-limit");}
        }
    }
    private static void ValidatePin(JsonNode? node)
    {
        if(node is null)return;
        if(node is not JsonObject p||!Uri.TryCreate(p["url"]?.GetValue<string>(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host==""||uri.UserInfo!=""
            ||p["byteLength"]?.GetValue<long>() is not >0||!Regex.IsMatch(p["sha256"]?.GetValue<string>()??"","^[a-f0-9]{64}$"))throw Invalid("catalogue-pin-invalid");
        if(p["fingerprint"] is { } fp&&!Regex.IsMatch(fp.GetValue<string>(),"^[a-f0-9]{64}$"))throw Invalid("catalogue-pin-invalid");
    }
    public static bool Slug(string value)=>Regex.IsMatch(value,"^[a-z0-9][a-z0-9-]{0,63}$");
    public static PublishingException Invalid(string code)=>new(422,code,"The complete Player catalogue, immutable pins and publication lineage must remain compatible.");
}
