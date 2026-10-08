using System.Text.Json.Serialization;

namespace Aonik.Commerce.Contracts.Models.Catalog;

/// <summary>The 14 regulated food allergen groups. A reviewed empty list is not a free-from claim.</summary>
[JsonConverter(typeof(RegulatedAllergenJsonConverter))]
public enum RegulatedAllergen
{
    Celery,
    CerealsContainingGluten,
    Crustaceans,
    Eggs,
    Fish,
    Lupin,
    Milk,
    Molluscs,
    Mustard,
    Peanuts,
    Sesame,
    Soybeans,
    SulphurDioxideAndSulphites,
    TreeNuts
}
