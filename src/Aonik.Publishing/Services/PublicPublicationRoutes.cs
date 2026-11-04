using Microsoft.AspNetCore.Http;

namespace Aonik.Publishing.Services;

public static class PublicPublicationRoutes
{
    public const string ResolvedChannelItem="Aonik.ServerResolvedPublicPublicationChannel";
    public static bool Match(PathString path,string method,out string channel)
    {
        channel="";if(!HttpMethods.IsGet(method)||path.Value is not { } value)return false;
        var s=value.Split('/');
        if(s.Length is not (5 or 6)||s[0]!=""||s[1]!="publishing"||s[2]!="channels"||!PlayerCatalogueProjection.Slug(s[3]))return false;
        if(s.Length==5&&s[4]=="catalogue.json"||s.Length==6&&s[4]=="assets"&&Guid.TryParseExact(s[5],"D",out _)){channel=s[3];return true;}
        return false;
    }
}
