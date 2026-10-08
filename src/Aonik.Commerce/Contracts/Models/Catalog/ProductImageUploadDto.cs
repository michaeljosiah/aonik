namespace Aonik.Commerce.Contracts.Models.Catalog;

/// <summary>An uploaded draft asset; the ordered media replacement persists its attachment.</summary>
public sealed record ProductImageUploadDto(string Url, string AltText);
