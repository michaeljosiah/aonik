namespace Aonik.Publishing.Endpoints;

public sealed class PublicationCatalogueQuery
{
    public string ChannelKey { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
